# GitHub Copilot Instructions for Rok

## General Guidelines
- **All code and comments MUST be written in English**
- **Comments are only added when strictly necessary to understand a specific line of code**
- **All code MUST compile without warnings**
- Avoid obvious or redundant comments
- Self-documenting code is preferred over comments
- Respect clean code and clean architecture
- ViewModels must not reference UI elements
- Use dependency injection via Microsoft.Extensions.DependencyInjection.

## Code Language and Documentation
- The user prefers scripting languages like Bash over PowerShell. Git Bash is used on Windows for scripting, and Bash is functional in their environment.

## Project Overview
Rok is a .NET 10 application using C# 13.0 with WinUi3 for listening local music and content management (albums, artists, tracks).

## Architecture
- **Presentation Layer**: MVVM ViewModels with CommunityToolkit.Mvvm
- **Application Layer**: Business logic and services
- **Domain Layer**: Core entities and domain logic
- **Import Layer**: Importing music from the file system
- **Shared Layer**: Shared utilities and models
- **Infrastructure Layer**: Data access, telemetry
- **Testing**: Unit tests for all layers, integration tests for critical paths

## Code Standards

### General Conventions
- **Framework**: .NET 10, C# 13.0
- **Patterns**: MVVM with `ObservableObject`, dependency injection
- **Collections**: Never use collection expressions (see `.editorconfig`)					
- **Warnings**: Code must compile with zero warnings

### Naming Conventions
- Follow .editorconfig strictly.
- Use PascalCase for classes, records, methods, public properties.
- Use camelCase for private fields and local variables.
- Use var only when the type is evident from the right-hand side (e.g., new, casts, literals).
- Place braces on their own line.
- Add blank lines before/after conditions, loops, and logical blocks.
- Prefer early return.
- Avoid regions.

## Patterns to Prefer
- Use async/await everywhere.
- Use IOptions<T> for configuration.
- Use records for immutable models.
- Use interfaces for all services.
- Use factory patterns for audio backends.

## Code Quality
- All code must pass: dotnet format --severity info
- All code must pass analyzers: CAxxxx, IDExxxx, StyleCop (if enabled)
- Generate code that compiles without warnings.

## Git Rules
- All commits must follow Conventional Commits.
- Breaking changes require a “BREAKING CHANGE:” section.

## Documentation
- Add XML comments for public APIs.
- Add summary comments for complex methods. 

### Architecture Patterns

### Player engine
- The player engine used is `NAudio` for audio playback and manipulation.

## What NOT to generate
- No obsolete APIs.
- No synchronous I/O.
- No static service locators.
- No code-behind logic in WinUI pages if it can be avoided.

# Available Skills
Here is a list of skills that contain domain specific knowledge on a variety of topics.
Each skill comes with a description of the topic and a file path that contains the detailed instructions.
When a user asks you to perform a task that falls within the domain of a skill, use the get_file tool to acquire the full instructions from the file URI.

- **git-commit-messages**: Rules and patterns for writing standard Git commit messages following the Conventional Commits specification. Activate when the user asks to write
  - File: `D:\Development\MF\Rok\.github\skills\git-commit-messages\SKILL.md`

- **msbuild-antipatterns**: Catalog of MSBuild anti-patterns with detection rules and fix recipes. Only activate in MSBuild/.NET build context. USE FOR: reviewing
  - File: `D:\Development\MF\Rok\.github\skills\msbuild-antipatterns\SKILL.md`
