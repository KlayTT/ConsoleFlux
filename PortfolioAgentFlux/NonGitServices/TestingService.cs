using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PortfolioAgentFlux.NonGitServices;

public class TestingService
{
    public string AnalyzeCodeForTests(string codeSnippet)
    {
        // 1. Parse the text into a "Syntax Tree"
        SyntaxTree tree = CSharpSyntaxTree.ParseText(codeSnippet);
        CompilationUnitSyntax root = tree.GetCompilationUnitRoot();

        var suggestions = new List<string>();

        // 2. Find all Method Declarations
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

        foreach (var method in methods)
        {
            string methodName = method.Identifier.Text;
            
            // Look at the parameters of the method
            foreach (var param in method.ParameterList.Parameters)
            {
                var assignments = method.DescendantNodes().OfType<AssignmentExpressionSyntax>();
                foreach (var assignment in assignments)
                {
                    var value = assignment.Right.ToString();
                    if (value.Contains("ghp_") || value.Contains("AIza")) // Common prefixes for GitHub/Google keys
                    {
                        suggestions.Add($"❌ Method '{methodName}': Potential hardcoded API key detected in assignment!");
                    }
                }
                // If the parameter is a string, suggest a null/empty check
                if (param.Type?.ToString() == "string")
                {
                    suggestions.Add($"⚠️ Method '{methodName}': Parameter '{param.Identifier.Text}' is a string. Suggest testing for null and String.Empty.");
                }
            }

            // Check if the method body contains a loop
            if (method.Body?.DescendantNodes().Any(n => n is ForStatementSyntax || n is ForEachStatementSyntax) == true)
            {
                suggestions.Add($"⚠️ Method '{methodName}': Contains a loop. Suggest testing with empty collections.");
            }
        }

        if (!suggestions.Any())
            return "Flux checked the structure via Roslyn. No obvious patterns found, but it looks clean!";

        return "### 🤖 Roslyn Deep Analysis:\n" + string.Join("\n", suggestions);
    }
}