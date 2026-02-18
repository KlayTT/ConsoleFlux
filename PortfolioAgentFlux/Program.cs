using Microsoft.Extensions.AI;
using OllamaSharp;
using ModelContextProtocol.Client;
/*
Current Work Flow.
_________________  
User asks question.
LLM decides if a tool is needed.
LLM outputs JSON (the tool call).
Your Code executes the tool and sends data back.
LLM translates data into a human sentence.
*/


// 1. Setup the Brain
// OllamaApiClient implements IChatClient natively in 4.0.1
IChatClient innerClient = new OllamaApiClient(new Uri("http://localhost:11434"), "llama3.2");

// 2. Wrap it for tool capability (keep this simple)
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

var chatHistory = new List<ChatMessage>
{
    new ChatMessage(ChatRole.System, 
        "You are Flux, a professional portfolio assistant for Klay Thacker. " +
        "RULE #1: Always respond in plain, conversational English first. " +
        "RULE #2: Do NOT output JSON or call tools unless the user explicitly asks for data like GitHub, Email, or Resume. " +
        "If you are just saying hello or introduced yourself, use text only.")
};

// --- THE NUDGE ---
chatHistory.Add(new ChatMessage(ChatRole.User, "Please introduce yourself briefly.")); 

var introResponse = await brain.GetResponseAsync(chatHistory);

// Add Flux's intro to history and show it
chatHistory.Add(new ChatMessage(ChatRole.Assistant, introResponse.Text));

// Only print if there's actual text (avoids blank lines or raw JSON)
if (!string.IsNullOrWhiteSpace(introResponse.Text))
{
    Console.WriteLine($"\nAgent: {introResponse.Text}");
}
// -----------------

while (true)
{
    Console.Write("\nYou: ");
    string? input = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(input) || input.ToLower() == "exit") break;

    chatHistory.Add(new ChatMessage(ChatRole.User, input));

    var response = await brain.GetResponseAsync(chatHistory, new ChatOptions { Tools = aiTools });
    
    chatHistory.Add(new ChatMessage(ChatRole.Assistant, response.Text));

    if (!string.IsNullOrWhiteSpace(response.Text))
    {
        Console.WriteLine($"\nAgent: {response.Text}");
    }
}