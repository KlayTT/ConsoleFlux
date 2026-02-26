namespace PortfolioAgentFlux.NonGitServices;

public class TestingService
{
    public string AnalyzeCodeForTests(string codeSnippet)
    {
        // A simple list to collect suggestions
        var suggestions = new List<string>();

        // 1. Check for Null Checks
        if (codeSnippet.Contains("class") || codeSnippet.Contains("void") || codeSnippet.Contains("Task"))
        {
            if (!codeSnippet.Contains("== null") && !codeSnippet.Contains("is null"))
            {
                suggestions.Add("⚠️ Missing Null Checks: Ensure you test how this code handles null inputs.");
            }
        }

        // 2. Check for Loops (Edge Cases)
        if (codeSnippet.Contains("for") || codeSnippet.Contains("foreach") || codeSnippet.Contains(".Select"))
        {
            suggestions.Add("⚠️ Collection Edge Cases: Test with an empty list and a list with only one item.");
        }

        // 3. Check for Strings
        if (codeSnippet.Contains("string"))
        {
            suggestions.Add("⚠️ String Inputs: Test with String.Empty and very long strings.");
        }

        if (suggestions.Count == 0)
            return "Flux reviewed the code and it looks solid, but manual unit tests are always recommended!";

        return "### 🧪 Unit Test Recommendations:\n" + string.Join("\n", suggestions);
    }
}