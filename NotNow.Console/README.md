# NotNow Console Application

A command-line interface for managing GitHub issues with integrated NotNow command processing for task tracking, time management, and subtask organization.

## Setup

### Prerequisites

- .NET 9.0 SDK or later
- GitHub Personal Access Token (PAT)
- Access to a GitHub repository

### Configuration

1. **Copy the example configuration file:**
   ```bash
   cp appsettings.example.json appsettings.json
   ```

2. **Edit `appsettings.json` with your GitHub settings:**
   ```json
   {
     "GitHubSettings": {
       "PersonalAccessToken": "ghp_YOUR_GITHUB_PAT_HERE",
       "RepositoryUrl": "https://github.com/owner/repository",
       "Owner": "repository-owner",
       "Repository": "repository-name"
     }
   }
   ```

### Creating a GitHub Personal Access Token

1. Go to GitHub → Settings → Developer settings → Personal access tokens
2. Click "Generate new token" (classic)
3. Select the following scopes:
   - `repo` (for private repositories)
   - `public_repo` (for public repositories only)
4. Generate the token and save it securely
5. Add the token to your `appsettings.json`

⚠️ **Security Note:** Never commit your `appsettings.json` with your PAT to source control. The file is already in `.gitignore`.

## Running the Application

```bash
# Build the solution
dotnet build

# Run the console application
cd NotNow.Console
dotnet run
```

## Using the Console

### Main Menu Options

When you start the application, you'll see the main menu with the following options:

1. **List Issues** - Browse GitHub issues with pagination
   - Filter by: Open, Closed, or All issues
   - Navigate with N (next), P (previous), Q (quit)

2. **Select Current Issue** - Choose an issue to work with
   - Required before using command mode or adding comments
   - Shows first 20 issues from loaded list

3. **View Current Issue Comments** - Display all comments on selected issue
   - Shows comment author and timestamp
   - Displays full comment text

4. **Add Comment to Current Issue** - Post a new comment
   - Type your comment (multi-line supported)
   - Type `END` on a new line to finish
   - Can include `/notnow` commands that will be processed

5. **Create New Issue** - Create a new GitHub issue
   - Option to initialize with NotNow tracking
   - Configure type, priority, assignee, estimate, due date, tags
   - Add subtasks with estimates

6. **Enter Command Mode** - Interactive `/notnow` command entry
   - Tab completion for commands
   - Real-time command execution
   - Results posted to GitHub (only if successful)

7. **Show Command Help** - Display available commands and syntax

8. **Exit** - Close the application

### Command Mode Features

When in command mode (option 6), you can use NotNow commands to manage tasks:

#### Available Commands

- **status** - Update issue status (todo, in_progress, done)
- **assign** - Assign issue to a user
- **due** - Set due date
- **estimate** - Set time estimate
- **tags** - Add/manage tags
- **priority** - Set priority level
- **time** - Log time spent
- **start** - Start a work session
- **stop** - Stop current work session
- **session** - View session information
- **timespent** - View total time spent
- **subtask** - Manage subtasks
- **complete** - Mark as complete
- **reopen** - Reopen completed issue

#### Command Examples

```bash
# Update status
/notnow status in_progress

# Assign to a user
/notnow assign @username

# Set estimate
/notnow estimate 4h30m

# Log time with description
/notnow time 2h --description "Implemented feature X"

# Add a subtask
/notnow subtask add "Write unit tests" --estimate 2h

# Start work session with description
/notnow start -d "Working on API integration"

# Stop work session
/notnow stop
```

### Keyboard Shortcuts

- **Tab** - Auto-complete commands in command mode
- **Escape** - Clear current input
- **Backspace** - Delete last character
- **Enter** - Execute command

### Command Auto-Completion

- Press **Tab** to see available commands or parameters
- If only one suggestion matches, it auto-completes
- All matching suggestions are displayed below the input line
- Continue typing to filter suggestions

### Error Handling

- Invalid commands are not posted to GitHub
- Error messages are displayed in red
- Success messages are displayed in green
- Command execution results show detailed data when available

## Features

### NotNow Command Processing
- Commands embedded in comments are automatically processed
- Command results are posted back to GitHub as formatted comments
- Failed commands show error messages but don't block comment posting

### GitHub Integration
- Full CRUD operations for issues
- Comment management
- Real-time synchronization with GitHub
- Support for labels and assignees

### Session Tracking
- Start/stop work sessions
- Automatic duration calculation
- Session data included in command results

### Subtask Management
- Create subtasks with individual estimates
- Track subtask completion
- Hierarchical task organization

## Troubleshooting

### Common Issues

1. **"No issue selected" error**
   - Select an issue using option 2 from the main menu

2. **Auto-completion display issues**
   - Ensure your console window is tall enough (at least 10 lines)
   - The application will automatically clear the screen if too close to bottom

3. **GitHub API errors**
   - Verify your PAT has the correct permissions
   - Check that the repository owner and name are correct
   - Ensure your PAT hasn't expired

4. **Commands not posting to GitHub**
   - Only successful commands are posted
   - Check for error messages in red
   - Verify GitHub connection settings

### Requirements

- Console window should be at least 80 characters wide
- Minimum 10 lines tall for proper suggestion display
- Terminal with UTF-8 support for special characters (✓, ✗, →)

## Tips

- Use Tab completion liberally - it shows all available options
- Start with `help` command in command mode to see all options
- Commands can be chained in a single comment
- Use the `/notnow init` command when creating issues to set up tracking