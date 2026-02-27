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
var getProjectsTool = AIFunctionFactory.Create(async () => await githubService.GetMyProjects(), "GetRepositories", "Lists all repositories.");
var getReadmeTool = AIFunctionFactory.Create(async (string repoName) => await githubService.GetReadme(repoName), "GetProjectDetails", "Fetches README content.");
var getRecentCommitsTool = AIFunctionFactory.Create(async (string repoName, int count) => await githubService.GetRecentCommits(repoName, count), "GetRecentCommits", "Fetches recent commits.");
var scanForSecretsTool = AIFunctionFactory.Create((string fileName, string content) => securityService.ScanContent(fileName, content), "ScanForSecrets", "Audits code for secrets.");
var getTestSuggestionsTool = AIFunctionFactory.Create((string codeSnippet) => testingService.AnalyzeCodeForTests(codeSnippet), "ReviewCodeForTests", "Suggests unit tests.");
var readProjectFileTool = AIFunctionFactory.Create((string fileName) => {
    try {
        string projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
        string filePath = Path.Combine(projectRoot, fileName);
        return File.Exists(filePath) ? File.ReadAllText(filePath) : $"❌ Error: File '{fileName}' not found.";
    } catch (Exception ex) { return $"❌ Error: {ex.Message}"; }
}, "ReadProjectFile", "Reads a local file's source code.");

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
        "You are Flux, Klay's witty Portfolio Agent. " +
        "CRITICAL RULES: " +
        "1. Do NOT use tools or search for repositories unless Klay explicitly asks you to. " +
        "2. If Klay just says 'Hello' or 'Hey', just respond with a greeting. " +
        "3. Be concise and conversational. " +
        "4. Only provide repository lists or file contents when directly requested.")
};

Console.WriteLine("🚀 Flux is live and connected to GitHub!");

// ==========================================
// 5. THE MAIN LOOP (Clean & Simple)
// ==========================================
while (true)
{
    Console.Write("\nYou: ");
    string? userInput = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(userInput)) continue;

    chatHistory.Add(new ChatMessage(ChatRole.User, userInput));

    Console.Write("Flux: ");

    try 
    {
        // Get the response (The Invoker handles the tools automatically)
        var response = await brain.GetResponseAsync(chatHistory, chatOptions);

        // Bypass properties: Extract the text via ToString()
        string responseText = response.ToString();

        // If the response text is empty or just brackets, peek at the history 
        // because the FunctionInvocation builder often injects the final answer there.
        if (string.IsNullOrWhiteSpace(responseText) || responseText == "{}")
        {
            responseText = chatHistory.LastOrDefault(m => m.Role == ChatRole.Assistant)?.Text 
                           ?? "Flux is processing...";
        }

        Console.WriteLine(responseText);

        // Ensure the Assistant's reply is in the history for the next turn
        // We only add it if the invoker didn't already put it there.
        if (chatHistory.LastOrDefault()?.Role != ChatRole.Assistant)
        {
            chatHistory.Add(new ChatMessage(ChatRole.Assistant, responseText));
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\n❌ Flux Error: {ex.Message}");
    }
}