## PortfolioAgentFlux
An intelligent C# Console application acting as a personal portfolio assistant. This agent uses local LLMs to interact with the GitHub API, providing a conversational interface for exploring developer activity and project details.

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
- [x] Integrate local Llama 3.2 model.
- [x] Implement GitHub tool calling (Repositories, READMEs, Commits).
- [x] Integrate Roslyn for deep code analysis and security scanning.
- [ ] Refactor into clean architecture (Service/Controller layers).
- [ ] Implement advanced filtering for repository analytics.

_________
## 🤖 Managed & Documented by Gemini
This README and the underlying agent logic are co-developed by Klay and Gemini to push the boundaries of local AI orchestration.