## PortfolioAgentFlux

- An intelligent C# Console application acting as a personal portfolio assistant. This agent uses local LLMs to interact with the GitHub API, providing a conversational interface for exploring developer activity and project details.
_____
## UseCases

- Automated Portfolio Narrative: Summarizes commit history into human-readable progress updates.
- Interactive Resume: Answers questions about specific projects by reading repository READMEs.
- Developer Productivity: Quick access to repository lists and project metadata via natural language.
- Code Auditing: Real-time scanning for security risks and unit test suggestions using Roslyn-based analysis.

## Resources & Tech Stack

- Language: C# / .NET 10
- AI Orchestration: Microsoft.Extensions.AI
- Local LLM: Ollama (Llama 3.2) via OllamaSharp
- Static Analysis: Microsoft.CodeAnalysis (Roslyn)
- API Integration: Octokit (GitHub Client)

## RoadMap

* [x] Integrate local Llama 3.2 model.
* [x] Implement GitHub tool calling (Repositories, READMEs, Commits).
* [x] Integrate Roslyn for deep code analysis and security scanning.
* [x] Refactor into Clean Architecture (Migration to FluxToolKit and Service layers).
* [x] Safety Audit: Move sensitive credentials (Tokens) to Environment Variables.
* [x] Advanced Filtering: Implement language-specific and metadata-based repository analytics.
* [ ] Visual Identity: Develop Wireframes and Figma designs for the web-based portfolio frontend.

## System Architecture

- PortfolioAgentFlux follows a Service-Oriented Architecture designed for high modularity and clean separation of concerns:
- The Flux Engine (Program.cs): The central orchestrator that manages the conversation loop and coordinates between the user and the local LLM.
- FluxToolKit: Acts as the "Brain's Hands." It encapsulates all tool definitions, handling the logic of how the AI interacts with the underlying services.
- Service Layer: TBA
- GitHubService: Handles all Octokit-based communication with the GitHub API.
- SecurityService: Contains logic for pattern-based secret detection.
- TestingService: Leverages Roslyn (Microsoft.CodeAnalysis) to perform deep syntax tree analysis for unit test generation.
- Namespace Isolation: Services are partitioned into logic-based folders (GithubServicesandFiles, NonGitServices) to ensure a clean, maintainable codebase.
______
🤖 Managed & Documented by Gemini
This README and the underlying agent logic are co-developed by Klay and Gemini to push the boundaries of local AI orchestration.