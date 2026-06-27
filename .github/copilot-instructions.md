# Copilot Instructions

## Architecture & Framework
- **Framework**: WinUI 3 (.NET 8) with MVVM using CommunityToolkit.MVVM
- **Pattern**: Observable properties via `[ObservableProperty]` with partial methods for change notifications
- **Async First**: All I/O operations use async/await; use `Task.Run()` for expensive CPU work on background threads

## General Instructions
- Make only high confidence suggestions when reviewing code changes.
- Write code with good maintainability practices, including comments on why certain design decisions are made.
- Handle edge cases and write clear exception handling.
- For libraries or external dependencies, mention their usage and purpose in comments.

## Naming Conventions

- Follow PascalCase for component names, method names, and public members.
- Use camelCase for private fields and local variables.
- Prefix interface names with "I" (e.g. IUserService).

## Build Commands

### Restore Dependencies
```powershell
dotnet restore
```

### Build Solution
```powershell
dotnet build
```

### Run WinUI Application (x64)
```powershell
dotnet run --project .\src\HNReader.WinUI\HNReader.WinUI.csproj -r win-x64
```

### Run WinUI Application (Debug)
```powershell
dotnet run --project .\src\HNReader.WinUI\HNReader.WinUI.csproj 
```

### Run Tests
```powershell
dotnet test
```

## Project-Specific Rules
- Always run `dotnet restore` before building after dependency changes.
- For development, use debug configuration; for distribution, ensure x64 release builds.
- This project has no AI/ML/LLM dependencies — keep it that way. All features are local and deterministic.
- The app talks only to `news.ycombinator.com` (Firebase API + website). No third-party services.
- Local persistence is limited to LiteDB (`favorites.db`) and a JSON file (`settings.json`) under `%LOCALAPPDATA%\HNReader\`.
