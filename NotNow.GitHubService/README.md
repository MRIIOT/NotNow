# NotNow.GitHubService

A .NET library for interacting with GitHub Issues API, providing functionality to manage issues and comments for a specific repository.

## Features

- Retrieve issues from a repository (with filtering by state)
- Retrieve comments for a specific issue
- Create new issues with labels and assignees
- Add comments to existing issues

## Installation

Add the NotNow.GitHubService project reference to your application:

```xml
<ProjectReference Include="..\NotNow.GitHubService\NotNow.GitHubService.csproj" />
```

## Configuration

### Using appsettings.json

Add the following configuration to your `appsettings.json`:

```json
{
  "GitHubSettings": {
    "PersonalAccessToken": "your-github-pat-here",
    "RepositoryUrl": "https://github.com/owner/repository",
    "Owner": "repository-owner",
    "Repository": "repository-name"
  }
}
```

### Dependency Injection Setup

In your `Program.cs` or `Startup.cs`:

```csharp
using NotNow.GitHubService.Extensions;

// Using configuration from appsettings.json
builder.Services.AddGitHubService(builder.Configuration);

// Or using inline configuration
builder.Services.AddGitHubService(options =>
{
    options.PersonalAccessToken = "your-pat";
    options.Owner = "owner";
    options.Repository = "repo";
    options.RepositoryUrl = "https://github.com/owner/repo";
});
```

## Usage

```csharp
using NotNow.GitHubService.Interfaces;
using Octokit;

public class YourService
{
    private readonly IGitHubService _gitHubService;

    public YourService(IGitHubService gitHubService)
    {
        _gitHubService = gitHubService;
    }

    public async Task ExampleUsage()
    {
        // Get all open issues
        var openIssues = await _gitHubService.GetIssuesAsync(ItemStateFilter.Open);

        // Get all issues (open and closed)
        var allIssues = await _gitHubService.GetIssuesAsync(ItemStateFilter.All);

        // Get comments for issue #123
        var comments = await _gitHubService.GetIssueCommentsAsync(123);

        // Create a new issue
        var newIssue = await _gitHubService.CreateIssueAsync(
            "Bug: Something is broken",
            "Detailed description of the issue",
            new[] { "bug", "priority-high" },
            new[] { "username1", "username2" }
        );

        // Add a comment to issue #123
        var comment = await _gitHubService.AddCommentToIssueAsync(123, "This has been fixed in PR #456");
    }
}
```

## Creating a GitHub Personal Access Token

1. Go to GitHub Settings > Developer settings > Personal access tokens
2. Click "Generate new token" (classic)
3. Select the following scopes:
   - `repo` (for private repositories)
   - `public_repo` (for public repositories only)
4. Generate the token and save it securely
5. Add the token to your `appsettings.json` or use environment variables/secrets management

## Security Note

Never commit your Personal Access Token to source control. Consider using:
- Environment variables
- User secrets (for development)
- Azure Key Vault or similar services (for production)