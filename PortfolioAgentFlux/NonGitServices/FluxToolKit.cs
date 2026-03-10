using Microsoft.Extensions.AI;
using PortfolioAgentFlux.GithubServicesandFiles;

namespace PortfolioAgentFlux.NonGitServices;

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
                "Reads local source code. Use this for files within THIS current project."),
            // 7. Language Filter
            AIFunctionFactory.Create(async (string language) => 
                    await FilterReposByLanguage(language), 
                "FilterProjectsByLanguage", "Returns a list of repositories that primarily use a specific language (e.g., 'C#', 'TypeScript')."),
            // 8. Recent Projects Fetcher
            AIFunctionFactory.Create(async (int limit) => 
                    await GetRecentlyPushedProjects(limit), 
                "GetRecentProjects", "Returns the most recently updated repositories (e.g., 'What has Klay been working on lately?').")
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
    private async Task<string> FilterReposByLanguage(string language)
    {
        try
        {
            var allRepos = await _githubService.GetRawRepoList();
    
            var filtered = allRepos
                .Where(r => string.Equals(r.Language, language, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(r => r.PushedAt) // Keep the most recent at the top
                .ToList();

            if (!filtered.Any())
                return $"🔍 No projects found where the primary language is '{language}'.";

            var result = $"📂 Found {filtered.Count} projects using {language} (Sorted by Recent):\n";
            foreach (var repo in filtered)
            {
                string dateStr = repo.PushedAt?.ToString("MMM dd, yyyy") ?? "Unknown";
                result += $"- {repo.Name} (Stars: {repo.StargazersCount} | Last Pushed: {dateStr})\n";
            }
            return result;
        }
        catch (Exception ex)
        {
            return $"❌ Error filtering projects: {ex.Message}";
        }
    }
    private async Task<string> GetRecentlyPushedProjects(int limit = 3)
    {
        try
        {
            var allRepos = await _githubService.GetRawRepoList();
        
            // Sort by PushedAt descending (most recent first)
            var recent = allRepos
                .Where(r => r.PushedAt.HasValue)
                .OrderByDescending(r => r.PushedAt)
                .Take(limit)
                .ToList();

            if (!recent.Any())
                return "🔍 I couldn't find any recently updated projects.";

            var result = $"🕒 Klay's {recent.Count} Most Recent Projects:\n";
            foreach (var repo in recent)
            {
                // Format the date nicely
                string dateStr = repo.PushedAt?.ToString("MMM dd, yyyy") ?? "Unknown";
                result += $"- {repo.Name} (Last Pushed: {dateStr})\n";
            }
            return result;
        }
        catch (Exception ex)
        {
            return $"❌ Error fetching recent projects: {ex.Message}";
        }
    }
}