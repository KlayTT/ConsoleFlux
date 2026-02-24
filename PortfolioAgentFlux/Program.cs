using Octokit;
using Microsoft.Extensions.AI; 
using OllamaSharp;          
using System.IO;
using System.ComponentModel; // Added for tool descriptions
using PortfolioAgentFlux.GithubServicesandFiles; // Assuming this is your namespace for the new file

// ==========================================
// 1. SETUP & PROTECTION
// ==========================================
string tokenFolder = "GithubServicesandFiles";
string tokenFile = "git_token.txt";
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
    
    chatHistory.Add(new ChatMessage(ChatRole.User, userMessage));

    string[] lazyKeywords = { "hi", "hey", "hello", "bye", "thanks", "signing off", "cya" };
    bool isSimpleTalk = lazyKeywords.Any(word => userMessage.ToLower().Contains(word));

    try 
    {
        bool responseNeedsProcessing = true;
        while (responseNeedsProcessing)
        {
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
            var filteredRepos = repos
                .Where(r => r.Owner.Login.Equals("KlayTT", StringComparison.OrdinalIgnoreCase) && !r.Private)
                .Select(r => $"{r.Name}: {r.Description ?? "No description"}");

            if (!filteredRepos.Any()) return "No public repositories found for KlayTT.";
            return "Klay's GitHub Repos:\n" + string.Join("\n", filteredRepos);
        }
        catch (Exception ex) { return $"GitHub Error: {ex.Message}"; }
    }

    public async Task<string> GetReadme(string repoName)
    {
        try 
        {
            var readme = await _client.Repository.Content.GetReadme("KlayTT", repoName);
            string content = readme.Content;
            Console.WriteLine($"[SYSTEM] Successfully retrieved {repoName} README. Length: {content.Length} characters.");

            return $@"
            [DATABASE_RESULT_START]
            REPOSITORY: {repoName}
            FILE: README.md
            CONTENT: {content}
            [DATABASE_RESULT_END]
            INSTRUCTION: Use the content above to answer the user's request.";
        }
        catch (Exception ex) { return $"ERROR: {ex.Message}"; }
    }

    public async Task<string> GetRecentCommits(string repoName, int count = 5)
    {
        try 
        {
            var request = new CommitRequest { Author = "KlayTT" };
            var options = new ApiOptions { PageCount = 1, PageSize = count };
            var commits = await _client.Repository.Commit.GetAll("KlayTT", repoName, request, options);

            if (!commits.Any()) return $"No recent commits found for {repoName}.";
            var commitLogs = commits.Select(c => $"[{c.Commit.Author.Date:yyyy-MM-dd}] {c.Commit.Message}");
            return $"Recent activity for {repoName}:\n" + string.Join("\n", commitLogs);
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }
}