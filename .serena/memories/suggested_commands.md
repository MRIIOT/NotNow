# Suggested Commands for NotNow Development

## Build and Run Commands

### Building the Solution
```bash
# Build entire solution
dotnet build

# Build in Release mode
dotnet build -c Release

# Clean and rebuild
dotnet clean
dotnet build
```

### Running the Application
```bash
# Run the console application
cd NotNow.Console
dotnet run

# Run with specific configuration
dotnet run --configuration Release
```

## Development Commands

### Package Management
```bash
# Restore NuGet packages
dotnet restore

# Add a package to a project
dotnet add package <PackageName>

# Update packages
dotnet list package --outdated
```

### Project References
```bash
# Add project reference
dotnet add reference ../NotNow.Core/NotNow.Core.csproj

# List project references
dotnet list reference
```

## Testing Commands
*Note: No test projects currently exist in the solution*

```bash
# Create a test project (if needed)
dotnet new xunit -n NotNow.Tests
dotnet sln add NotNow.Tests/NotNow.Tests.csproj

# Run tests (when available)
dotnet test
```

## Code Quality Commands

### Code Formatting
```bash
# Format code (requires dotnet-format tool)
dotnet format

# Install format tool if not present
dotnet tool install -g dotnet-format
```

### Code Analysis
```bash
# Run code analysis
dotnet build /p:RunAnalyzers=true

# With specific analyzers
dotnet build /p:RunAnalyzers=true /p:RunAnalyzersDuringBuild=true
```

## Git Commands (Windows)

### Basic Git Operations
```bash
# Check status
git status

# Stage changes
git add .
git add <file>

# Commit
git commit -m "commit message"

# Push changes
git push origin main

# Pull latest
git pull origin main
```

### Branch Management
```bash
# Create and switch branch
git checkout -b feature/branch-name

# Switch branches
git checkout main
git checkout feature/branch-name

# Merge branch
git merge feature/branch-name
```

## Windows System Commands

### Directory Navigation
```bash
# List directory contents
dir
# or use PowerShell
ls

# Change directory
cd NotNow.Console
cd ..

# Create directory
mkdir NewFolder

# Remove directory
rmdir /s FolderName
```

### File Operations
```bash
# Copy file
copy source.txt dest.txt

# Move/Rename file
move oldname.txt newname.txt

# Delete file
del filename.txt

# View file contents
type filename.txt
# or in PowerShell
cat filename.txt
```

### Process Management
```bash
# List running processes
tasklist

# Kill a process
taskkill /F /IM dotnet.exe

# Check ports in use
netstat -an | findstr :5000
```

## PowerShell Specific Commands

```powershell
# Find files
Get-ChildItem -Recurse -Filter "*.cs"

# Search in files
Select-String -Path "*.cs" -Pattern "pattern"

# Environment variables
$env:PATH

# Check .NET version
dotnet --info
```

## Configuration Setup

### Initial Setup
```bash
# Copy example configuration
copy NotNow.Console\appsettings.example.json NotNow.Console\appsettings.json

# Edit configuration (opens in notepad)
notepad NotNow.Console\appsettings.json
```

## Debugging

### Using Visual Studio Code
```bash
# Generate launch.json and tasks.json
dotnet build

# Start debugging (in VS Code)
# Press F5 or use Debug menu
```

### Using Visual Studio
```bash
# Open solution
start NotNow.sln

# Or use devenv command
devenv NotNow.sln
```

### Command Line Debugging
```bash
# Run with verbose logging
dotnet run --verbosity detailed

# Run with specific environment
set ASPNETCORE_ENVIRONMENT=Development
dotnet run
```