using Microsoft.Extensions.AI;
using PortfolioAgentFlux.GithubServicesandFiles;
using PortfolioAgentFlux.NonGitServices;

namespace PortfolioAgentFlux.Services;

public class FluxToolKit
{
    // These are now private fields. Program.cs no longer needs to worry about them.
    private readonly GitHubService _githubService;
    private readonly SecurityService _securityService;
    private readonly TestingService _testingService;

    public FluxToolKit(string githubToken)
    {
        _githubService = new GitHubService(githubToken);
        _securityService = new SecurityService();
        _testingService = new TestingService();
    }

    /// <summary>
    /// Returns a list of all tools Flux can use. 
    /// Adding a new tool now only requires adding one entry here.
    /// </summary>
    public List<AITool> GetTools()
    {
        return new List<AITool>
        {
            // 1. GitHub Repository List
            AIFunctionFactory.Create(async () => 
                await _githubService.GetMyProjects(), "GetRepositories", "Lists all repositories."),

            // 2. GitHub README Fetcher
            AIFunctionFactory.Create(async (string repoName) => 
                await _githubService.GetReadme(repoName), 
                "GetProjectDetails", 
                "Fetches README content. Use the EXACT case-sensitive name from GetRepositories."),

            // 3. GitHub Commit Fetcher
            AIFunctionFactory.Create(async (string repoName, int count) => 
                await _githubService.GetRecentCommits(repoName, count), 
                "GetRecentCommits", "Fetches recent commits for a project."),

            // 4. Security Auditor
            AIFunctionFactory.Create((string fileName, string content) => 
                _securityService.ScanContent(fileName, content), 
                "ScanForSecrets", "Audits code for leaked secrets or keys."),

            // 5. Unit Test Suggester
            AIFunctionFactory.Create((string codeSnippet) => 
                _testingService.AnalyzeCodeForTests(codeSnippet), 
                "ReviewCodeForTests", "Suggests unit tests for a specific code snippet."),

            // 6. Local File Reader (Encapsulated logic for safety)
            AIFunctionFactory.Create(ReadLocalFile, "ReadProjectFile", 
                "Reads local source code. Use this for files within THIS current project.")
        };
    }

    // Moving the complex local file logic into a private method to keep the list clean
    private string ReadLocalFile(string fileName)
    {
        try 
        {
            // We use AppContext.BaseDirectory to find our way back to the source files
            string projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
            
            var foundFile = Directory.GetFiles(projectRoot, "*", SearchOption.AllDirectories)
                .FirstOrDefault(f => Path.GetFileName(f).Equals(fileName, StringComparison.OrdinalIgnoreCase));

            if (foundFile == null || !File.Exists(foundFile)) 
                return $"❌ Error: File '{fileName}' not found. Check spelling.";

            string content = File.ReadAllText(foundFile);
            var lines = content.Split('\n');
            
            return lines.Length > 500 
                ? $"⚠️ Warning: File is large. First 100 lines:\n{string.Join("\n", lines.Take(100))}" 
                : content;
        } 
        catch (Exception ex) 
        {
            return $"❌ Error accessing file: {ex.Message}";
        }
    }
}