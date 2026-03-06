using Microsoft.Extensions.AI; 
using OllamaSharp;          
using PortfolioAgentFlux.Services; // Ensure this matches your namespace

// ==========================================
// 1. SETUP & PROTECTION 
// ==========================================
// No longer need txt file
string? githubToken = Environment.GetEnvironmentVariable("FLUX_GIT_TOKEN");

if (string.IsNullOrEmpty(githubToken))
{
    Console.WriteLine("⚠️ Error: 'FLUX_GIT_TOKEN' environment variable not found.");
    Console.WriteLine("Please set it in Windows Environment Variables and restart your IDE.");
    return;
}

// 2. THE BRAIN
IChatClient innerClient = new OllamaApiClient(new Uri("http://localhost:11434"), "llama3.2");
IChatClient brain = innerClient.AsBuilder().UseFunctionInvocation().Build();

// 3. THE TOOLS (REFACTORED)
// We just initialize the Toolkit and pull the list.
var toolKit = new FluxToolKit(githubToken);
var chatOptions = new ChatOptions { Tools = toolKit.GetTools() };

// 4. CHAT CONFIGURATION
var chatHistory = new List<ChatMessage>
{
    new ChatMessage(ChatRole.System,
        "You are Flux, Klay's AI Partner. " +
        "1. If Klay is just saying hello or chatting, respond naturally without calling tools. " +
        "2. ONLY call 'GetRepositories' if he asks to find or list projects. " +
        "3. Once a search keyword is given (e.g., 'Pickle'), call 'GetRepositories', " +
        "find the EXACT case-sensitive match (e.g., 'PickleProject'), and then use 'GetProjectDetails'. " +
        "4. Always present retrieved README content directly to Klay.")
};

Console.WriteLine("Flux: [Connected]");

while (true)
{
    Console.Write("\nYou: ");
    string? userInput = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(userInput)) continue;

    chatHistory.Add(new ChatMessage(ChatRole.User, userInput));
    Console.Write("Flux: ");

    try 
    {
        // GetResponseAsync with FunctionInvocation handles the tool history for us.
        var response = await brain.GetResponseAsync(chatHistory, chatOptions);
        
        // Use the built-in Text property if available, otherwise ToString()
        string responseText = response.ToString();

        // CLEANUP: If it's a JSON string or blank, we need to extract the actual words.
        if (string.IsNullOrWhiteSpace(responseText) || responseText == "{}" || responseText.Contains("\"CallId\""))
        {
            // Look for the last message Flux actually wrote to the history during his "thought process"
            var lastAssistantMsg = chatHistory.LastOrDefault(m => m.Role == ChatRole.Assistant && !string.IsNullOrEmpty(m.Text));
            responseText = lastAssistantMsg?.Text ?? "Done! What's next?";
        }

        // Only print if we have something new.
        Console.WriteLine(responseText);

        // CRITICAL SYNC: Only add to history if the brain didn't already add it.
        // This prevents the "Double Vision" that makes him think there's a path error.
        if (chatHistory.LastOrDefault()?.Text != responseText)
        {
            chatHistory.Add(new ChatMessage(ChatRole.Assistant, responseText));
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\n❌ Flux Error: {ex.Message}");
    }
}