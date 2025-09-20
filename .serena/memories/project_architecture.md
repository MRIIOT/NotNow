# NotNow Project Architecture

## Solution Structure

```
NotNow.sln
├── NotNow.Console (Executable)
│   ├── Program.cs                    # Main entry point, menu system
│   ├── README.md                     # Usage documentation
│   ├── appsettings.json             # Configuration (git-ignored)
│   └── appsettings.example.json     # Configuration template
│
├── NotNow.Core (Class Library)
│   ├── Commands/
│   │   ├── Framework/               # Command infrastructure
│   │   │   ├── ICommandHandler.cs   # Handler interface
│   │   │   ├── ICommandModule.cs    # Module interface
│   │   │   └── CommandSchema.cs     # Command structure definition
│   │   ├── Modules/                 # Command implementations
│   │   │   ├── CoreModule.cs        # Core commands registration
│   │   │   ├── Core/                # Core command handlers
│   │   │   │   ├── InitCommandHandler.cs
│   │   │   │   ├── StatusCommandHandler.cs
│   │   │   │   ├── AssignCommandHandler.cs
│   │   │   │   ├── CommentCommandHandler.cs
│   │   │   │   └── OtherHandlers.cs
│   │   │   ├── SubtaskModule.cs     # Subtask commands registration
│   │   │   ├── Subtasks/            # Subtask handlers
│   │   │   │   └── SubtaskHandlers.cs
│   │   │   ├── TimeTrackingModule.cs # Time tracking registration
│   │   │   └── TimeTracking/        # Time tracking handlers
│   │   │       └── TimeTrackingHandlers.cs
│   │   ├── Parser/                  # Command parsing
│   │   │   ├── ICommandParser.cs
│   │   │   └── ModularCommandParser.cs
│   │   ├── Registry/                # Command registration
│   │   │   ├── ICommandRegistry.cs
│   │   │   └── CommandRegistry.cs
│   │   └── Execution/               # Command execution
│   │       ├── ICommandExecutor.cs
│   │       └── CommandExecutor.cs
│   ├── Console/                     # Console utilities
│   │   ├── CommandAutoCompleter.cs  # Tab completion logic
│   │   ├── CommandInputWithHints.cs # Input with suggestions
│   │   ├── InteractiveCommandInput.cs # Interactive mode
│   │   └── SimpleCommandInput.cs    # Basic input handling
│   ├── Models/                      # Data models
│   │   └── IssueState.cs           # Issue state representation
│   ├── Services/                    # Business services
│   │   ├── IssueStateParser.cs     # Parse commands from GitHub
│   │   ├── IssueStateService.cs    # State management
│   │   ├── CommandPostingService.cs # Post commands to GitHub
│   │   └── CommandInitializationService.cs
│   └── Extensions/
│       └── ServiceCollectionExtensions.cs # DI registration
│
└── NotNow.GitHubService (Class Library)
    ├── Interfaces/
    │   └── IGitHubService.cs       # GitHub service contract
    ├── Services/
    │   └── GitHubService.cs        # Octokit implementation
    └── Extensions/
        └── ServiceCollectionExtensions.cs

```

## Key Design Patterns

### 1. Command Pattern
- Each command has a dedicated handler implementing `ICommandHandler`
- Commands are grouped into modules implementing `ICommandModule`
- Dynamic command discovery and registration at startup

### 2. Dependency Injection
- Constructor injection throughout
- Service registration in `ServiceCollectionExtensions`
- Scoped and Singleton lifetimes appropriately used

### 3. Module Pattern
- Commands organized into logical modules (Core, Subtasks, TimeTracking)
- Each module self-registers its commands
- Modules can be independently added/removed

### 4. Repository Pattern (Implicit)
- `IGitHubService` abstracts GitHub operations
- Services layer abstracts business logic from presentation

### 5. Parser Pattern
- `ModularCommandParser` handles command text parsing
- Regex-based extraction of commands from text
- Support for command chaining

## Data Flow

1. **User Input** → Console Application
2. **Command Entry** → CommandInputWithHints (autocomplete)
3. **Command Parsing** → ModularCommandParser
4. **Command Execution** → CommandExecutor → CommandHandler
5. **State Management** → IssueStateService
6. **GitHub API** → GitHubService → Octokit
7. **Response** → Console Output

## Service Lifetimes

- **Singleton Services:**
  - `ICommandRegistry` - Command definitions
  - `ICommandParser` - Parsing logic
  - `ICommandExecutor` - Execution engine
  - `IIssueStateService` - Shared state
  - `CommandAutoCompleter` - Autocomplete logic

- **Scoped Services:**
  - `IGitHubService` - GitHub API client
  - `ICommandPostingService` - Command posting

- **Transient:**
  - Command handlers - Created per execution

## Extension Points

### Adding New Commands
1. Create handler class inheriting from `CommandHandler<TArgs>`
2. Add registration to appropriate module
3. Define command schema (parameters, options)
4. Handler automatically discovered at startup

### Adding New Modules
1. Create class implementing `ICommandModule`
2. Register commands in `GetCommands()`
3. Module automatically loaded via reflection

### Extending State
1. Add properties to `IssueState` model
2. Update `IssueStateParser` to extract new data
3. State automatically persisted via `IssueStateService`

## Configuration

### appsettings.json Structure
```json
{
  "GitHubSettings": {
    "PersonalAccessToken": "ghp_...",
    "Owner": "repository-owner",
    "Repository": "repository-name"
  }
}
```

### Environment-Specific Settings
- Development: Local testing configuration
- Production: Actual GitHub repository settings

## Error Handling Strategy

1. **Command Level**: Return `CommandResult.Failure()` with message
2. **Service Level**: Throw exceptions for unexpected errors
3. **API Level**: Catch and wrap Octokit exceptions
4. **Console Level**: Display friendly error messages

## Security Considerations

- PAT stored in local config (git-ignored)
- No credentials in source code
- Input validation in command handlers
- Rate limiting respect for GitHub API