namespace PortfolioAgentFlux.GithubServicesandFiles;

public class SecurityService
{
    // Simple regex-style patterns for common secrets
    private readonly string[] _riskPatterns = { "api_key", "password", "secret", "token", "password=" };

    public string ScanContent(string fileName, string content)
    {
        var foundRisks = new List<string>();

        foreach (var pattern in _riskPatterns)
        {
            if (content.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                foundRisks.Add(pattern);
            }
        }

        if (foundRisks.Any())
        {
            return $"[SECURITY ALERT] Found potential risks in {fileName}: {string.Join(", ", foundRisks)}. Please review before pushing!";
        }

        return $"[CLEAN] No obvious secrets found in {fileName}.";
    }
}