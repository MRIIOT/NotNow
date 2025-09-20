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

When in command mode (option 6), you can use NotNow commands to manage tasks. Commands are automatically posted to GitHub when successful.

## Complete Command Reference

### Core Commands

#### `init` - Initialize NotNow tracking
**Context:** Issue body only
**Syntax:** `/notnow init [options]`
**Options:**
- `--type <type>` - Issue type (bug, feature, task)
- `--priority <level>` - Priority (low, medium, high, critical)
- `--workflow <name>` - Workflow to use

**Example:**
```bash
/notnow init --type feature --priority high
```

#### `status` - Update issue status
**Context:** Comments
**Syntax:** `/notnow status <status> [options]`
**Parameters:**
- `status` - New status (todo, in_progress, done, blocked)

**Options:**
- `--reason <text>` or `-r <text>` - Reason for status change

**Examples:**
```bash
/notnow status in_progress
/notnow status blocked --reason "Waiting for API access"
/notnow status done
```

#### `assign` - Assign issue to user
**Aliases:** assignee
**Context:** Comments
**Syntax:** `/notnow assign <username> [options]`
**Parameters:**
- `username` - GitHub username (with or without @)

**Options:**
- `--notify` or `-n` - Send notification

**Examples:**
```bash
/notnow assign @johndoe
/notnow assign alice --notify
```

#### `due` - Set due date
**Context:** Both issue body and comments
**Syntax:** `/notnow due <date>`
**Parameters:**
- `date` - Due date in YYYY-MM-DD format

**Examples:**
```bash
/notnow due 2025-12-31
/notnow due 2025-01-15
```

#### `estimate` - Set time estimate
**Context:** Both issue body and comments
**Syntax:** `/notnow estimate <duration>`
**Parameters:**
- `duration` - Time estimate (e.g., 2h, 4h30m, 3d)

**Examples:**
```bash
/notnow estimate 8h
/notnow estimate 2h30m
/notnow estimate 3d
```

#### `tags` - Manage tags
**Aliases:** tag
**Context:** Both issue body and comments
**Syntax:** `/notnow tags <action> <tags>`
**Parameters:**
- `action` - add or remove
- `tags` - Comma-separated list of tags

**Examples:**
```bash
/notnow tags add backend,api,urgent
/notnow tags remove wip
/notnow tag add documentation
```

#### `priority` - Set priority level
**Context:** Both issue body and comments
**Syntax:** `/notnow priority <level>`
**Parameters:**
- `level` - Priority (low, medium, high, critical)

**Examples:**
```bash
/notnow priority high
/notnow priority critical
```

#### `type` - Set issue type
**Context:** Both issue body and comments
**Syntax:** `/notnow type <type>`
**Parameters:**
- `type` - Issue type (bug, feature, task, enhancement)

**Examples:**
```bash
/notnow type bug
/notnow type feature
```

### Time Tracking Commands

#### `start` - Start work session
**Context:** Comments
**Syntax:** `/notnow start [options]`
**Options:**
- `--description <text>` or `-d <text>` - Session description

**Examples:**
```bash
/notnow start
/notnow start -d "Working on API integration"
/notnow start --description "Fixing authentication bug"
```

#### `stop` - Stop current work session
**Context:** Comments
**Syntax:** `/notnow stop`

**Example:**
```bash
/notnow stop
```

#### `time` - Log time spent
**Context:** Comments
**Syntax:** `/notnow time <duration> [options]`
**Parameters:**
- `duration` - Time spent (e.g., 2h, 1h30m)

**Options:**
- `--description <text>` or `-d <text>` - Work description
- `--date <date>` - Date of work (default: today)

**Examples:**
```bash
/notnow time 2h
/notnow time 1h30m --description "Code review and testing"
/notnow time 4h -d "Implementation" --date 2025-01-10
```

#### `session` - View session information
**Context:** Comments
**Syntax:** `/notnow session [options]`
**Options:**
- `--current` or `-c` - Show only current session
- `--list` or `-l` - List all sessions

**Examples:**
```bash
/notnow session
/notnow session --current
/notnow session --list
```

#### `timespent` - View total time spent
**Context:** Comments
**Syntax:** `/notnow timespent [options]`
**Options:**
- `--by-day` - Group by day
- `--by-user` - Group by user

