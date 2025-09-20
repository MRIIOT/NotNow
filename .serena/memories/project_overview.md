# NotNow Project Overview

## Purpose
NotNow is a GitHub issue management system with integrated command processing for task tracking, time management, and subtask organization. It provides a command-line interface for managing GitHub issues through special `/notnow` commands that can be embedded in issue bodies and comments.

## Architecture
The solution consists of three main projects:
1. **NotNow.Console** - Command-line interface application for interacting with GitHub issues
2. **NotNow.Core** - Core business logic, command processing framework, and services
3. **NotNow.GitHubService** - GitHub API integration using Octokit

## Key Features
- GitHub issue management with CRUD operations
- Command processing framework with modular architecture
- Interactive console with tab-completion support
- Work session tracking and time management
- Subtask management with dependencies
- State parsing and persistence for issues
- Rich comment formatting with markdown support

## Tech Stack
- **.NET 9.0** - Target framework
- **C# 13** with nullable reference types enabled
- **Octokit** (14.0.0) - GitHub API client
- **Microsoft.Extensions.DependencyInjection** - Dependency injection
- **Microsoft.Extensions.Configuration** - Configuration management
- **Microsoft.Extensions.Hosting** - Application hosting

## Project Structure
```
NotNow/
├── NotNow.Console/       # CLI application
│   ├── Program.cs        # Main entry point
│   └── README.md         # Console usage documentation
├── NotNow.Core/          # Core functionality
│   ├── Commands/         # Command framework
│   │   ├── Framework/    # Command interfaces and base classes
│   │   ├── Modules/      # Command modules (Core, TimeTracking, Subtasks)
│   │   ├── Parser/       # Command parsing
│   │   └── Registry/     # Command registration
│   ├── Console/          # Console utilities (autocomplete, input handling)
│   ├── Models/           # Data models
│   └── Services/         # Business services
└── NotNow.GitHubService/ # GitHub integration
```

## Configuration
The application uses `appsettings.json` for configuration:
- GitHub Personal Access Token
- Repository Owner
- Repository Name

Example configuration is provided in `appsettings.example.json`.