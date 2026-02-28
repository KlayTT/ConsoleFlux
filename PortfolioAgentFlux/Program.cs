using Microsoft.Extensions.AI; 
using OllamaSharp;          
using PortfolioAgentFlux.GithubServicesandFiles; 
using PortfolioAgentFlux.NonGitServices; 
using System.Linq;

// ==========================================
// 1. SETUP & PROTECTION
// ==========================================
string tokenFolder = "GithubServicesandFiles";
string tokenPath = Path.Combine(Directory.GetCurrentDirectory(), tokenFolder, "git_token.txt");

if (!File.Exists(tokenPath))
{
    Console.WriteLine($"⚠️ Error: Could not find {tokenPath}");
    return;
}
string githubToken = File.ReadAllText(tokenPath).Trim();

// ==========================================
// 2. THE BRAIN (Now with Automatic Tool Invocation)
// ==========================================
// The base client talks to Ollama
IChatClient innerClient = new OllamaApiClient(new Uri("http://localhost:11434"), "llama3.2");

// The builder wraps it so tools are executed AUTOMATICALLY
IChatClient brain = innerClient.AsBuilder()
    .UseFunctionInvocation() 
    .Build();

var githubService = new GitHubService(githubToken);
var securityService = new SecurityService(); 
var testingService = new TestingService();

// ==========================================
// 3. THE TOOLS (Definitions stay the same)
// ==========================================
var getProjectsTool = AIFunctionFactory.Create(
        async () => await githubService.GetMyProjects(), "GetRepositories", "Lists all repositories.");
var getReadmeTool = AIFunctionFactory.Create(
    async (string repoName) => await githubService.GetReadme(repoName), 
    "GetProjectDetails", 
    "Fetches README content. IMPORTANT: The repoName MUST be the exact, case-sensitive string found in the GetRepositories list (e.g., 'Part-PartnerAPI').");
var getRecentCommitsTool = AIFunctionFactory.Create(
        async (string repoName, int count) => await githubService.GetRecentCommits(repoName, count), "GetRecentCommits", "Fetches recent commits.");
var scanForSecretsTool = AIFunctionFactory.Create(
        (string fileName, string content) => securityService.ScanContent(fileName, content), "ScanForSecrets", "Audits code for secrets.");
var getTestSuggestionsTool = AIFunctionFactory.Create(
        (string codeSnippet) => testingService.AnalyzeCodeForTests(codeSnippet), "ReviewCodeForTests", "Suggests unit tests.");
var readProjectFileTool = AIFunctionFactory.Create(
    (string fileName) => {
        try {
            string projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
            
            // 1. IMPROVED: Search for the file to handle casing issues (e.g., securityservice vs SecurityService)
            var foundFile = Directory.GetFiles(projectRoot, "*", SearchOption.AllDirectories)
                .FirstOrDefault(f => Path.GetFileName(f).Equals(fileName, StringComparison.OrdinalIgnoreCase));

            if (foundFile == null || !File.Exists(foundFile)) 
                return $"❌ Error: File '{fileName}' not found in the project structure. Please check the spelling.";

            string content = File.ReadAllText(foundFile);
            
            var lines = content.Split('\n');
            if (lines.Length > 500) {
                return $"⚠️ Warning: File is very large. Here is the start:\n" + string.Join("\n", lines.Take(100));
            }

            return content;
        } catch (Exception ex) {
            return $"❌ Error accessing file: {ex.Message}";
        }
    },
    "ReadProjectFile",
    "Reads a local file's source code. Use this whenever you need to see the actual contents of a file in this project.");
// ==========================================
// 4. CHAT CONFIGURATION
// ==========================================
var chatOptions = new ChatOptions
{
    Tools = new List<AITool> { getProjectsTool, getReadmeTool, getRecentCommitsTool, scanForSecretsTool, getTestSuggestionsTool, readProjectFileTool }
};

var chatHistory = new List<ChatMessage>
{
    new ChatMessage(ChatRole.System,
        "You are Flux, Klay's AI Partner. " +
        "CRITICAL RULES: " +
        "1. For greetings (Hey, Hello, Hi, etc.), ONLY respond with a text-based greeting like 'Hello Klay!'. " +
        "2. DO NOT call 'GetRepositories' or any other tool unless Klay specifically asks for data, files, or GitHub info. " +
        "3. Only use a tool if it is absolutely necessary to answer a specific request.")
};
Console.WriteLine("🚀 Flux is live and connected to GitHub!");

while (true)
{
    Console.Write("\nYou: ");
    string? userInput = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(userInput)) continue;

    chatHistory.Add(new ChatMessage(ChatRole.User, userInput));
    Console.Write("Flux: ");

    try 
    {
        // GetResponseAsync with FunctionInvocation handles the tool history for us.
        var response = await brain.GetResponseAsync(chatHistory, chatOptions);
        
        // Use the built-in Text property if available, otherwise ToString()
        string responseText = response.ToString();

        // CLEANUP: If it's a JSON string or blank, we need to extract the actual words.
        if (string.IsNullOrWhiteSpace(responseText) || responseText == "{}" || responseText.Contains("\"CallId\""))
        {
            // Look for the last message Flux actually wrote to the history during his "thought process"
            var lastAssistantMsg = chatHistory.LastOrDefault(m => m.Role == ChatRole.Assistant && !string.IsNullOrEmpty(m.Text));
            responseText = lastAssistantMsg?.Text ?? "Done! What's next?";
        }

        // Only print if we have something new.
        Console.WriteLine(responseText);

        // CRITICAL SYNC: Only add to history if the brain didn't already add it.
        // This prevents the "Double Vision" that makes him think there's a path error.
        if (chatHistory.LastOrDefault()?.Text != responseText)
        {
            chatHistory.Add(new ChatMessage(ChatRole.Assistant, responseText));
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\n❌ Flux Error: {ex.Message}");
    }
}