# Code Style and Conventions

## C# Coding Standards

### General Guidelines
- **Language Version**: C# 13 with .NET 9.0
- **Nullable Reference Types**: Enabled (`<Nullable>enable</Nullable>`)
- **Implicit Usings**: Enabled (`<ImplicitUsings>enable</ImplicitUsings>`)

### Naming Conventions
- **Classes/Interfaces**: PascalCase (e.g., `CommandHandler`, `ICommandExecutor`)
- **Interfaces**: Prefix with 'I' (e.g., `ICommandHandler`, `IIssueStateService`)
- **Methods**: PascalCase (e.g., `ExecuteAsync`, `GetCommands`)
- **Private Fields**: Underscore prefix with camelCase (e.g., `_serviceProvider`, `_gitHubService`)
- **Properties**: PascalCase (e.g., `IssueNumber`, `Status`)
- **Parameters/Variables**: camelCase (e.g., `context`, `args`, `issueState`)
- **Constants**: UPPER_CASE with underscores (when used)

### File Organization
- One primary type per file
- File name matches the primary type name
- Related small classes can be in the same file (e.g., command args and handlers)

### Async/Await Patterns
- All async methods suffixed with `Async`
- Use `Task` or `Task<T>` return types
- Use `async/await` throughout rather than `.Result` or `.Wait()`

### Dependency Injection
- Constructor injection pattern
- Use `IServiceProvider` when multiple services needed
- Register services in `ServiceCollectionExtensions`

### Command Pattern
- Commands inherit from `CommandHandler<TArgs>`
- Args classes inherit from `CommandArgs`
- Return `CommandResult.Ok()` or `CommandResult.Failure()`

### Error Handling
- Use try-catch for external operations (GitHub API, file I/O)
- Return descriptive error messages via `CommandResult.Failure()`
- Include original exception message when appropriate

### Comments and Documentation
- Minimal inline comments (code should be self-documenting)
- No XML documentation comments in current codebase
- Use descriptive variable and method names instead

### String Formatting
- Use string interpolation: `$"Message: {value}"`
- Use verbatim strings for multi-line: `@"multi
line"`
- Use `StringBuilder` for complex string building

### LINQ Usage
- Method syntax preferred over query syntax
- Use LINQ for collections: `.FirstOrDefault()`, `.Any()`, `.Select()`

### Null Handling
- Leverage nullable reference types
- Use null-conditional operators: `?.` and `??`
- Check for null/empty strings: `string.IsNullOrEmpty()`

### Collection Types
- Use `List<T>` for mutable collections
- Use `IEnumerable<T>` for method returns when appropriate
- Initialize collections in declarations: `= new List<T>()`

### Pattern Matching
- Use switch expressions where appropriate
- Use pattern matching for type checking and casting

### File Paths
- Always use forward slashes or `Path.Combine()`
- Store paths as strings, not `FileInfo`/`DirectoryInfo`