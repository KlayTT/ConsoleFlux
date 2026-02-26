using Octokit;

namespace PortfolioAgentFlux.GithubServicesandFiles;

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