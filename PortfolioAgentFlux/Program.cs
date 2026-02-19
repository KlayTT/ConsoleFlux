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
        "You are Flux, a professional and friendly portfolio assistant for Klay Thacker. " +
        "Your goal is to be a supportive partner in Klay's professional growth. " +
        "RULE #1: Always respond in plain, conversational English first. " +
        "RULE #2: Do NOT output JSON or call tools unless the user explicitly asks for specific data like GitHub, Email, or Resume. " +
        "RULE #3: Keep your introduction very brief—one or two sentences max. " +
        "Keep your tone helpful, professional, and slightly enthusiastic about technology.")
};

// --- THE NUDGE ---
// We add a hidden user prompt to force the first response to be text-only.
// We explicitly tell it NOT to use tools here.
var introOptions = new ChatOptions { Tools = new List<AITool>() }; 
chatHistory.Add(new ChatMessage(ChatRole.User, "Please introduce yourself briefly.")); 

var introResponse = await brain.GetResponseAsync(chatHistory, introOptions);

// Add Flux's intro to history and show it
chatHistory.Add(new ChatMessage(ChatRole.Assistant, introResponse.Text));
Console.WriteLine($"\nAgent: {introResponse.Text}");
// -----------------

while (true)
{
    Console.Write("\nYou: ");
    var userMessage = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(userMessage)) continue;

    chatHistory.Add(new ChatMessage(ChatRole.User, userMessage));

    try 
    {
        // Get the response from the agent
        var response = await brain.GetResponseAsync(chatHistory);
        
        // Add response to history and print it
        chatHistory.Add(new ChatMessage(ChatRole.Assistant, response.Text));
        Console.WriteLine($"\nAgent: {response.Text}");
    }
    catch (Exception ex)
    {
        // This stops the whole server from crashing if something goes wrong!
        Console.WriteLine($"\n[SYSTEM ERROR]: {ex.Message}");
    
        // Nudge Flux to NOT retry the tool immediately
        chatHistory.Add(new ChatMessage(ChatRole.Assistant, 
            "I ran into an issue with a tool. I will wait for your next instruction."));
    }
}