# Copilot Instructions

## Architecture & Framework
- **Framework**: WinUI 3 (.NET 8) with MVVM using CommunityToolkit.MVVM
- **Pattern**: Observable properties via `[ObservableProperty]` with partial methods for change notifications
- **Async First**: All I/O operations use async/await; use `Task.Run()` for expensive CPU work on background threads

## General Instructions
- Make only high confidence suggestions when reviewing code changes.
- Write code with good maintainability practices, including comments on why certain design decisions were made.
- Handle edge cases and write clear exception handling.
- For libraries or external dependencies, mention their usage and purpose in comments.

## Naming Conventions

- Follow PascalCase for component names, method names, and public members.
- Use camelCase for private fields and local variables.
- Prefix interface names with "I" (e.g., IUserService).

## Project-Specific Rules
- Use web-scraped comments (WebCommentNode) exclusively.
- Remove unused legacy API-based Comment/CommentNode classes and the API comment loading infrastructure.