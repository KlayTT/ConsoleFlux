using ModelContextProtocol.Client;
//using System;

// 🛑 Update this path to your folder!
string serverProjectPath = @"C:\Users\User\RiderProjects\PortMCPKlayTT2\FluxPortfolioServer";

Console.WriteLine("Initializing Flux Agent...");

// 1. Setup the Transport
var transport = new StdioClientTransport(new StdioClientTransportOptions
{
    Name = "Klay-Portfolio-Agent",
    Command = "dotnet",
    Arguments = ["run", "--project", serverProjectPath, "--no-build"]
});

// 2. Create the Client
var client = await McpClient.CreateAsync(transport);

Console.WriteLine("--- Connected to Flux Portfolio Server ---");

// 3. List tools
var tools = await client.ListToolsAsync();
Console.WriteLine($"\nDiscovered {tools.Count} tools:");
foreach (var tool in tools)
{
    Console.WriteLine($"- {tool.Name}: {tool.Description}");
}

// 4. Test the tools - Fix the names here!
Console.WriteLine("\n--- Testing Tools ---");

// Change 'GetResume' to 'get_resume'
var resumeResponse = await client.CallToolAsync("get_resume"); 
Console.WriteLine($"Resume Response: {resumeResponse}");

// Change 'GetGithubStats' to 'get_github_repos'
var statsResponse = await client.CallToolAsync("get_github_repos"); 
Console.WriteLine($"GitHub Stats: {statsResponse}");