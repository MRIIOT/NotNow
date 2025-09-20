using Microsoft.Maui.Controls;
using NotNow.Core.Services;
using NotNow.Core.Console;
using NotNow.GitHubService.Interfaces;
using System.Collections.ObjectModel;
using Microsoft.Extensions.DependencyInjection;
using NotNow.Core.Commands.Execution;
using NotNow.Core.Commands.Parser;
using NotNow.Core.Commands.Framework;

namespace NotNow.Quake.Views;

public partial class TerminalPage : ContentPage
{
    private IGitHubService? _gitHubService;
    private IIssueStateService? _stateService;
    private ICommandExecutor? _commandExecutor;
    private CommandAutoCompleter? _autoCompleter;
    private ICommandParser? _commandParser;
    private ObservableCollection<IssueItem> _issues;
    private IssueItem? _selectedIssue;
    private List<string> _currentSuggestions = new();

    public TerminalPage()
    {
        InitializeComponent();

        _issues = new ObservableCollection<IssueItem>();
        IssuesListView.ItemsSource = _issues;
        IssuesListView.SelectionChanged += OnIssueSelectionChanged;

        // Services will be initialized when the page is loaded
        Loaded += OnPageLoaded;
    }

    private void OnPageLoaded(object? sender, EventArgs e)
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
        }

        // Load issues after services are initialized
        LoadIssues();
    }

    private async void LoadIssues()
    {
        try
        {
            if (_gitHubService == null) return;
            var issues = await _gitHubService.GetIssuesAsync();
            _issues.Clear();

            foreach (var issue in issues.Take(20)) // Show top 20 issues
            {
                _issues.Add(new IssueItem
                {
                    Number = issue.Number,
                    Title = issue.Title,
                    DisplayText = $"#{issue.Number} {issue.Title.Substring(0, Math.Min(issue.Title.Length, 20))}"
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
            if (_gitHubService == null || _stateService == null) return;
            var issues = await _gitHubService.GetIssuesAsync();
            var issue = issues.FirstOrDefault(i => i.Number == issueNumber);
            if (issue == null) return;
            var state = _stateService.GetOrCreateState(issueNumber);

            // Update issue details
            IssueTitle.Text = $"#{issue.Number}: {issue.Title}";
            StatusLabel.Text = state.Status ?? "todo";
            PriorityLabel.Text = state.Priority ?? "medium";
            AssigneeLabel.Text = issue.Assignee?.Login ?? "unassigned";
            DueLabel.Text = state.DueDate?.ToString("yyyy-MM-dd") ?? "not set";
            EstimateLabel.Text = state.Estimate ?? "not set";

            // Update time tracking
            TotalTimeLabel.Text = "5h 30m"; // TODO: Calculate from state
            TodayTimeLabel.Text = "2h 15m";
            WeekTimeLabel.Text = "12h";

            // Update subtasks
            SubtasksList.Children.Clear();
            foreach (var subtask in state.Subtasks)
            {
                var subtaskLabel = new Label
                {
                    Text = $"{(subtask.Status == "done" ? "☑" : "☐")} {subtask.Title} ({subtask.Estimate ?? "?"})",
                    TextColor = Color.FromArgb("#00FF00"),
                    FontFamily = "CascadiaMono",
                    FontSize = 11
                };
                SubtasksList.Children.Add(subtaskLabel);
            }

            // Update progress
            if (state.Subtasks.Any())
            {
                var completed = state.Subtasks.Count(s => s.Status == "done");
                var total = state.Subtasks.Count;
                ProgressBar.Progress = (double)completed / total;
            }
            else
            {
                ProgressBar.Progress = 0;
            }

            // Load comments
            await LoadComments(issueNumber);
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

            CommentsList.Children.Clear();

            foreach (var comment in comments.Take(10)) // Show last 10 comments
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
                    TextColor = Color.FromArgb("#008800"),
                    FontFamily = "CascadiaMono",
                    FontSize = 10
                };
                Grid.SetRow(authorLabel, 0);
                commentGrid.Children.Add(authorLabel);

                var bodyLabel = new Label
                {
                    Text = TruncateComment(comment.Body),
                    TextColor = Color.FromArgb("#00FF00"),
                    FontFamily = "CascadiaMono",
                    FontSize = 11,
                    Margin = new Thickness(0, 2)
                };
                Grid.SetRow(bodyLabel, 1);
                commentGrid.Children.Add(bodyLabel);

                var separator = new BoxView
                {
                    Color = Color.FromArgb("#003300"),
                    HeightRequest = 1,
                    Margin = new Thickness(0, 5)
                };
                Grid.SetRow(separator, 2);
                commentGrid.Children.Add(separator);

                CommentsList.Children.Add(commentGrid);
            }
        }
        catch
        {
            // Silently fail for comments
        }
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

    private string TruncateComment(string comment)
    {
        var lines = comment.Split('\n');
        if (lines.Length > 3)
        {
            return string.Join('\n', lines.Take(3)) + "...";
        }
        if (comment.Length > 200)
        {
            return comment.Substring(0, 200) + "...";
        }
        return comment;
    }

    private void OnCommandTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.NewTextValue))
        {
            SuggestionsPanel.IsVisible = false;
            return;
        }

        if (_autoCompleter == null) return;

        var input = "/notnow " + e.NewTextValue;
        _currentSuggestions = _autoCompleter.GetSuggestions(input, CommandContext.Comment);

        if (_currentSuggestions.Any())
        {
            SuggestionsList.Children.Clear();
            foreach (var suggestion in _currentSuggestions.Take(5))
            {
                var suggestionLabel = new Label
                {
                    Text = suggestion,
                    TextColor = Color.FromArgb("#00FF00"),
                    FontFamily = "CascadiaMono",
                    FontSize = 11
                };

                var tapGesture = new TapGestureRecognizer();
                tapGesture.Tapped += (s, e) =>
                {
                    CommandInput.Text = suggestion + " ";
                    SuggestionsPanel.IsVisible = false;
                };
                suggestionLabel.GestureRecognizers.Add(tapGesture);

                SuggestionsList.Children.Add(suggestionLabel);
            }
            SuggestionsPanel.IsVisible = true;
        }
        else
        {
            SuggestionsPanel.IsVisible = false;
        }
    }

    private async void OnCommandCompleted(object? sender, EventArgs e)
    {
        if (_selectedIssue == null || string.IsNullOrWhiteSpace(CommandInput.Text))
            return;

        var command = "/notnow " + CommandInput.Text.Trim();
        CommandInput.Text = "";
        SuggestionsPanel.IsVisible = false;

        try
        {
            if (_commandExecutor == null || _commandParser == null || _gitHubService == null) return;

            // Parse command first
            var parseResult = _commandParser.Parse(command, CommandContext.Comment);
            if (!parseResult.Commands.Any())
            {
                await DisplayAlert("Error", "Invalid command", "OK");
                return;
            }

            // Execute command
            var context = new CommandExecutionContext
            {
                IssueNumber = _selectedIssue.Number,
                User = "current-user" // TODO: Get from config
            };

            var result = await _commandExecutor.ExecuteCommandsAsync(parseResult.Commands, context);

            if (result.Success)
            {
                // Post to GitHub if successful
                var comment = $"{command}\n\n{result.Summary}";
                await _gitHubService.AddCommentToIssueAsync(_selectedIssue.Number, comment);

                // Reload issue details
                await LoadIssueDetails(_selectedIssue.Number);
            }
            else
            {
                await DisplayAlert("Command Error", result.Summary ?? "Command failed", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to execute command: {ex.Message}", "OK");
        }
    }

    public class IssueItem
    {
        public int Number { get; set; }
        public string Title { get; set; } = "";
        public string DisplayText { get; set; } = "";
        public bool IsSelected { get; set; }
    }
}