**Examples:**
```bash
/notnow timespent
/notnow timespent --by-day
/notnow timespent --by-user
```

### Subtask Commands

#### `subtask` - Manage subtasks
**Aliases:** task
**Context:** Both issue body and comments
**Syntax:** `/notnow subtask <action> [title] [options]`
**Parameters:**
- `action` - Action to perform (add, complete, remove, list)
- `title` - Subtask title (for add action)

**Options:**
- `--id <id>` - Subtask ID
- `--estimate <duration>` - Time estimate
- `--depends <ids>` - Dependencies (comma-separated IDs)
- `--assignee <username>` - Assignee

**Examples:**
```bash
# Add subtasks
/notnow subtask add "Write unit tests" --estimate 2h
/notnow subtask add "Update documentation" --id doc1 --assignee @alice
/notnow subtask add "Deploy to staging" --depends doc1,test1

# List subtasks
/notnow subtask list

# Complete a subtask
/notnow subtask complete st1

# Remove a subtask
/notnow subtask remove st2
```

#### `complete` - Mark subtask as complete
**Aliases:** done, finish
**Context:** Comments
**Syntax:** `/notnow complete <id> [options]`
**Parameters:**
- `id` - Subtask ID to complete

**Options:**
- `--time <duration>` - Time spent
- `--notes <text>` - Completion notes

**Examples:**
```bash
/notnow complete st1
/notnow complete doc1 --time 1h30m
/notnow done test1 --notes "All tests passing"
```

#### `reopen` - Reopen completed subtask
**Context:** Comments
**Syntax:** `/notnow reopen <id> [options]`
**Parameters:**
- `id` - Subtask ID to reopen

**Options:**
- `--reason <text>` - Reason for reopening

**Examples:**
```bash
/notnow reopen st1
/notnow reopen test1 --reason "Found additional edge cases"
```

### Communication Commands

#### `comment` - Post a comment
**Aliases:** c, msg
**Context:** Comments
**Syntax:** `/notnow comment [message] [options]`
**Parameters:**
- `message` - Comment text

**Options:**
- `--body <text>` or `-b <text>` - Alternative to message parameter
- `--markdown` or `-m` - Format as markdown quote

**Examples:**
```bash
/notnow comment "This looks good to me"
/notnow c "Starting work on this now"
/notnow comment --body "Longer comment with multiple lines" --markdown
```

#### `note` - Add a formatted note
**Aliases:** n
**Context:** Comments
**Syntax:** `/notnow note <text> [options]`
**Parameters:**
- `text` - Note content

**Options:**
- `--title <text>` or `-t <text>` - Note title
- `--category <category>` or `-c <category>` - Note category

**Examples:**
```bash
/notnow note "Remember to update the documentation"
/notnow n "API needs authentication" --title "Security Note" --category security
/notnow note "Decision: Use PostgreSQL" --category decision
```

#### `update` - Post a status update
**Aliases:** u, progress
**Context:** Comments
**Syntax:** `/notnow update [message] [options]`
**Parameters:**
- `message` - Update message

**Options:**
- `--progress <percentage>` or `-p <percentage>` - Progress (0-100)
- `--blockers <list>` or `-b <list>` - Comma-separated blockers
- `--next <list>` or `-n <list>` - Comma-separated next steps

**Examples:**
```bash
/notnow update "Finished API integration" --progress 75
/notnow u --progress 50 --blockers "Waiting for API keys"
/notnow progress "On track" -p 80 --next "Write tests, Deploy"
```

### Command Context

- **IssueBody**: Commands that can only be used in the issue description
- **Comment**: Commands that can be used in comments
- **Both**: Commands that work in both contexts

### Auto-completion

Press **Tab** at any point to see available options:
- After `/notnow` - shows all available commands
- After command name - shows available parameters and options
- While typing - filters suggestions based on input

### Command Chaining

Multiple commands can be included in a single comment:

```bash
/notnow status in_progress
/notnow assign @alice
/notnow estimate 4h
/notnow subtask add "Review PR" --estimate 30m
```

### Error Handling

- Commands with errors are not posted to GitHub
- Error messages appear in red
- Success messages appear in green
- Command data is shown in gray

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