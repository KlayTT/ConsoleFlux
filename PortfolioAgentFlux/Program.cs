using Microsoft.Extensions.AI; 
using OllamaSharp;          
using PortfolioAgentFlux.GithubServicesandFiles; // Assuming this is your namespace for the new file
using PortfolioAgentFlux.NonGitServices; //whoops I didn't add this the first time around

// ==========================================
// 1. SETUP & PROTECTION
// ==========================================
string tokenFolder = "GithubServicesandFiles";
string tokenPath = Path.Combine(Directory.GetCurrentDirectory(), tokenFolder, "git_token.txt");

Console.WriteLine($"🔍 Looking for token at: {tokenPath}");

if (!File.Exists(tokenPath))
{
    Console.WriteLine($"⚠️ Error: Could not find {tokenPath}");
    return;
}

string githubToken = File.ReadAllText(tokenPath).Trim();

// ==========================================
// 2. THE BRAIN & SERVICES
// ==========================================
IChatClient brain = new OllamaApiClient(new Uri("http://localhost:11434"), "llama3.2");

var githubService = new GitHubService(githubToken);
var securityService = new SecurityService(); // <--- 1. INITIALIZED SECURITY SERVICE
var testingService = new TestingService();

// ==========================================
// 3. THE TOOLS
// ==========================================
var getProjectsTool = AIFunctionFactory.Create(
    async () => await githubService.GetMyProjects(),
    "GetRepositories",
    "Use ONLY to get the overall LIST of all repositories.");

var getReadmeTool = AIFunctionFactory.Create(
    async (string repoName) => await githubService.GetReadme(repoName),
    "GetProjectDetails",
    "Use ONLY to fetch the README content for a SPECIFIC project name.");

var getRecentCommitsTool = AIFunctionFactory.Create(
    async (string repoName, int count) => await githubService.GetRecentCommits(repoName, count),
    "GetRecentCommits",
    "Use this to fetch the most recent commit messages for a project.");

// 2. CREATED THE SECURITY TOOL
var scanForSecretsTool = AIFunctionFactory.Create(
    (string fileName, string content) => securityService.ScanContent(fileName, content),
    "ScanForSecrets",
    "Scans code or text for potential security risks like API keys or passwords. Use this if a user asks to 'check' or 'audit' a file.");

var getTestSuggestionsTool = AIFunctionFactory.Create(
    (string codeSnippet) => testingService.AnalyzeCodeForTests(codeSnippet),
    "ReviewCodeForTests",
    "Use this to analyze a code snippet and get suggestions for unit tests. Use this when the user asks to 'review', 'check', or 'suggest tests' for code.");

// ==========================================
// 4. CHAT CONFIGURATION
// ==========================================
var chatHistory = new List<ChatMessage>
{
    new ChatMessage(ChatRole.System, 
        "You are Flux, Klay's portfolio assistant. Your primary job is to show off Klay's work. " +
        "If asked to 'audit', 'check security', or 'scan' a project, use the ScanForSecrets tool on the README or code provided.")
};

var chatOptions = new ChatOptions
{
    // 3. ADDED TOOL TO THE OPTIONS LIST
    Tools = new List<AITool> { getProjectsTool, getReadmeTool, getRecentCommitsTool, scanForSecretsTool }
};

// ==========================================
// 5. THE MAIN LOOP
// ==========================================
Console.WriteLine("🚀 Flux is live and connected to GitHub!");

// 4. ADDED TO THE TOOL MAP
var toolMap = new Dictionary<string, AIFunction>
{
    { "GetRepositories", getProjectsTool },
    { "GetProjectDetails", getReadmeTool },
    { "GetRecentCommits" , getRecentCommitsTool },
    { "ScanForSecrets", scanForSecretsTool }
};

while (true)
{
    Console.Write("\nYou: ");
    string? userMessage = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(userMessage)) continue;
    if (userMessage.ToLower() == "exit") break;
    int loopSafetyCounter = 0; // Prevent infinite AI loops
    
    chatHistory.Add(new ChatMessage(ChatRole.User, userMessage));

    string[] lazyKeywords = { "hi", "hey", "hello", "bye", "thanks", "signing off", "cya" };
    bool isSimpleTalk = lazyKeywords.Any(word => userMessage.ToLower().Contains(word));

    try 
    {
        bool responseNeedsProcessing = true;
        while (responseNeedsProcessing && loopSafetyCounter < 3)
        {
            loopSafetyCounter++;
            var currentOptions = isSimpleTalk ? new ChatOptions { Tools = null } : chatOptions;
            var response = await brain.GetResponseAsync(chatHistory, currentOptions);
            // If it was just simple talk, don't let him even think about tool calls
            if (isSimpleTalk) responseNeedsProcessing = false;
            var lastMessage = response.Messages.Last();
            var toolCalls = lastMessage.Contents.OfType<FunctionCallContent>().ToList();
            
            if (toolCalls.Any())
            {
                chatHistory.Add(lastMessage); 

                foreach (var call in toolCalls)
                {
                    if (toolMap.TryGetValue(call.Name, out var toolToRun))
                    {
                        Console.WriteLine($"🔧 [Flux] Executing: {call.Name}...");
                        var toolArgs = new AIFunctionArguments(call.Arguments);
                        var result = await toolToRun.InvokeAsync(toolArgs);
                        var resultContent = new FunctionResultContent(call.CallId, result?.ToString() ?? "No data.");
                        chatHistory.Add(new ChatMessage(ChatRole.Tool, [resultContent]));
                    }
                }
                responseNeedsProcessing = true; 
            }
            else
            {
                string assistantText = response.ToString();
                if (!string.IsNullOrWhiteSpace(assistantText))
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