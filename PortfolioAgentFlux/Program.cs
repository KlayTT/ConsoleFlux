using Microsoft.Extensions.AI; 
using OllamaSharp;          
using PortfolioAgentFlux.NonGitServices; // Ensure this matches your namespace

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
        "1. Do NOT call tools for casual conversation or greetings. " +
        "2. ONLY call a tool if Klay specifically asks for information you don't have (e.g., listing repos, checking code, or filtering by language). " +
        "3. If Klay asks for projects by a specific language, use 'FilterProjectsByLanguage'. " +
        "4. Be concise and wait for instructions before acting." +
        "5. If Klay asks what you've been working on or wants recent projects, use 'GetRecentProjects'.")
};

Console.WriteLine("Flux: [Connected]");

while (true)
{
    Console.Write("\nYou: ");
    string? userInput = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(userInput)) continue;

    chatHistory.Add(new ChatMessage(ChatRole.User, userInput));
    Console.Write("Flux: ");

    // 1. Declare the variable OUTSIDE the try block
    string responseText = string.Empty;

    try 
    {
        var response = await brain.GetResponseAsync(chatHistory, chatOptions);
        
        // 2. Assign the value
        responseText = response.ToString() ?? "";

        if (string.IsNullOrWhiteSpace(responseText) || responseText == "{}" || responseText.Contains("\"CallId\""))
        {
            var lastAssistantMsg = chatHistory.LastOrDefault(m => m.Role == ChatRole.Assistant && !string.IsNullOrEmpty(m.Text));
            responseText = lastAssistantMsg?.Text ?? "Done! What's next?";
        }

        Console.WriteLine(responseText);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\n❌ Flux Error: {ex.Message}");
        continue; // Skip the history sync if it crashed
    }

    // 3. Now this block can see responseText perfectly!
    if (!string.IsNullOrEmpty(responseText) && chatHistory.LastOrDefault()?.Text != responseText)
    {
        chatHistory.Add(new ChatMessage(ChatRole.Assistant, responseText));
    }
}