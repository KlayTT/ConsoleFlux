using Octokit;
using Microsoft.Extensions.AI; // Required for CompleteAsync
using OllamaSharp;          
using System.IO;

// ==========================================
// 1. SETUP & PROTECTION
// ==========================================
string tokenFolder = "GithubServicesandFiles";
string tokenFile = "git_token.txt";
string tokenPath = Path.Combine(Directory.GetCurrentDirectory(), tokenFolder, "git_token.txt");

// Diagnostic Print
Console.WriteLine($"🔍 Looking for token at: {tokenPath}");
Console.WriteLine($"📂 Current Execution Directory: {Directory.GetCurrentDirectory()}");

if (!File.Exists(tokenPath))
{
    Console.WriteLine($"⚠️ Error: Could not find {tokenPath}");
    // Let's list what DOES exist here to help us debug
    if (Directory.Exists(tokenFolder)) {
        Console.WriteLine($"✅ Folder '{tokenFolder}' found, but file might be missing.");
    } else {
        Console.WriteLine($"❌ Folder '{tokenFolder}' not found in current directory.");
    }
    return;
}

string githubToken = File.ReadAllText(tokenPath).Trim();

// ==========================================
// 2. THE BRAIN (OLLAMA + LLAMA)
// ==========================================
// We use the OllamaApiClient directly as the ChatClient
IChatClient brain = new OllamaApiClient(new Uri("http://localhost:11434"), "llama3.2");
var testResponse = await brain.GetResponseAsync("Hello world!");

// ==========================================
// 3. THE TOOLS
// ==========================================
var githubService = new GitHubService(githubToken);

var getProjectsTool = AIFunctionFactory.Create(
    async () => await githubService.GetMyProjects(),
    "GetRepositories",
    "Use ONLY to get the overall LIST of all repositories. Do NOT use this if the user mentions a specific project name.");

var getReadmeTool = AIFunctionFactory.Create(
    async (string repoName) => await githubService.GetReadme(repoName),
    "GetProjectDetails",
    "Use ONLY to fetch the README content for a SPECIFIC project name (e.g., 'PortMCPKlayTT2'). Use this when the user asks 'tell me more' or 'how does it work'." );

// ==========================================
// 4. CHAT CONFIGURATION
// ==========================================
var chatHistory = new List<ChatMessage>
{
    new ChatMessage(ChatRole.System, 
        "You are Flux, Klay's portfolio assistant. " +
        "CRITICAL: Do not use tools for small talk. " +
        "If Klay says 'Hi' or 'How are you', just be a friendly human. " +
        "Only use GetRepositories if the conversation is specifically about his code or GitHub." +
        "When you need to use a tool, do not type the JSON yourself; use the internal function calling mechanism." +
        "Before choosing a tool, check if the user is asking about a specific project name you already know. If they are, you MUST use 'GetProjectDetails' instead of 'GetRepositories'.")
};

var chatOptions = new ChatOptions
{
    Tools = new List<AITool> { getProjectsTool, getReadmeTool }
};

// ==========================================
// 5. THE MAIN LOOP
// ==========================================
Console.WriteLine("🚀 Flux is live and connected to GitHub!");
var toolMap = new Dictionary<string, AIFunction>
{
    { "GetRepositories", getProjectsTool },
    { "GetProjectDetails", getReadmeTool }
};

while (true)
{
    Console.Write("\nYou: ");
    string? userMessage = Console.ReadLine();
    
    if (string.IsNullOrWhiteSpace(userMessage)) continue;
    if (userMessage.ToLower() == "exit") break;
    
    // Add user input to history immediately
    chatHistory.Add(new ChatMessage(ChatRole.User, userMessage));

    // 1. THE HACK: Detect simple social talk to prevent over-working
    string[] lazyKeywords = { "hi", "hey", "hello", "bye", "thanks", "signing off", "cya" };
    bool isSimpleTalk = lazyKeywords.Any(word => userMessage.ToLower().Contains(word));

    try 
    {
        bool responseNeedsProcessing = true;
        while (responseNeedsProcessing)
        {
            // 2. APPLY HACK: Override options if it's just a greeting/goodbye
            var currentOptions = isSimpleTalk ? new ChatOptions { Tools = null } : chatOptions;
        
            var response = await brain.GetResponseAsync(chatHistory, currentOptions);
            responseNeedsProcessing = false;

            var lastMessage = response.Messages.Last();
            var toolCalls = lastMessage.Contents.OfType<FunctionCallContent>().ToList();
            
            if (toolCalls.Any())
            {
                chatHistory.Add(lastMessage); 

                foreach (var call in toolCalls)
                {
                    // 1. Look up the tool by name in our map
                    if (toolMap.TryGetValue(call.Name, out var toolToRun))
                    {
                        Console.WriteLine($"🔧 [Flux] Executing: {call.Name}...");
            
                        var toolArgs = new AIFunctionArguments(call.Arguments);
            
                        // 2. Run whichever tool the AI chose (GetRepositories OR GetProjectDetails)
                        var result = await toolToRun.InvokeAsync(toolArgs);
            
                        var resultContent = new FunctionResultContent(call.CallId, result?.ToString() ?? "No data.");
                        chatHistory.Add(new ChatMessage(ChatRole.Tool, [resultContent]));
                    }
                    else
                    {
                        Console.WriteLine($"⚠️ [Flux] tried to use unknown tool: {call.Name}");
                    }
                }

                responseNeedsProcessing = true; 
            }
            else
            {
                string assistantText = response.ToString();

                // 3. SANITY CHECK: Hide raw JSON leakage from the user
                bool isRawJson = assistantText.Trim().StartsWith("{") && assistantText.Contains("parameters");

                if (!isRawJson && !string.IsNullOrWhiteSpace(assistantText))
                {
                    Console.WriteLine($"Flux: {assistantText}");
                    chatHistory.Add(new ChatMessage(ChatRole.Assistant, assistantText));
                }
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Error: {ex.Message}");
    }
}

// ==========================================
// 6. CLASS DEFINITIONS (Bottom of file)
// ==========================================
public class GitHubService
{
    private readonly GitHubClient _client;

    public GitHubService(string token)
    {
        _client = new GitHubClient(new ProductHeaderValue("Flux-Portfolio-Agent"))
        {
            Credentials = new Credentials(token)
        };
    }

    public async Task<string> GetMyProjects()
    {
        try 
        {
            var repos = await _client.Repository.GetAllForCurrent();
            var repoInfo = repos.Select(r => $"{r.Name}: {r.Description ?? "No description"}");
            return "Klay's GitHub Repos:\n" + string.Join("\n", repoInfo);
        }
        catch (Exception ex)
        {
            return $"GitHub Error: {ex.Message}";
        }
    }
    public async Task<string> GetReadme(string repoName)
    {
        try 
        {
            // Explicitly using the KlayTT account from your link
            var readme = await _client.Repository.Content.GetReadme("KlayTT", repoName);
            return $"[CONTENT OF README.MD for {repoName}]:\n{readme.Content}";
        }
        catch (Exception ex)
        {
            return $"I tried to read the README for {repoName} but got an error: {ex.Message}";
        }
    }
}