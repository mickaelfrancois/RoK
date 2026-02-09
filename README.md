[🇬🇧 English](README.md) | [🇫🇷 Français](README.fr.md)

# 🎵 Rok

**Rok** is a modern music player for Windows, built with the latest Microsoft technologies.

## 📖 About

Rok is a Windows desktop application for managing and playing your local music collection. Developed with .NET 10 and WinUI 3, it offers a fluid and modern interface that seamlessly integrates with Windows 11.

## ✨ Features

- 🎵 **Audio Playback** - Complete player with queue management
- 📚 **Library Management** - Browse by albums, artists, genres, and playlists
- 🔍 **Search** - Quick search across your entire collection
- ✏️ **Metadata Editing** - Modify tags, covers, and track information
- 📝 **Playlists** - Create and manage custom playlists
- 🎮 **Discord Integration** - Display your current listening activity on Discord
- 🌓 **Themes** - Light and dark mode support
- 🎯 **Compact Mode** - Minimal player view

## 🛠️ Technologies

### Tech Stack

- **.NET 10.0** - Modern and performant framework
- **C# 13.0** - Latest language features
- **WinUI 3** - Native Windows UI framework
- **Windows App SDK 1.8** - Windows platform APIs
- **SQLite** - Local database
- **Dapper** - High-performance micro-ORM
- **TagLibSharp** - Audio metadata reading/writing
- **Serilog** - Structured logging

### Architecture

The project follows **Clean Architecture** principles with clear separation of concerns:

#### Layers and Responsibilities

**🎨 Presentation**
- WinUI 3 user interface with XAML
- ViewModels implementing MVVM pattern
- Two-way data binding
- Page navigation
- Theme and style management

**💼 Application (Core/Rok.Application)**
- Business use cases (CQRS Commands and Queries)
- Application logic orchestration
- DTOs for data transfer
- Messages for decoupled communication (Mediator Pattern)
- Service interfaces

**🏛️ Domain (Core/Rok.Domain)**
- Business entities (Album, Artist, Track, Playlist, Genre)
- Business rules and validations
- Repository interfaces
- Infrastructure-independent domain model

**📦 Infrastructure (Infrastructure/Rok.Infrastructure)**
- Repository implementation with Dapper
- SQLite database access
- Metadata reading/writing with TagLibSharp
- File and image services
- Logging with Serilog
- Discord integration

**Design Patterns:**
- **MVVM** (Model-View-ViewModel) - UI/logic separation
- **CQRS** (Command Query Responsibility Segregation) - Read/write separation
- **Dependency Injection** - Inversion of control with Microsoft.Extensions.DependencyInjection
- **Mediator Pattern** - Decoupled component communication
- **Repository Pattern** - Data access abstraction
- **Unit of Work** - Transactional operation management

## 📋 Requirements

- Windows 10 version 1809 (build 17763) or higher
- Windows 11 recommended for optimal experience

## 📧 Contact

Mickaël François - [@mickaelfrancois](https://github.com/mickaelfrancois)

⭐ If you like this project, feel free to give it a star on GitHub!