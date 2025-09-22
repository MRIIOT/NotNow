using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NotNow.Core.Commands.Execution;
using NotNow.Core.Commands.Framework;
using NotNow.Core.Commands.Registry;
using NotNow.Core.Console;
using NotNow.Core.Extensions;
using NotNow.Core.Models;
using NotNow.Core.Services;
using NotNow.GitHubService.Extensions;
using NotNow.GitHubService.Interfaces;
using Octokit;

class Program
{
    private static IGitHubService? _gitHubService;
    private static ICommandExecutor? _commandExecutor;
    private static ICommandRegistry? _commandRegistry;
    private static ICommandAutoCompleter? _autoCompleter;
    private static NotNow.Core.Services.ICommandPostingService? _commandPoster;
    private static IIssueStateParser? _issueStateParser;
    private static IIssueStateService? _issueStateService;
    private static Issue? _currentIssue;
    private static IssueState? _currentIssueState;
    private static List<Issue> _issues = new();

    static async Task Main(string[] args)
    {
        var host = CreateHostBuilder(args).Build();
        _gitHubService = host.Services.GetRequiredService<IGitHubService>();
        _commandExecutor = host.Services.GetRequiredService<ICommandExecutor>();
        _commandRegistry = host.Services.GetRequiredService<ICommandRegistry>();
        _autoCompleter = host.Services.GetRequiredService<ICommandAutoCompleter>();
        _commandPoster = host.Services.GetRequiredService<NotNow.Core.Services.ICommandPostingService>();
        _issueStateParser = host.Services.GetRequiredService<IIssueStateParser>();
        _issueStateService = host.Services.GetRequiredService<IIssueStateService>();

        // Initialize command registry with modules
        var initService = host.Services.GetRequiredService<NotNow.Core.Services.ICommandInitializationService>();
        initService.Initialize();

        await RunInteractiveConsole();
    }

    static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
#if DEBUG
                // In Debug mode, also load Development configuration which will override base settings
                config.AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true);
