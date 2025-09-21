using Microsoft.Maui.Controls;
using NotNow.Core.Services;
using NotNow.Core.Console;
using NotNow.GitHubService.Interfaces;
using System.Collections.ObjectModel;
using Microsoft.Extensions.DependencyInjection;
using NotNow.Core.Commands.Execution;
using NotNow.Core.Commands.Parser;
using NotNow.Core.Commands.Framework;
using NotNow.Core.Commands.Registry;

namespace NotNow.Quake.Views;

public partial class TerminalPage : ContentPage
{
    private IGitHubService? _gitHubService;
    private IIssueStateService? _stateService;
    private ICommandExecutor? _commandExecutor;
    private CommandAutoCompleter? _autoCompleter;
    private ICommandParser? _commandParser;
    private ICommandPostingService? _commandPostingService;
    private IIssueStateParser? _issueStateParser;
    private ObservableCollection<IssueItem> _issues;
    private IssueItem? _selectedIssue;
    private List<string> _currentSuggestions = new();
    private bool _showCommandComments = false;  // OFF by default (commands are filtered/hidden)
    private List<Octokit.IssueComment>? _allComments;

    public TerminalPage()
    {
        InitializeComponent();

        _issues = new ObservableCollection<IssueItem>();
        IssuesListView.ItemsSource = _issues;
        IssuesListView.SelectionChanged += OnIssueSelectionChanged;

        // Services will be initialized when the page is loaded
        Loaded += OnPageLoaded;
    }

    private async void OnPageLoaded(object? sender, EventArgs e)
    {
        // Get services from DI after the page is loaded
        var serviceProvider = Handler?.MauiContext?.Services;
        if (serviceProvider != null)
        {
            _gitHubService = serviceProvider.GetService<IGitHubService>();
            _stateService = serviceProvider.GetService<IIssueStateService>();
            _commandExecutor = serviceProvider.GetService<ICommandExecutor>();
            _autoCompleter = serviceProvider.GetService<CommandAutoCompleter>();
            _commandParser = serviceProvider.GetService<ICommandParser>();
            _commandPostingService = serviceProvider.GetService<ICommandPostingService>();
            _issueStateParser = serviceProvider.GetService<IIssueStateParser>();

            // Initialize command registry
            var initService = serviceProvider.GetService<ICommandInitializationService>();
            if (initService != null)
            {
                initService.Initialize();
            }
        }

        // Initialize filter toggle appearance (OFF by default - commands hidden)
        if (CommandFilterToggle != null && CommandFilterToggleBorder != null)
        {
            CommandFilterToggleBorder.BackgroundColor = Color.FromArgb("#2A2A2A");
            CommandFilterToggleBorder.Stroke = Color.FromArgb("#4A9EFF");
            CommandFilterToggle.TextColor = Color.FromArgb("#808080");
        }

        // Load issues after services are initialized
        LoadIssues();
    }

    private async Task RefreshIssues()
    {
        await LoadIssuesAsync();
    }

    private async void LoadIssues()
    {
        await LoadIssuesAsync();
    }

