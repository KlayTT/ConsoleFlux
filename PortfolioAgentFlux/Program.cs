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

var getRecentCommitsTool = AIFunctionFactory.Create(
    async (string repoName, int count) => await githubService.GetRecentCommits(repoName, count),
    "GetRecentCommits",
    "Use this to fetch the most recent commit messages for a project. This helps show recent progress, work ethic, and specific updates made to a repository."
);

// ==========================================
// 4. CHAT CONFIGURATION
// ==========================================
var chatHistory = new List<ChatMessage>
{
    new ChatMessage(ChatRole.System, 
        "You are Flux, Klay's portfolio assistant. Your primary job is to show off Klay's work. " +
        "GUIDELINES: " +
        "1. For general greetings, just be friendly. " +
        "2. If Klay asks for 'activity', 'updates', 'what I've been doing', or 'recent work', you MUST use 'GetRecentCommits'. " +
        "3. Use 'GetProjectDetails' ONLY when specifically asked about how a project works or for its README content. " +
        "4. If Klay mentions a project name with a space (like 'Console Flux'), interpret it as the single string 'ConsoleFlux'.")
};

var chatOptions = new ChatOptions
{
    // You need to add the new tool here so the AI knows it's available!
    Tools = new List<AITool> { getProjectsTool, getReadmeTool, getRecentCommitsTool }
};

// ==========================================
// 5. THE MAIN LOOP
// ==========================================
Console.WriteLine("🚀 Flux is live and connected to GitHub!");
var toolMap = new Dictionary<string, AIFunction>
{
    { "GetRepositories", getProjectsTool },
    { "GetProjectDetails", getReadmeTool },
    { "GetRecentCommits" , getRecentCommitsTool }
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
            // Use the simpler approach: Get all for current user, then filter the list in memory
            var repos = await _client.Repository.GetAllForCurrent();
        
            // Filter: Only show repos where Klay is the owner AND they are public
            var filteredRepos = repos
                .Where(r => r.Owner.Login.Equals("KlayTT", StringComparison.OrdinalIgnoreCase) && !r.Private)
                .Select(r => $"{r.Name}: {r.Description ?? "No description"}");

            if (!filteredRepos.Any()) return "No public repositories found for KlayTT.";

            return "Klay's GitHub Repos:\n" + string.Join("\n", filteredRepos);
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
    public async Task<string> GetRecentCommits(string repoName, int count = 5)
    {
        try 
        {
            // 1. Create a request to filter for your commits
            var request = new CommitRequest 
            { 
                Author = "KlayTT" 
            };

            // 2. Use ApiOptions to limit the result count
            var options = new ApiOptions
            {
                PageCount = 1,
                PageSize = count
            };

            // 3. Fetch using the correct field name (_client)
            var commits = await _client.Repository.Commit.GetAll("KlayTT", repoName, request, options);

            if (!commits.Any()) return $"No recent commits found for {repoName} by KlayTT.";

            var commitLogs = commits.Select(c => 
                $"[{c.Commit.Author.Date.ToString("yyyy-MM-dd")}] {c.Commit.Message}");

            return $"Recent activity for {repoName}:\n" + string.Join("\n", commitLogs);
        }
        catch (Exception ex)
        {
            return $"Error fetching commits for {repoName}: {ex.Message}";
        }
    }
}