#endif
            })
            .ConfigureServices((context, services) =>
            {
                services.AddGitHubService(context.Configuration);
                services.AddNotNowCore(options =>
                {
                    options.RegisterCoreModules = true;
                });
                services.AddScoped<ICommandAutoCompleter, CommandAutoCompleter>();
            });

    static async Task RunInteractiveConsole()
    {
        Console.Clear();
        Console.WriteLine("========================================");
        Console.WriteLine("     NotNow GitHub Issue Manager");
        Console.WriteLine("========================================");

        while (true)
        {
            ShowMainMenu();
            var choice = Console.ReadLine()?.Trim();

            try
            {
                switch (choice)
                {
                    case "1":
                        await ListIssues();
                        break;
                    case "2":
                        await SelectCurrentIssue();
                        break;
                    case "3":
                        await ViewCurrentIssueComments();
                        break;
                    case "4":
                        await AddCommentToCurrentIssue();
                        break;
                    case "5":
                        await CreateNewIssue();
                        break;
                    case "6":
                        await EnterCommandMode();
                        break;
                    case "7":
                        ShowCommandHelp();
                        break;
                    case "8":
                        Console.WriteLine("\nGoodbye!");
                        return;
                    default:
                        Console.WriteLine("\nInvalid option. Please try again.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError: {ex.Message}");
            }

            if (choice != "8")
            {
                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
            }
        }
    }

    static void ShowMainMenu()
    {
        Console.Clear();
        Console.WriteLine("========================================");
        Console.WriteLine("            Main Menu");
        Console.WriteLine("========================================");

        if (_currentIssue != null && _currentIssueState != null)
        {
            Console.WriteLine($"\nCurrent Issue: #{_currentIssue.Number} - {_currentIssue.Title}");
            Console.WriteLine($"GitHub State: {_currentIssue.State}");

            // Display parsed NotNow state
            Console.WriteLine("\n--- NotNow Status ---");
            if (_currentIssueState.IsInitialized)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✓ NotNow Tracking Active");
                Console.ResetColor();

                // Status and Priority
                Console.Write($"Status: ");
                SetStatusColor(_currentIssueState.Status);
                Console.Write(_currentIssueState.Status);
                Console.ResetColor();
                Console.Write($" | Priority: ");
                SetPriorityColor(_currentIssueState.Priority);
                Console.WriteLine(_currentIssueState.Priority);
                Console.ResetColor();

                // Type and Assignee
                if (_currentIssueState.Type != null || _currentIssueState.Assignee != null)
                {
                    if (_currentIssueState.Type != null)
                        Console.Write($"Type: {_currentIssueState.Type}");
                    if (_currentIssueState.Type != null && _currentIssueState.Assignee != null)
                        Console.Write(" | ");
                    if (_currentIssueState.Assignee != null)
                        Console.Write($"Assignee: @{_currentIssueState.Assignee}");
                    Console.WriteLine();
                }

                // Estimate and Due Date
                if (_currentIssueState.Estimate != null || _currentIssueState.DueDate != null)
                {
                    if (_currentIssueState.Estimate != null)
                        Console.Write($"Estimate: {_currentIssueState.Estimate}");
                    if (_currentIssueState.Estimate != null && _currentIssueState.DueDate != null)
                        Console.Write(" | ");
                    if (_currentIssueState.DueDate != null)
                    {
                        var daysUntil = (_currentIssueState.DueDate.Value - DateTime.Now).Days;
                        Console.Write($"Due: {_currentIssueState.DueDate:yyyy-MM-dd}");
                        if (daysUntil < 0)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.Write($" (OVERDUE by {Math.Abs(daysUntil)} days)");
                        }
                        else if (daysUntil <= 3)
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.Write($" ({daysUntil} days)");
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Gray;
                            Console.Write($" ({daysUntil} days)");
                        }
                        Console.ResetColor();
                    }
                    Console.WriteLine();
                }

                // Tags
                if (_currentIssueState.Tags.Any())
                {
                    Console.Write("Tags: ");
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine(string.Join(", ", _currentIssueState.Tags));
                    Console.ResetColor();
                }

                // Time Tracking
                if (_currentIssueState.TotalTimeSpent > TimeSpan.Zero || _currentIssueState.ActiveSession != null)
                {
                    Console.Write($"Time Spent: {FormatTimeSpan(_currentIssueState.TotalTimeSpent)}");
                    if (_currentIssueState.ActiveSession != null)
                    {
                        var currentDuration = DateTime.UtcNow - _currentIssueState.ActiveSession.StartedAt;
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write($" (+ {FormatTimeSpan(currentDuration)} ACTIVE)");
                        Console.ResetColor();
                    }
                    Console.WriteLine();
                }

                // Subtasks
                if (_currentIssueState.Subtasks.Any())
                {
                    var completed = _currentIssueState.Subtasks.Count(s => s.Status == "done");
                    var total = _currentIssueState.Subtasks.Count;
                    Console.Write($"Subtasks: {completed}/{total} completed");
                    if (completed == total)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write(" ✓");
                    }
                    Console.ResetColor();
                    Console.WriteLine();
                }
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("(NotNow tracking not initialized)");
                Console.ResetColor();
            }
            Console.WriteLine("----------------------------------------");
        }
        else if (_currentIssue != null)
        {
            Console.WriteLine($"\nCurrent Issue: #{_currentIssue.Number} - {_currentIssue.Title}");
            Console.WriteLine($"State: {_currentIssue.State}");
            Console.WriteLine("(Loading issue state...)");
            Console.WriteLine("----------------------------------------");
        }

        Console.WriteLine("\n1. List Issues");
        Console.WriteLine("2. Select Current Issue");
        Console.WriteLine("3. View Current Issue Comments");
        Console.WriteLine("4. Add Comment to Current Issue");
        Console.WriteLine("5. Create New Issue");
        Console.WriteLine("6. Enter Command Mode (/notnow)");
        Console.WriteLine("7. Show Command Help");
        Console.WriteLine("8. Exit");
        Console.Write("\nSelect an option: ");
    }

    static void SetStatusColor(string status)
    {
        switch (status?.ToLower())
        {
            case "done":
            case "completed":
                Console.ForegroundColor = ConsoleColor.Green;
                break;
            case "in_progress":
            case "in-progress":
                Console.ForegroundColor = ConsoleColor.Yellow;
                break;
            case "blocked":
                Console.ForegroundColor = ConsoleColor.Red;
                break;
            default:
                Console.ForegroundColor = ConsoleColor.Gray;
                break;
        }
    }

    static void SetPriorityColor(string priority)
    {
        switch (priority?.ToLower())
        {
            case "critical":
                Console.ForegroundColor = ConsoleColor.Red;
                break;
            case "high":
                Console.ForegroundColor = ConsoleColor.Magenta;
                break;
            case "medium":
                Console.ForegroundColor = ConsoleColor.Yellow;
                break;
            case "low":
                Console.ForegroundColor = ConsoleColor.Gray;
                break;
            default:
                Console.ForegroundColor = ConsoleColor.White;
                break;
        }
    }

    static string FormatTimeSpan(TimeSpan time)
    {
        if (time.TotalDays >= 1)
            return $"{(int)time.TotalDays}d {time.Hours}h {time.Minutes}m";
        else if (time.TotalHours >= 1)
            return $"{(int)time.TotalHours}h {time.Minutes}m";
        else
            return $"{time.Minutes}m";
    }

    static async Task EnterCommandMode()
    {
        if (_currentIssue == null)
        {
            Console.WriteLine("\nNo issue selected. Please select an issue first.");
            return;
        }

        Console.Clear();
        Console.WriteLine("========================================");
        Console.WriteLine("         Command Mode");
        Console.WriteLine("========================================");
        Console.WriteLine($"Issue: #{_currentIssue.Number} - {_currentIssue.Title}");
        Console.WriteLine("\nEnter /notnow commands. Type 'exit' to return to main menu.");
        Console.WriteLine("Type 'help' for available commands. Use Tab for auto-completion.\n");

        var inputHelper = new CommandInputWithHints(_autoCompleter!, CommandContext.Comment);

        while (true)
        {
            var input = inputHelper.ReadCommand("/notnow ");

            if (input.Equals("exit", StringComparison.OrdinalIgnoreCase))
                break;

            if (input.Equals("help", StringComparison.OrdinalIgnoreCase))
            {
                ShowAvailableCommands();
                continue;
            }

            if (string.IsNullOrWhiteSpace(input))
                continue;

            // Execute the command
            var context = new CommandExecutionContext
            {
                IssueNumber = _currentIssue.Number,
                User = "current-user", // In real app, get from auth
                Timestamp = DateTime.UtcNow,
                CommandContext = CommandContext.Comment
            };

            // Check if input already starts with /notnow (shouldn't happen with our prompt)
            var fullCommand = input.StartsWith("/notnow", StringComparison.OrdinalIgnoreCase)
                ? input
                : $"/notnow {input}";
            var result = await _commandExecutor!.ExecuteAsync(fullCommand, context);

            // Display results
            foreach (var commandResult in result.Results)
            {
                if (commandResult.Success)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"✓ {commandResult.Message}");

                    if (commandResult.Data != null)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.WriteLine($"  Data: {System.Text.Json.JsonSerializer.Serialize(commandResult.Data)}");
                    }
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"✗ {commandResult.Error}");
                }
                Console.ResetColor();
            }

            // Post the command to GitHub only if at least one command succeeded
            if (result.Results.Any(r => r.Success))
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("\nPosting command to GitHub...");
                Console.ResetColor();

                var posted = await _commandPoster!.PostCommandToGitHubAsync(
                    _currentIssue.Number,
                    fullCommand,
                    result);

                if (posted)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("✓ Command posted to GitHub");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("⚠ Failed to post command to GitHub (command still executed locally)");
                }
                Console.ResetColor();
            }
            else if (result.Results.Any())
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("\n(Command not posted to GitHub due to errors)");
                Console.ResetColor();
            }
        }
    }

    static void ShowCommandHelp()
    {
        Console.Clear();
        Console.WriteLine("========================================");
        Console.WriteLine("       NotNow Commands Help");
        Console.WriteLine("========================================\n");

        ShowAvailableCommands();
    }

    static void ShowAvailableCommands()
    {
        var commands = _commandRegistry!.GetCommandsForContext(CommandContext.Comment);

        Console.WriteLine("Available Commands:");
        Console.WriteLine("==================\n");

        foreach (var cmd in commands.OrderBy(c => c.Name))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"{cmd.Name}");

            if (cmd.Aliases != null && cmd.Aliases.Length > 0)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write($" (aliases: {string.Join(", ", cmd.Aliases)})");
            }

            Console.ResetColor();
            Console.WriteLine($"\n  {cmd.Description}");

            // Show parameters
            if (cmd.Schema.Parameters.Count > 0)
            {
                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.Write("  Parameters: ");
                foreach (var param in cmd.Schema.Parameters)
                {
                    var required = param.Required ? "*" : "";
                    Console.Write($"{param.Name}{required} ");
                }
                Console.WriteLine();
            }

            // Show options
            if (cmd.Schema.Options.Count > 0)
            {
                Console.ForegroundColor = ConsoleColor.DarkMagenta;
                Console.Write("  Options: ");
                foreach (var opt in cmd.Schema.Options)
                {
                    Console.Write($"--{opt.LongName} ");
                    if (!string.IsNullOrEmpty(opt.ShortName))
                        Console.Write($"(-{opt.ShortName}) ");
                }
                Console.WriteLine();
            }

            Console.ResetColor();
            Console.WriteLine();
        }

        Console.WriteLine("\nExamples:");
        Console.WriteLine("=========");
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.WriteLine("/notnow time 2h30m --description \"Fixed bug\"");
        Console.WriteLine("/notnow status in_progress");
        Console.WriteLine("/notnow assign @john");
        Console.WriteLine("/notnow subtask add \"Write unit tests\" --estimate 2h");
        Console.WriteLine("/notnow start -d \"Working on API\"");
        Console.ResetColor();
    }

    static async Task ListIssues()
    {
        Console.Clear();
        Console.WriteLine("========================================");
        Console.WriteLine("           Issue List");
        Console.WriteLine("========================================");

        Console.WriteLine("\nSelect issue state:");
        Console.WriteLine("1. Open Issues");
        Console.WriteLine("2. Closed Issues");
        Console.WriteLine("3. All Issues");
        Console.Write("\nChoice: ");

        var stateChoice = Console.ReadLine()?.Trim();
        var state = stateChoice switch
        {
            "1" => ItemStateFilter.Open,
            "2" => ItemStateFilter.Closed,
            "3" => ItemStateFilter.All,
            _ => ItemStateFilter.Open
        };

        Console.WriteLine("\nFetching issues...");
        _issues = (await _gitHubService!.GetIssuesAsync(state)).ToList();

        Console.Clear();
        Console.WriteLine("========================================");
        Console.WriteLine($"        Issues ({state})");
        Console.WriteLine("========================================\n");

        if (_issues.Count == 0)
        {
            Console.WriteLine("No issues found.");
        }
        else
        {
            var pageSize = 10;
            var currentPage = 0;
            var totalPages = (int)Math.Ceiling(_issues.Count / (double)pageSize);

            while (true)
            {
                Console.Clear();
                Console.WriteLine("========================================");
                Console.WriteLine($"   Issues ({state}) - Page {currentPage + 1}/{totalPages}");
                Console.WriteLine("========================================\n");

                var startIndex = currentPage * pageSize;
                var endIndex = Math.Min(startIndex + pageSize, _issues.Count);

                for (int i = startIndex; i < endIndex; i++)
                {
                    var issue = _issues[i];
                    Console.WriteLine($"#{issue.Number} - {issue.Title}");
                    Console.WriteLine($"   State: {issue.State} | Created: {issue.CreatedAt:yyyy-MM-dd}");
                    Console.WriteLine($"   {(string.IsNullOrEmpty(issue.Body) ? "No description" : issue.Body.Length > 50 ? issue.Body.Substring(0, 50) + "..." : issue.Body)}");
                    Console.WriteLine();
                }

                Console.WriteLine("----------------------------------------");
                Console.WriteLine("Navigation: [N]ext page, [P]revious page, [Q]uit");
                Console.Write("Choice: ");

                var nav = Console.ReadLine()?.Trim().ToUpper();
                if (nav == "Q")
                    break;
                else if (nav == "N" && currentPage < totalPages - 1)
                    currentPage++;
                else if (nav == "P" && currentPage > 0)
                    currentPage--;
            }
        }
    }

    static async Task SelectCurrentIssue()
    {
        Console.Clear();
        Console.WriteLine("========================================");
        Console.WriteLine("       Select Current Issue");
        Console.WriteLine("========================================");

        if (_issues.Count == 0)
        {
            Console.WriteLine("\nNo issues loaded. Fetching open issues...");
            _issues = (await _gitHubService!.GetIssuesAsync(ItemStateFilter.Open)).ToList();
        }

        if (_issues.Count == 0)
        {
            Console.WriteLine("\nNo issues available.");
            return;
        }

        Console.WriteLine("\nAvailable Issues:");
        foreach (var issue in _issues.Take(20))
        {
            Console.WriteLine($"#{issue.Number} - {issue.Title}");
        }

        Console.Write("\nEnter issue number to select (or 0 to cancel): ");
        if (int.TryParse(Console.ReadLine(), out int issueNumber))
        {
            if (issueNumber == 0)
            {
                Console.WriteLine("Selection cancelled.");
                return;
            }

            _currentIssue = _issues.FirstOrDefault(i => i.Number == issueNumber);
            if (_currentIssue != null)
            {
                Console.WriteLine($"\nIssue #{_currentIssue.Number} selected as current issue.");
                Console.WriteLine("\nLoading issue state and parsing commands...");

                try
                {
                    // Fetch all comments for the issue
                    var comments = await _gitHubService!.GetIssueCommentsAsync(_currentIssue.Number);

                    // Parse the issue state from all commands
                    _currentIssueState = _issueStateParser!.ParseIssueState(_currentIssue, comments);

                    // Store the state in the service
                    _issueStateService!.SetState(_currentIssue.Number, _currentIssueState);

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("✓ Issue state loaded successfully");

                    if (_currentIssueState.IsInitialized)
                    {
                        Console.WriteLine($"  Status: {_currentIssueState.Status}");
                        Console.WriteLine($"  Priority: {_currentIssueState.Priority}");
                        if (_currentIssueState.TotalTimeSpent > TimeSpan.Zero)
                            Console.WriteLine($"  Time Spent: {FormatTimeSpan(_currentIssueState.TotalTimeSpent)}");
                        if (_currentIssueState.Subtasks.Any())
                        {
                            var completed = _currentIssueState.Subtasks.Count(s => s.Status == "done");
                            Console.WriteLine($"  Subtasks: {completed}/{_currentIssueState.Subtasks.Count} completed");
                        }
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("⚠ NotNow tracking not initialized for this issue");
                    }
                    Console.ResetColor();
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"✗ Error loading issue state: {ex.Message}");
                    Console.ResetColor();
                    _currentIssueState = null;
                }
            }
            else
            {
                Console.WriteLine($"\nIssue #{issueNumber} not found in the loaded issues.");
                _currentIssueState = null;
            }
        }
        else
        {
            Console.WriteLine("\nInvalid issue number.");
        }
    }

    static async Task ViewCurrentIssueComments()
    {
        Console.Clear();
        Console.WriteLine("========================================");
        Console.WriteLine("        Issue Comments");
        Console.WriteLine("========================================");

        if (_currentIssue == null)
        {
            Console.WriteLine("\nNo issue selected. Please select an issue first.");
            return;
        }

        Console.WriteLine($"\nIssue: #{_currentIssue.Number} - {_currentIssue.Title}");
        Console.WriteLine("----------------------------------------");
        Console.WriteLine("\nIssue Description:");
        Console.WriteLine(_currentIssue.Body ?? "No description");
        Console.WriteLine("\n----------------------------------------");

        Console.WriteLine("\nFetching comments...");
        var comments = await _gitHubService!.GetIssueCommentsAsync(_currentIssue.Number);

        if (comments.Count == 0)
        {
            Console.WriteLine("\nNo comments on this issue.");
        }
        else
        {
            Console.WriteLine($"\nComments ({comments.Count}):\n");
            foreach (var comment in comments)
            {
                Console.WriteLine($"Author: {comment.User.Login} | Date: {comment.CreatedAt:yyyy-MM-dd HH:mm}");
                Console.WriteLine($"{comment.Body}");
                Console.WriteLine("----------------------------------------");
            }
        }
    }

    static async Task AddCommentToCurrentIssue()
    {
        Console.Clear();
        Console.WriteLine("========================================");
        Console.WriteLine("      Add Comment to Issue");
        Console.WriteLine("========================================");

        if (_currentIssue == null)
        {
            Console.WriteLine("\nNo issue selected. Please select an issue first.");
            return;
        }

        Console.WriteLine($"\nAdding comment to: #{_currentIssue.Number} - {_currentIssue.Title}");
        Console.WriteLine("\nEnter your comment (type END on a new line to finish):");
        Console.WriteLine("Tip: You can include /notnow commands in your comment!");

        var lines = new List<string>();
        string line;
        while ((line = Console.ReadLine() ?? "") != "END")
        {
            lines.Add(line);
        }

        var comment = string.Join("\n", lines);

        if (string.IsNullOrWhiteSpace(comment))
        {
            Console.WriteLine("\nComment cannot be empty.");
            return;
        }

        // Check if the comment contains /notnow commands
        if (comment.Contains("/notnow", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("\nDetected /notnow commands in comment. Processing...");

            // Execute any embedded commands
            var context = new CommandExecutionContext
            {
                IssueNumber = _currentIssue.Number,
                User = "current-user",
                Timestamp = DateTime.UtcNow,
                CommandContext = CommandContext.Comment
            };

            var result = await _commandExecutor!.ExecuteAsync(comment, context);

            if (result.Results.Any())
            {
                Console.WriteLine("\nCommand execution results:");
                foreach (var cmdResult in result.Results)
                {
                    if (cmdResult.Success)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"✓ {cmdResult.Message}");
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"✗ {cmdResult.Error}");
                    }
                    Console.ResetColor();
                }
            }
        }

        Console.WriteLine("\nAdding comment...");
        var addedComment = await _gitHubService!.AddCommentToIssueAsync(_currentIssue.Number, comment);

        Console.WriteLine($"\nComment added successfully by {addedComment.User.Login} at {addedComment.CreatedAt:yyyy-MM-dd HH:mm}");
    }

    static async Task CreateNewIssue()
    {
        Console.Clear();
        Console.WriteLine("========================================");
        Console.WriteLine("         Create New Issue");
        Console.WriteLine("========================================");

        Console.Write("\nEnter issue title: ");
        var title = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(title))
        {
            Console.WriteLine("\nTitle cannot be empty.");
            return;
        }

        Console.WriteLine("\nEnter issue description (type END on a new line to finish):");
        var lines = new List<string>();
        string line;
        while ((line = Console.ReadLine() ?? "") != "END")
        {
            lines.Add(line);
        }

        var description = string.Join("\n", lines);

        // Ask if this should be a NotNow-tracked issue
        Console.Write("\nInitialize as NotNow-tracked issue? (y/n): ");
        var initNotNow = Console.ReadLine()?.Trim().ToLower() == "y";

        var body = description;

        if (initNotNow)
        {
            Console.WriteLine("\nNotNow Configuration:");

            Console.Write("Type (bug/feature/task) [task]: ");
            var type = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(type)) type = "task";

            Console.Write("Priority (low/medium/high/critical) [medium]: ");
            var priority = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(priority)) priority = "medium";

            Console.Write("Assignee (username or press Enter to skip): ");
            var assignee = Console.ReadLine()?.Trim();

            Console.Write("Estimate (e.g., 8h, 2h30m, or press Enter to skip): ");
            var estimate = Console.ReadLine()?.Trim();

            Console.Write("Due date (YYYY-MM-DD or press Enter to skip): ");
            var dueDate = Console.ReadLine()?.Trim();

            Console.Write("Tags (comma-separated, or press Enter to skip): ");
            var tags = Console.ReadLine()?.Trim();

            // Add subtasks
            var subtasks = new List<string>();
            Console.WriteLine("\nAdd subtasks (press Enter with empty title to finish):");
            int subtaskId = 1;
            while (true)
            {
                Console.Write($"Subtask {subtaskId} title: ");
                var subtaskTitle = Console.ReadLine()?.Trim();
                if (string.IsNullOrEmpty(subtaskTitle)) break;

                Console.Write($"  Estimate (optional): ");
                var subtaskEstimate = Console.ReadLine()?.Trim();

                var subtaskCmd = $"/notnow subtask \"{subtaskTitle}\" --id st{subtaskId}";
                if (!string.IsNullOrEmpty(subtaskEstimate))
                    subtaskCmd += $" --estimate {subtaskEstimate}";

                subtasks.Add(subtaskCmd);
                subtaskId++;
            }

            // Build the issue body with NotNow initialization
            body = description;
            body += "\n\n---\n\n";
            body += "/notnow init\n";
            body += $"/notnow type {type}\n";
            body += $"/notnow priority {priority}\n";

            if (!string.IsNullOrEmpty(assignee))
            {
                if (!assignee.StartsWith("@")) assignee = "@" + assignee;
                body += $"/notnow assignee {assignee}\n";
            }

            if (!string.IsNullOrEmpty(estimate))
                body += $"/notnow estimate {estimate}\n";

            if (!string.IsNullOrEmpty(dueDate))
                body += $"/notnow due {dueDate}\n";

            if (!string.IsNullOrEmpty(tags))
                body += $"/notnow tags {tags}\n";

            if (subtasks.Any())
            {
                body += "\n";
                foreach (var subtask in subtasks)
                {
                    body += subtask + "\n";
                }
            }
        }

        Console.WriteLine("\nCreating issue...");
        var newIssue = await _gitHubService!.CreateIssueAsync(title, body);

        Console.WriteLine($"\nIssue created successfully!");
        Console.WriteLine($"Issue #{newIssue.Number}: {newIssue.Title}");
        Console.WriteLine($"URL: {newIssue.HtmlUrl}");

        if (initNotNow)
        {
            Console.WriteLine("\n✓ NotNow tracking initialized");
            Console.WriteLine("The issue body contains initialization commands that will be processed when the issue is first accessed.");
        }

        _currentIssue = newIssue;
        _issues.Insert(0, newIssue);
    }
}