    private async Task LoadIssuesAsync()
    {
        try
        {
            if (_gitHubService == null) return;

            // Fetch both open and closed issues
            var openIssues = await _gitHubService.GetIssuesAsync(Octokit.ItemStateFilter.Open);
            var closedIssues = await _gitHubService.GetIssuesAsync(Octokit.ItemStateFilter.Closed);

            _issues.Clear();

            // Add open issues first (limit to 15)
            foreach (var issue in openIssues.Take(15))
            {
                _issues.Add(new IssueItem
                {
                    Number = issue.Number,
                    Title = issue.Title,
                    DisplayText = $"#{issue.Number} {issue.Title.Substring(0, Math.Min(issue.Title.Length, 20))}",
                    IsClosed = false
                });
            }

            // Then add closed issues (limit to 10)
            foreach (var issue in closedIssues.Take(10))
            {
                _issues.Add(new IssueItem
                {
                    Number = issue.Number,
                    Title = issue.Title,
                    DisplayText = $"#{issue.Number} {issue.Title.Substring(0, Math.Min(issue.Title.Length, 20))}",
                    IsClosed = true
                });
            }

            if (_issues.Any())
            {
                IssuesListView.SelectedItem = _issues.First();
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to load issues: {ex.Message}", "OK");
        }
    }

    private async void OnIssueSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is IssueItem issue)
        {
            _selectedIssue = issue;
            await LoadIssueDetails(issue.Number);
        }
    }

    private async Task LoadIssueDetails(int issueNumber)
    {
        try
        {
            if (_gitHubService == null || _stateService == null || _issueStateParser == null) return;

            // Try to find the issue in open issues first
            var openIssues = await _gitHubService.GetIssuesAsync(Octokit.ItemStateFilter.Open);
            var issue = openIssues.FirstOrDefault(i => i.Number == issueNumber);

            // If not found in open issues, check closed issues
            if (issue == null)
            {
                var closedIssues = await _gitHubService.GetIssuesAsync(Octokit.ItemStateFilter.Closed);
                issue = closedIssues.FirstOrDefault(i => i.Number == issueNumber);
            }

            if (issue == null) return;

            // Load comments first to parse state
            var comments = await _gitHubService.GetIssueCommentsAsync(issueNumber);

            // Debug: Log all comments to see what we're parsing
            System.Diagnostics.Debug.WriteLine($"Issue #{issue.Number}: Body = {issue.Body ?? "(empty)"}");
            System.Diagnostics.Debug.WriteLine($"Issue #{issue.Number}: Found {comments.Count()} comments");
            foreach (var comment in comments)
            {
                System.Diagnostics.Debug.WriteLine($"  Comment by {comment.User.Login}: {comment.Body?.Substring(0, Math.Min(100, comment.Body?.Length ?? 0))}...");
                if (comment.Body?.Contains("/notnow", StringComparison.OrdinalIgnoreCase) == true)
                {
                    System.Diagnostics.Debug.WriteLine($"    ^ Contains /notnow command!");
                }
            }

            // Parse state from issue body and comments
            var state = _issueStateParser.ParseIssueState(issue, comments);

            // Update the state service with the parsed state from GitHub
            // This ensures command handlers have access to the current state
            _stateService.SetState(issue.Number, state);

            // Debug: Log subtask count
            System.Diagnostics.Debug.WriteLine($"Issue #{issue.Number}: Parsed state - Found {state.Subtasks.Count} subtasks");
            foreach (var subtask in state.Subtasks)
            {
                System.Diagnostics.Debug.WriteLine($"  - {subtask.Title} ({subtask.Status})");
            }

            // Update issue details
            IssueTitle.Text = $"#{issue.Number}: {issue.Title}";

            // Display issue body/description
            IssueDescription.Text = string.IsNullOrWhiteSpace(issue.Body)
                ? "No description provided"
                : issue.Body;

            StatusLabel.Text = state.Status ?? "todo";
            PriorityLabel.Text = state.Priority ?? "medium";
            AssigneeLabel.Text = issue.Assignee?.Login ?? "unassigned";
            DueLabel.Text = state.DueDate?.ToString("yyyy-MM-dd") ?? "not set";
            EstimateLabel.Text = state.Estimate ?? "not set";

            // Update time tracking
            TotalTimeLabel.Text = FormatTimeSpan(state.TotalTimeSpent);

            // Calculate today's time
            var todayTime = TimeSpan.FromSeconds(
                state.Sessions
                    .Where(s => s.StartedAt.Date == DateTime.UtcNow.Date)
                    .Sum(s => s.Duration.TotalSeconds));
            TodayTimeLabel.Text = FormatTimeSpan(todayTime);

            // Calculate this week's time
            var weekStart = DateTime.UtcNow.Date.AddDays(-(int)DateTime.UtcNow.DayOfWeek);
            var weekTime = TimeSpan.FromSeconds(
                state.Sessions
                    .Where(s => s.StartedAt >= weekStart)
                    .Sum(s => s.Duration.TotalSeconds));
            WeekTimeLabel.Text = FormatTimeSpan(weekTime);

            // Update subtasks
            SubtasksList.Children.Clear();
            if (state.Subtasks.Any())
            {
                foreach (var subtask in state.Subtasks)
                {
                    var subtaskGrid = new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitionCollection
                        {
                            new ColumnDefinition { Width = new GridLength(25) },
                            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
                        }
                    };

                    // Clickable checkbox
                    var checkboxLabel = new Label
                    {
                        Text = subtask.Status == "done" ? "☑" : "☐",
                        TextColor = Color.FromArgb("#4A9EFF"),
                        FontFamily = "CascadiaMono",
                        FontSize = 14,
                        VerticalTextAlignment = TextAlignment.Center
                    };

                    var tapGesture = new TapGestureRecognizer();
                    tapGesture.Tapped += async (s, e) => await OnSubtaskCheckboxClicked(subtask.Id, subtask.Status);
                    checkboxLabel.GestureRecognizers.Add(tapGesture);

                    Grid.SetColumn(checkboxLabel, 0);
                    subtaskGrid.Children.Add(checkboxLabel);

                    // Subtask text
                    var textLabel = new Label
                    {
                        Text = $"{subtask.Title} ({subtask.Estimate ?? "?"})",
                        TextColor = subtask.Status == "done"
                            ? Color.FromArgb("#808080")  // Grayed out when done
                            : Color.FromArgb("#E0E0E0"),
                        FontFamily = "CascadiaMono",
                        FontSize = 13,
                        TextDecorations = subtask.Status == "done"
                            ? TextDecorations.Strikethrough
                            : TextDecorations.None,
                        VerticalTextAlignment = TextAlignment.Center
                    };

                    Grid.SetColumn(textLabel, 1);
                    subtaskGrid.Children.Add(textLabel);

                    SubtasksList.Children.Add(subtaskGrid);
                }
            }
            else
            {
                // Show placeholder when no subtasks
                var placeholderLabel = new Label
                {
                    Text = "No subtasks defined",
                    TextColor = Color.FromArgb("#606060"),
                    FontFamily = "CascadiaMono",
                    FontSize = 13,
                    FontAttributes = FontAttributes.Italic
                };
                SubtasksList.Children.Add(placeholderLabel);
            }


            // Display comments (already loaded above for state parsing)
            _allComments = comments.ToList();
            DisplayComments();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to load issue details: {ex.Message}", "OK");
        }
    }

    private async Task LoadComments(int issueNumber)
    {
        try
        {
            if (_gitHubService == null) return;
            var comments = await _gitHubService.GetIssueCommentsAsync(issueNumber);
            _allComments = comments.ToList();

            DisplayComments();
        }
        catch
        {
            // Silently fail for comments
        }
    }

    private void DisplayComments()
    {
        if (_allComments == null) return;

        CommentsList.Children.Clear();

        // Show command comments only if toggle is ON (by default it's OFF, so commands are hidden)
        var commentsToShow = _showCommandComments
            ? _allComments  // Show all including commands
            : _allComments.Where(c => !IsCommandComment(c));  // Filter out commands

        // Sort comments by date ascending (oldest first)
        var sortedComments = commentsToShow.OrderBy(c => c.CreatedAt);

            foreach (var comment in sortedComments)
            {
                var commentGrid = new Grid
                {
                    RowDefinitions = new RowDefinitionCollection
                    {
                        new RowDefinition { Height = GridLength.Auto },
                        new RowDefinition { Height = GridLength.Auto },
                        new RowDefinition { Height = GridLength.Auto }
                    }
                };

                var authorLabel = new Label
                {
                    Text = $"@{comment.User.Login} {GetRelativeTime(comment.CreatedAt)}:",
                    TextColor = Color.FromArgb("#808080"),
                    FontFamily = "CascadiaMono",
                    FontSize = 12
                };
                Grid.SetRow(authorLabel, 0);
                commentGrid.Children.Add(authorLabel);

                // Display full comment body without truncation
                var bodyLabel = new Label
                {
                    Text = comment.Body,
                    TextColor = Color.FromArgb("#E0E0E0"),
                    FontFamily = "CascadiaMono",
                    FontSize = 13,
                    Margin = new Thickness(0, 2),
                    LineBreakMode = LineBreakMode.WordWrap
                };
                Grid.SetRow(bodyLabel, 1);
                commentGrid.Children.Add(bodyLabel);

                var separator = new BoxView
                {
                    Color = Color.FromArgb("#2A2A2A"),
                    HeightRequest = 1,
                    Margin = new Thickness(0, 5)
                };
                Grid.SetRow(separator, 2);
                commentGrid.Children.Add(separator);

                CommentsList.Children.Add(commentGrid);
            }

        // Scroll to bottom to show most recent comments
        // Use a small delay to ensure layout is complete
        Dispatcher.Dispatch(async () =>
        {
            await Task.Delay(100);
            await CommentsScrollView.ScrollToAsync(0, CommentsScrollView.ContentSize.Height, true);
        });
    }

    private bool IsCommandComment(Octokit.IssueComment comment)
    {
        if (string.IsNullOrEmpty(comment.Body))
            return false;

        // Check if comment contains /notnow command
        if (comment.Body.Contains("/notnow", StringComparison.OrdinalIgnoreCase))
            return true;

        // Check for notnow-metadata marker
        if (comment.Body.Contains("<!-- notnow-metadata", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private void OnCommandFilterToggled(object? sender, EventArgs e)
    {
        _showCommandComments = !_showCommandComments;

        // Update toggle appearance (matching subtasks button style)
        if (CommandFilterToggle != null && CommandFilterToggleBorder != null)
        {
            if (_showCommandComments)
            {
                // ON state - showing commands
                CommandFilterToggleBorder.BackgroundColor = Color.FromArgb("#4A9EFF");
                CommandFilterToggleBorder.Stroke = Color.FromArgb("#4A9EFF");
                CommandFilterToggle.TextColor = Color.FromArgb("#FFFFFF");
            }
            else
            {
                // OFF state - hiding commands (default)
                CommandFilterToggleBorder.BackgroundColor = Color.FromArgb("#2A2A2A");
                CommandFilterToggleBorder.Stroke = Color.FromArgb("#4A9EFF");
                CommandFilterToggle.TextColor = Color.FromArgb("#808080");
            }
        }

        // Refresh comment display
        DisplayComments();
    }

    private string GetRelativeTime(DateTimeOffset dateTime)
    {
        var diff = DateTimeOffset.Now - dateTime;
        if (diff.TotalMinutes < 60)
            return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalHours < 24)
            return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalDays < 7)
            return $"{(int)diff.TotalDays}d ago";
        return dateTime.ToString("MMM dd");
    }

    private string FormatTimeSpan(TimeSpan time)
    {
        if (time == TimeSpan.Zero)
            return "0m";

        var parts = new List<string>();
        if (time.Days > 0)
            parts.Add($"{time.Days}d");
        if (time.Hours > 0)
            parts.Add($"{time.Hours}h");
        if (time.Minutes > 0)
            parts.Add($"{time.Minutes}m");

        return string.Join(" ", parts);
    }

    private void OnSubtasksTabTapped(object? sender, EventArgs e)
    {
        // Switch to Subtasks tab
        SubtasksTab.BackgroundColor = Color.FromArgb("#2A2A2A");
        if (SubtasksTab.Content is Label subtasksLabel)
            subtasksLabel.TextColor = Color.FromArgb("#FFFFFF");

        TimeTrackingTab.BackgroundColor = Color.FromArgb("#1A1A1A");
        if (TimeTrackingTab.Content is Label timeTrackingLabel)
            timeTrackingLabel.TextColor = Color.FromArgb("#808080");

        SubtasksContent.IsVisible = true;
        TimeTrackingContent.IsVisible = false;
    }

    private void OnTimeTrackingTabTapped(object? sender, EventArgs e)
    {
        // Switch to Time Tracking tab
        TimeTrackingTab.BackgroundColor = Color.FromArgb("#2A2A2A");
        if (TimeTrackingTab.Content is Label timeTrackingLabel)
            timeTrackingLabel.TextColor = Color.FromArgb("#FFFFFF");

        SubtasksTab.BackgroundColor = Color.FromArgb("#1A1A1A");
        if (SubtasksTab.Content is Label subtasksLabel)
            subtasksLabel.TextColor = Color.FromArgb("#808080");

        TimeTrackingContent.IsVisible = true;
        SubtasksContent.IsVisible = false;
    }

    private void OnAddSubtaskClicked(object? sender, EventArgs e)
    {
        // Show the input panel
        SubtaskInputPanel.IsVisible = true;
        SubtaskTitleInput.Text = "";
        SubtaskEstimateInput.Text = "";
        SubtaskTitleInput.Focus();
    }

    private void OnCancelSubtaskInput(object? sender, EventArgs e)
    {
        // Hide the input panel
        SubtaskInputPanel.IsVisible = false;
        SubtaskTitleInput.Text = "";
        SubtaskEstimateInput.Text = "";
    }

    private async Task OnSubtaskCheckboxClicked(string subtaskId, string currentStatus)
    {
        if (_selectedIssue == null || string.IsNullOrWhiteSpace(subtaskId)) return;

        try
        {
            if (_commandExecutor == null || _commandParser == null || _commandPostingService == null) return;

            // Build the appropriate command based on current status
            string command;
            if (currentStatus == "done")
            {
                // Reopen the subtask
                command = $"/notnow reopen {subtaskId}";
            }
            else
            {
                // Complete the subtask
                command = $"/notnow complete {subtaskId}";
            }

            // Parse command
            var parseResult = _commandParser.Parse(command, CommandContext.Comment);
            if (!parseResult.Commands.Any())
            {
                await DisplayAlert("Error", "Failed to parse subtask toggle command", "OK");
                return;
            }

            // Execute command
            var context = new CommandExecutionContext
            {
                IssueNumber = _selectedIssue.Number,
                User = "current-user", // TODO: Get from config
                CommandContext = CommandContext.Comment,
                RawText = command
            };

            var result = await _commandExecutor.ExecuteCommandsAsync(parseResult.Commands, context);

            if (result.Success)
            {
                // Post the command with metadata to GitHub
                await _commandPostingService.PostCommandToGitHubAsync(_selectedIssue.Number, command, result);

                // Add a small delay to ensure GitHub has processed the command
                await Task.Delay(1000);

                // Reload issue details to show updated subtask
                await LoadIssueDetails(_selectedIssue.Number);
            }
            else
            {
                var errorMessage = $"Failed to toggle subtask:\n\n{result.Summary}";

                // Add detailed error information
                var failedCommands = result.Results.Where(r => !r.Success).ToList();
                if (failedCommands.Any())
                {
                    errorMessage += "\n\nDetails:";
                    foreach (var failed in failedCommands)
                    {
                        if (!string.IsNullOrEmpty(failed.Error))
                        {
                            errorMessage += $"\n• {failed.Error}";
                        }
                        else if (!string.IsNullOrEmpty(failed.Message))
                        {
                            errorMessage += $"\n• {failed.Message}";
                        }
                    }
                }

                // Also include the command that was attempted
                errorMessage += $"\n\nCommand attempted:\n{command}";

                await DisplayAlert("Error", errorMessage, "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to toggle subtask: {ex.Message}", "OK");
        }
    }

    private async void OnSubmitSubtask(object? sender, EventArgs e)
    {
        if (_selectedIssue == null || string.IsNullOrWhiteSpace(SubtaskTitleInput.Text))
        {
            if (string.IsNullOrWhiteSpace(SubtaskTitleInput.Text))
            {
                await DisplayAlert("Error", "Title is required for subtask", "OK");
            }
            return;
        }

        var title = SubtaskTitleInput.Text.Trim();
        var estimate = SubtaskEstimateInput.Text?.Trim();

        // Hide the input panel
        SubtaskInputPanel.IsVisible = false;

        try
        {
            if (_commandExecutor == null || _commandParser == null || _commandPostingService == null || _gitHubService == null || _issueStateParser == null) return;

            // Get current issue state to determine next ID
            string nextId = "st1";
            try
            {
                // Try open issues first, then closed
                var openIssues = await _gitHubService.GetIssuesAsync(Octokit.ItemStateFilter.Open);
                var issue = openIssues.FirstOrDefault(i => i.Number == _selectedIssue.Number);

                if (issue == null)
                {
                    var closedIssues = await _gitHubService.GetIssuesAsync(Octokit.ItemStateFilter.Closed);
                    issue = closedIssues.FirstOrDefault(i => i.Number == _selectedIssue.Number);
                }

                if (issue != null)
                {
                    var comments = await _gitHubService.GetIssueCommentsAsync(_selectedIssue.Number);
                    var state = _issueStateParser.ParseIssueState(issue, comments);

                    if (state.Subtasks != null && state.Subtasks.Any())
                    {
                        var existingIds = state.Subtasks
                            .Where(s => s.Id?.StartsWith("st") == true)
                            .Select(s =>
                            {
                                if (int.TryParse(s.Id.Substring(2), out int num))
                                    return num;
                                return 0;
                            })
                            .DefaultIfEmpty(0)
                            .Max();

                        nextId = $"st{existingIds + 1}";
                    }
                }
            }
            catch
            {
                // If we can't get the current state, fall back to st1
            }

            // Build the /notnow subtask add command with explicit ID
            var escapedTitle = title.Replace("\"", "\\\"");
            var command = $"/notnow subtask add \"{escapedTitle}\" --id {nextId}";

            if (!string.IsNullOrWhiteSpace(estimate))
            {
                command += $" --estimate {estimate}";
            }

            // Parse command
            var parseResult = _commandParser.Parse(command, CommandContext.Comment);
            if (!parseResult.Commands.Any())
            {
                await DisplayAlert("Error", "Failed to parse subtask command", "OK");
                return;
            }

            // Execute command
            var context = new CommandExecutionContext
            {
                IssueNumber = _selectedIssue.Number,
                User = "current-user", // TODO: Get from config
                CommandContext = CommandContext.Comment,
                RawText = command
            };

            var result = await _commandExecutor.ExecuteCommandsAsync(parseResult.Commands, context);

            if (result.Success)
            {
                // Post the command with metadata to GitHub
                await _commandPostingService.PostCommandToGitHubAsync(_selectedIssue.Number, command, result);

                // Clear inputs
                SubtaskTitleInput.Text = "";
                SubtaskEstimateInput.Text = "";

                // Reload issue details to show new subtask
                await LoadIssueDetails(_selectedIssue.Number);
            }
            else
            {
                var errorMessage = $"Failed to add subtask:\n\n{result.Summary}";

                // Add detailed error information
                var failedCommands = result.Results.Where(r => !r.Success).ToList();
                if (failedCommands.Any())
                {
                    errorMessage += "\n\nDetails:";
                    foreach (var failed in failedCommands)
                    {
                        if (!string.IsNullOrEmpty(failed.Error))
                        {
                            errorMessage += $"\n• {failed.Error}";
                        }
                        else if (!string.IsNullOrEmpty(failed.Message))
                        {
                            errorMessage += $"\n• {failed.Message}";
                        }
                    }
                }

                errorMessage += $"\n\nCommand attempted:\n{command}";
                await DisplayAlert("Error", errorMessage, "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to add subtask: {ex.Message}", "OK");
        }
    }



    private void OnAddCommentClicked(object? sender, EventArgs e)
    {
        // Show the comment input panel
        CommentInputPanel.IsVisible = true;
        CommentBodyInput.Text = "";
        MarkdownCheckbox.IsChecked = false;
        CommentBodyInput.Focus();
    }

    private void OnCancelCommentInput(object? sender, EventArgs e)
    {
        // Hide the comment input panel
        CommentInputPanel.IsVisible = false;
        CommentBodyInput.Text = "";
        MarkdownCheckbox.IsChecked = false;
    }

    private void OnAddNoteClicked(object? sender, EventArgs e)
    {
        // Show the note input panel
        NoteInputPanel.IsVisible = true;
        NoteTitleInput.Text = "";
        NoteCategoryInput.Text = "";
        NoteContentInput.Text = "";
        NoteContentInput.Focus();
    }

    private void OnCancelNoteInput(object? sender, EventArgs e)
    {
        // Hide the note input panel
        NoteInputPanel.IsVisible = false;
        NoteTitleInput.Text = "";
        NoteCategoryInput.Text = "";
        NoteContentInput.Text = "";
    }

    private async void OnSubmitNote(object? sender, EventArgs e)
    {
        if (_selectedIssue == null || string.IsNullOrWhiteSpace(NoteContentInput.Text))
        {
            if (string.IsNullOrWhiteSpace(NoteContentInput.Text))
            {
                await DisplayAlert("Error", "Note content is required", "OK");
            }
            return;
        }

        var noteContent = NoteContentInput.Text.Trim();
        var noteTitle = NoteTitleInput.Text?.Trim();
        var noteCategory = NoteCategoryInput.Text?.Trim();

        // Hide the input panel
        NoteInputPanel.IsVisible = false;

        try
        {
            if (_commandExecutor == null || _commandParser == null || _commandPostingService == null) return;

            // Build the /notnow note command
            var escapedContent = noteContent.Replace("\"", "\\\"");
            var command = $"/notnow note \"{escapedContent}\"";

            if (!string.IsNullOrWhiteSpace(noteTitle))
            {
                var escapedTitle = noteTitle.Replace("\"", "\\\"");
                command += $" --title \"{escapedTitle}\"";
            }

            if (!string.IsNullOrWhiteSpace(noteCategory))
            {
                command += $" --category {noteCategory}";
            }

            // Parse command
            var parseResult = _commandParser.Parse(command, CommandContext.Comment);
            if (!parseResult.Commands.Any())
            {
                await DisplayAlert("Error", "Failed to parse note command", "OK");
                return;
            }

            // Execute command
            var context = new CommandExecutionContext
            {
                IssueNumber = _selectedIssue.Number,
                User = "current-user", // TODO: Get from config
                CommandContext = CommandContext.Comment,
                RawText = noteContent
            };

            var result = await _commandExecutor.ExecuteCommandsAsync(parseResult.Commands, context);

            if (result.Success)
            {
                // Post the command with metadata to GitHub
                await _commandPostingService.PostCommandToGitHubAsync(_selectedIssue.Number, command, result);

                // Clear inputs
                NoteTitleInput.Text = "";
                NoteCategoryInput.Text = "";
                NoteContentInput.Text = "";

                // Reload comments to show the new note
                await LoadComments(_selectedIssue.Number);
            }
            else
            {
                var errorMessage = $"Failed to post note: {result.Summary}";
                await DisplayAlert("Error", errorMessage, "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to post note: {ex.Message}", "OK");
        }
    }

    private async void OnIssueCheckboxTapped(object? sender, EventArgs e)
    {
        if (sender is not Label label || label.BindingContext is not IssueItem issue)
            return;

        try
        {
            if (_gitHubService == null) return;

            if (issue.IsClosed)
            {
                // Reopen the issue
                await _gitHubService.ReopenIssueAsync(issue.Number);
                issue.IsClosed = false;

                // Post /notnow reopen command
                if (_commandPostingService != null)
                {
                    var reopenCommand = "/notnow reopen";
                    var executionResult = new ExecutionResult
                    {
                        Results = new List<CommandResult>
                        {
                            CommandResult.Ok($"Issue #{issue.Number} reopened", new { Reopened = true })
                        }
                    };
                    await _commandPostingService.PostCommandToGitHubAsync(issue.Number, reopenCommand, executionResult);
                }
            }
            else
            {
                // Close the issue
                await _gitHubService.CloseIssueAsync(issue.Number);
                issue.IsClosed = true;

                // Post /notnow complete command
                if (_commandPostingService != null)
                {
                    var completeCommand = "/notnow complete";
                    var executionResult = new ExecutionResult
                    {
                        Results = new List<CommandResult>
                        {
                            CommandResult.Ok($"Issue #{issue.Number} completed", new { Completed = true })
                        }
                    };
                    await _commandPostingService.PostCommandToGitHubAsync(issue.Number, completeCommand, executionResult);
                }
            }

            // Add delay to allow GitHub to process the state change
            await Task.Delay(1500);

            // Refresh the issue display to show the new state
            await RefreshIssues();

            // If this was the selected issue, reload its details
            if (_selectedIssue?.Number == issue.Number)
            {
                await LoadIssueDetails(issue.Number);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to update issue state: {ex.Message}", "OK");
        }
    }

    private void OnAddIssueClicked(object? sender, EventArgs e)
    {
        // Show the issue input panel
        IssueInputPanel.IsVisible = true;
        IssueTitleInput.Text = "";
        IssueDescriptionInput.Text = "";
        IssueTitleInput.Focus();
    }

    private void OnCancelIssueInput(object? sender, EventArgs e)
    {
        // Hide the issue input panel
        IssueInputPanel.IsVisible = false;
        IssueTitleInput.Text = "";
        IssueDescriptionInput.Text = "";
    }

    private async void OnCreateIssue(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(IssueTitleInput.Text))
        {
            await DisplayAlert("Error", "Title is required for the issue", "OK");
            return;
        }

        var title = IssueTitleInput.Text.Trim();
        var description = IssueDescriptionInput.Text?.Trim() ?? "";

        // Hide the input panel
        IssueInputPanel.IsVisible = false;

        try
        {
            if (_gitHubService == null || _commandPostingService == null) return;

            // Create the new issue on GitHub
            var newIssue = await _gitHubService.CreateIssueAsync(title, description);

            if (newIssue != null)
            {
                // Post the /notnow init command as the first comment
                var initCommand = "/notnow init";

                // Create a simple execution result for the init command
                var executionResult = new ExecutionResult
                {
                    Results = new List<CommandResult>
                    {
                        CommandResult.Ok("Issue initialized for NotNow tracking", new { Initialized = true })
                    }
                };

                // Post the init command to GitHub
                await _commandPostingService.PostCommandToGitHubAsync(newIssue.Number, initCommand, executionResult);

                // Clear inputs
                IssueTitleInput.Text = "";
                IssueDescriptionInput.Text = "";

                // Add a small delay to ensure GitHub has processed the new issue
                await Task.Delay(1000);

                // Refresh the issues list
                await RefreshIssues();

                // If the new issue isn't in the list yet, try once more
                if (!_issues.Any(i => i.Number == newIssue.Number))
                {
                    await Task.Delay(1500);
                    await RefreshIssues();
                }

                // Select and load the newly created issue
                _selectedIssue = new IssueItem
                {
                    Number = newIssue.Number,
                    DisplayText = $"#{newIssue.Number}: {newIssue.Title}"
                };

                // Find and select the issue in the CollectionView
                var issueInList = _issues.FirstOrDefault(i => i.Number == newIssue.Number);
                if (issueInList != null)
                {
                    IssuesListView.SelectedItem = issueInList;
                }

                await LoadIssueDetails(newIssue.Number);

                // Success is indicated by the issue appearing in the list - no dialog needed
            }
            else
            {
                await DisplayAlert("Error", "Failed to create issue", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to create issue: {ex.Message}", "OK");
        }
    }

    private async void OnSubmitComment(object? sender, EventArgs e)
    {
        if (_selectedIssue == null || string.IsNullOrWhiteSpace(CommentBodyInput.Text))
        {
            if (string.IsNullOrWhiteSpace(CommentBodyInput.Text))
            {
                await DisplayAlert("Error", "Comment text is required", "OK");
            }
            return;
        }

        var commentText = CommentBodyInput.Text.Trim();
        var useMarkdown = MarkdownCheckbox.IsChecked;

        // Hide the input panel
        CommentInputPanel.IsVisible = false;

        try
        {
            if (_commandExecutor == null || _commandParser == null || _commandPostingService == null) return;

            // Build the /notnow comment command
            var escapedText = commentText.Replace("\"", "\\\"");
            var command = $"/notnow comment --body \"{escapedText}\"";

            if (useMarkdown)
            {
                command += " --markdown";
            }

            // Parse command
            var parseResult = _commandParser.Parse(command, CommandContext.Comment);
            if (!parseResult.Commands.Any())
            {
                await DisplayAlert("Error", "Failed to parse comment command", "OK");
                return;
            }

            // Execute command
            var context = new CommandExecutionContext
            {
                IssueNumber = _selectedIssue.Number,
                User = "current-user", // TODO: Get from config
                CommandContext = CommandContext.Comment,
                RawText = commentText
            };

            var result = await _commandExecutor.ExecuteCommandsAsync(parseResult.Commands, context);

            if (result.Success)
            {
                // Post the command with metadata to GitHub
                await _commandPostingService.PostCommandToGitHubAsync(_selectedIssue.Number, command, result);

                // Clear inputs
                CommentBodyInput.Text = "";
                MarkdownCheckbox.IsChecked = false;

                // Reload comments to show the new comment
                await LoadComments(_selectedIssue.Number);
            }
            else
            {
                var errorMessage = $"Failed to post comment: {result.Summary}";
                await DisplayAlert("Error", errorMessage, "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to post comment: {ex.Message}", "OK");
        }
    }

    public class IssueItem
    {
        public int Number { get; set; }
        public string Title { get; set; } = "";
        public string DisplayText { get; set; } = "";
        public bool IsSelected { get; set; }
        public bool IsClosed { get; set; }
        public string CheckboxText => IsClosed ? "☑" : "☐";
        public Color TextColor => IsClosed ? Color.FromArgb("#808080") : Color.FromArgb("#E0E0E0");
        public TextDecorations TextDecorations => IsClosed ? TextDecorations.Strikethrough : TextDecorations.None;
    }
}