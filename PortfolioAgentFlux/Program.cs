using Microsoft.Extensions.AI;
using OllamaSharp;
// The core client and transport now live here
using ModelContextProtocol;
using ModelContextProtocol.Client;

// 1. Setup the Brain
// OllamaApiClient implements IChatClient natively in 4.0.1
IChatClient innerClient = new OllamaApiClient(new Uri("http://localhost:11434"), "llama3.2");

// 2. Wrap it for tool capability
var brain = new ChatClientBuilder(innerClient)
    .UseFunctionInvocation()
    .Build();

// 3. Connect to MCP Server 
// StdioClientTransport is now in the ModelContextProtocol namespace
var transport = new StdioClientTransport(new StdioClientTransportOptions {
    Command = "dotnet",
    // ".." goes up to RiderProjects, then we go down into the server folder
    Arguments = ["run", "--project", @"..\..\PortMCPKlayTT2\FluxPortfolioServer\FluxPortfolioServer.csproj"]
});

await using var mcpClient = await McpClient.CreateAsync(transport);

// 4. Link Tools
// ListToolsAsync returns the list directly in this version
var mcpTools = await mcpClient.ListToolsAsync();
var aiTools = mcpTools.Cast<AITool>().ToList();

// 5. Interaction Loop
Console.WriteLine("🚀 Agent is live!");

while (true)
{
    Console.Write("\nYou: ");
    string? input = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(input)) break;

    var response = await brain.GetResponseAsync(input, new ChatOptions { Tools = aiTools });
    Console.WriteLine($"\nAgent: {response.Text}");
}