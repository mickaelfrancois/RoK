# GitHub Copilot Instructions for Rok

## Code Language and Documentation

**CRITICAL RULES:**
- **All code and comments MUST be written in English**
- **Comments are only added when strictly necessary to understand a specific line of code**
- **All code MUST compile without warnings**
- Avoid obvious or redundant comments
- Self-documenting code is preferred over comments
- Respect clean code and clean architecture

## Project Overview

Rok is a .NET 9 application using C# 13.0 with WinUi3 for listening local music and content management (albums, artists, tracks).

## Architecture

- **Presentation Layer**: MVVM ViewModels with CommunityToolkit.Mvvm
- **Logic Layer**: Business logic and services
- **Infrastructure Layer**: Data access, telemetry

## Code Standards

### General Conventions

- **Framework**: .NET 9, C# 13.0
- **Patterns**: MVVM with `ObservableObject`, dependency injection
- **Collections**: Never use collection expressions (see `.editorconfig`)
- **Warnings**: Code must compile with zero warnings

### Naming Conventions

- Classes: PascalCase
- Methods: PascalCase
- Local variables: camelCase
- Properties: PascalCase
- Events: PascalCase with "Changed" or "Completed" suffix

### Architecture Patterns

#### ViewModels