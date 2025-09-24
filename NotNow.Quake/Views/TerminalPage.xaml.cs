using Microsoft.Maui.Controls.Shapes;
using NotNow.Core.Services;
using NotNow.Core.Models;
using NotNow.GitHubService.Models;
using NotNow.GitHubService.Interfaces;
using NotNow.GitHubService.Services;
using System.Collections.ObjectModel;
using Microsoft.Extensions.Configuration;
using NotNow.Core.Commands.Execution;
using NotNow.Core.Commands.Parser;
using NotNow.Core.Commands.Framework;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace NotNow.Quake.Views;

public partial class TerminalPage : ContentPage, IDisposable
{
    private IGitHubService? _gitHubService;
    private IGitHubServiceManager? _gitHubServiceManager;
    private IIssueStateService? _stateService;
    private ICommandExecutor? _commandExecutor;
    private ICommandParser? _commandParser;
    private ICommandPostingService? _commandPostingService;
    private IIssueStateParser? _issueStateParser;
    private IIssueStateManager? _stateManager;
    private IConfiguration? _configuration;
    private ObservableCollection<IssueItem> _issues;
    private ObservableCollection<IssueItem> _filteredIssues;
    private ObservableCollection<GroupedIssueItem> _groupedIssues;
    private List<IssueItem> _allIssues = new List<IssueItem>();
    private string _filterText = string.Empty;
    private IssueItem? _selectedIssue;
    private bool _showCommandComments = false;  // OFF by default (commands are filtered/hidden)
    private bool _hideClosedIssues = true;  // ON by default (closed issues are hidden)
    private bool _groupByTag = false;  // OFF by default (flat list view)
    private enum SortMode
    {
        IssueNumberDescending,  // Default: newest issues first
        DueDateAscending,       // Earliest due dates first (nulls at top)
        DueDateDescending       // Latest due dates first (nulls at top)
    }
    private SortMode _sortMode = SortMode.IssueNumberDescending;
    private List<Octokit.IssueComment>? _allComments;
    private string? _pendingSubtaskId;  // Track subtask being completed
    private string? _currentRepositoryId;
    private FileSystemWatcher? _configWatcher;
    private DateTime _lastConfigReload = DateTime.MinValue;
    private IServiceScope? _currentServiceScope;
    private Octokit.User? _currentUser;

    // Timer fields
    private System.Timers.Timer? _workTimer;
    private System.Timers.Timer? _countdownTimer;
    private DateTime _workTimerStartTime;
    private TimeSpan _workTimerElapsed = TimeSpan.Zero;
    private bool _workTimerRunning = false;
    private bool _workTimerStopped = false; // Track if timer was stopped (not just paused)
    private int _countdownSeconds = 1500; // Default 25 minutes = 1500 seconds
    private int _countdownRemainingSeconds = 1500;
    private bool _countdownTimerRunning = false;

    public TerminalPage()
    {
        try
        {
            Console.WriteLine("[TerminalPage] Constructor starting...");
            System.Diagnostics.Debug.WriteLine("[TerminalPage] Constructor starting...");

            InitializeComponent();
            Console.WriteLine("[TerminalPage] InitializeComponent completed");

            _issues = new ObservableCollection<IssueItem>();
            _filteredIssues = new ObservableCollection<IssueItem>();
            _groupedIssues = new ObservableCollection<GroupedIssueItem>();
            _allIssues = new List<IssueItem>();
            
            // Bind the filtered issues to the view
            IssuesListView.ItemsSource = _filteredIssues;
            IssuesListView.SelectionChanged += OnIssueSelectionChanged;
            Console.WriteLine("[TerminalPage] ListView configured");

            // Services will be initialized when the page is loaded
            Loaded += OnPageLoaded;

            // Initialize timers
            InitializeTimers();

            Console.WriteLine("[TerminalPage] Constructor completed successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TerminalPage] Constructor error: {ex}");
            System.Diagnostics.Debug.WriteLine($"[TerminalPage] Constructor error: {ex}");
            throw;
        }
    }

    private async void OnPageLoaded(object? sender, EventArgs e)
    {
        try
        {
            Console.WriteLine("[TerminalPage] OnPageLoaded starting...");
            System.Diagnostics.Debug.WriteLine("[TerminalPage] OnPageLoaded starting...");

            // Get services from DI after the page is loaded
            var serviceProvider = Handler?.MauiContext?.Services;
            Console.WriteLine($"[TerminalPage] ServiceProvider available: {serviceProvider != null}");

            if (serviceProvider != null)
            {
                try
                {
                    _configuration = serviceProvider.GetService<IConfiguration>();
                    Console.WriteLine($"[TerminalPage] Configuration loaded: {_configuration != null}");

                    _gitHubServiceManager = serviceProvider.GetService<IGitHubServiceManager>();
                    Console.WriteLine($"[TerminalPage] GitHubServiceManager loaded: {_gitHubServiceManager != null}");

                    _stateService = serviceProvider.GetService<IIssueStateService>();
                    Console.WriteLine($"[TerminalPage] StateService loaded: {_stateService != null}");

                    // Don't load these services yet - they need IGitHubService which requires CurrentRepositoryId to be set
                    // We'll load them after InitializeRepositorySelector sets the current repository
                    Console.WriteLine("[TerminalPage] Initial services loaded");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[TerminalPage] Error loading services: {ex}");
                    System.Diagnostics.Debug.WriteLine($"[TerminalPage] Error loading services: {ex}");
                    throw;
                }

                // Initialize command registry
                var initService = serviceProvider.GetService<ICommandInitializationService>();
                if (initService != null)
                {
                    Console.WriteLine("[TerminalPage] Initializing command registry...");
                    initService.Initialize();
                    Console.WriteLine("[TerminalPage] Command registry initialized");
                }

                // Initialize repository selector
                Console.WriteLine("[TerminalPage] Initializing repository selector...");
                InitializeRepositorySelector();

                // Setup config file watcher
                Console.WriteLine("[TerminalPage] Setting up config watcher...");
                SetupConfigWatcher();
            }
            else
            {
                Console.WriteLine("[TerminalPage] WARNING: ServiceProvider is null!");
            }

            // Initialize filter toggle appearance (OFF by default - commands hidden)
            Console.WriteLine("[TerminalPage] Initializing filter toggles...");
            if (CommandFilterToggle != null && CommandFilterToggleBorder != null)
            {
                CommandFilterToggleBorder.BackgroundColor = Color.FromArgb("#2A2A2A");
                CommandFilterToggleBorder.Stroke = Color.FromArgb("#4A9EFF");
                CommandFilterToggle.TextColor = Color.FromArgb("#808080");
            }

            // Initialize closed issues toggle appearance (ON by default - closed issues hidden)
            if (ClosedIssuesToggle != null && ClosedIssuesToggleBorder != null)
            {
                ClosedIssuesToggleBorder.BackgroundColor = Color.FromArgb("#4A9EFF");
                ClosedIssuesToggleBorder.Stroke = Color.FromArgb("#4A9EFF");
                ClosedIssuesToggle.TextColor = Color.FromArgb("#FFFFFF");
            }

            // Load issues after services are initialized
            Console.WriteLine("[TerminalPage] Loading issues...");
            LoadIssues();
            Console.WriteLine("[TerminalPage] OnPageLoaded completed successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TerminalPage] OnPageLoaded error: {ex}");
            System.Diagnostics.Debug.WriteLine($"[TerminalPage] OnPageLoaded error: {ex}");
        }
    }

    private async Task RefreshIssues()
    {
        Console.WriteLine("[RefreshIssues] Starting refresh");
        Console.WriteLine($"[RefreshIssues] Current groupByTag mode: {_groupByTag}");
        await LoadIssuesAsync();
        Console.WriteLine("[RefreshIssues] Completed");
    }

    private async void LoadIssues()
    {
        await LoadIssuesAsync();
    }

    private async Task LoadIssuesAsync()
    {
        Console.WriteLine($"[LoadIssuesAsync] Starting. GroupByTag: {_groupByTag}, HideClosedIssues: {_hideClosedIssues}");
        Console.WriteLine($"[LoadIssuesAsync] Collections - Issues: {_issues?.Count ?? 0}, AllIssues: {_allIssues?.Count ?? 0}, FilteredIssues: {_filteredIssues?.Count ?? 0}, GroupedIssues: {_groupedIssues?.Count ?? 0}");
        
        try
        {
            if (_gitHubService == null)
            {
                Console.WriteLine("[LoadIssuesAsync] GitHubService is null, returning");
                return;
            }

            ShowLoadingIndicator();
            
            Console.WriteLine("[LoadIssuesAsync] Clearing collections");
            _issues.Clear();
            _allIssues.Clear();
            _filteredIssues.Clear();
            Console.WriteLine($"[LoadIssuesAsync] Collections cleared. Issues: {_issues.Count}, AllIssues: {_allIssues.Count}, FilteredIssues: {_filteredIssues.Count}");

            // Always fetch and add open issues (limit to 15)
            Console.WriteLine("[LoadIssuesAsync] Fetching open issues from GitHub");
            var openIssues = await _gitHubService.GetIssuesAsync(Octokit.ItemStateFilter.Open);
            Console.WriteLine($"[LoadIssuesAsync] Fetched {openIssues?.Count ?? 0} open issues");
            
            int openIssuesAdded = 0;
            foreach (var issue in openIssues.Take(15))
            {
                Console.WriteLine($"[LoadIssuesAsync] Processing open issue #{issue.Number}");
                var issueItem = new IssueItem
                {
                    Number = issue.Number,
                    Title = issue.Title,
                    DisplayText = $"#{issue.Number} {(issue.Title.Length > 45 ? issue.Title.Substring(0, 45) + "..." : issue.Title)}",
                    IsClosed = false
                };
                
                // Try to extract task counts, status, priority, due date, and tags from embedded state
                var versionedState = _issueStateParser?.ParseVersionedState(issue);
                if (versionedState != null)
                {
                    var taskCounts = versionedState.Data.GetTaskCounts();
                    issueItem.OpenTaskCount = taskCounts.Open;
                    issueItem.TotalTaskCount = taskCounts.Total;
                    issueItem.Status = versionedState.Data.Status ?? "todo";
                    issueItem.Priority = versionedState.Data.Priority ?? "medium";
                    issueItem.DueDate = versionedState.Data.DueDate;
                    issueItem.Tags = versionedState.Data.Tags ?? new List<string>();
                    Console.WriteLine($"[LoadIssuesAsync] Issue #{issue.Number} has {issueItem.Tags.Count} tags: [{string.Join(", ", issueItem.Tags)}]");
                }
                
                // Apply priority filters (only for open issues)
                bool shouldInclude = issueItem.Priority switch
                {
                    "critical" => _filterCriticalPriority,
                    "high" => _filterHighPriority,
                    "medium" => _filterMediumPriority,
                    "low" => _filterLowPriority,
                    _ => true // Include if priority is unknown
                };
                
                Console.WriteLine($"[LoadIssuesAsync] Issue #{issue.Number} priority: {issueItem.Priority}, should include: {shouldInclude}");
                
                if (shouldInclude)
                {
                    _issues.Add(issueItem);
                    _allIssues.Add(issueItem);
                    openIssuesAdded++;
                    Console.WriteLine($"[LoadIssuesAsync] Added issue #{issue.Number} to collections. Issues count: {_issues.Count}, AllIssues count: {_allIssues.Count}");
                }
            }
            Console.WriteLine($"[LoadIssuesAsync] Added {openIssuesAdded} open issues to collections");

            // Only fetch and add closed issues if filter is OFF
            if (!_hideClosedIssues)
            {
                Console.WriteLine("[LoadIssuesAsync] Fetching closed issues from GitHub");
                var closedIssues = await _gitHubService.GetIssuesAsync(Octokit.ItemStateFilter.Closed);
                Console.WriteLine($"[LoadIssuesAsync] Fetched {closedIssues?.Count ?? 0} closed issues");
                
                int closedIssuesAdded = 0;
                foreach (var issue in closedIssues.Take(10))
                {
                    Console.WriteLine($"[LoadIssuesAsync] Processing closed issue #{issue.Number}");
                    var issueItem = new IssueItem
                    {
                        Number = issue.Number,
                        Title = issue.Title,
                        DisplayText = $"#{issue.Number} {(issue.Title.Length > 45 ? issue.Title.Substring(0, 45) + "..." : issue.Title)}",
                        IsClosed = true
                    };
                    
                    // Try to extract task counts, status, priority, due date, and tags from embedded state
                    var versionedState = _issueStateParser?.ParseVersionedState(issue);
                    if (versionedState != null)
                    {
                        var taskCounts = versionedState.Data.GetTaskCounts();
                        issueItem.OpenTaskCount = taskCounts.Open;
                        issueItem.TotalTaskCount = taskCounts.Total;
                        issueItem.Status = versionedState.Data.Status ?? "done";
                        issueItem.Priority = versionedState.Data.Priority ?? "medium";
                        issueItem.DueDate = versionedState.Data.DueDate;
                        issueItem.Tags = versionedState.Data.Tags ?? new List<string>();
                        Console.WriteLine($"[LoadIssuesAsync] Issue #{issue.Number} has {issueItem.Tags.Count} tags: [{string.Join(", ", issueItem.Tags)}]");
                    }
                    
                    // Closed issues are not filtered by priority
                    _issues.Add(issueItem);
                    _allIssues.Add(issueItem);
                    closedIssuesAdded++;
                    Console.WriteLine($"[LoadIssuesAsync] Added closed issue #{issue.Number}. Issues count: {_issues.Count}, AllIssues count: {_allIssues.Count}");
                }
                Console.WriteLine($"[LoadIssuesAsync] Added {closedIssuesAdded} closed issues to collections");
            }
            else
            {
                Console.WriteLine("[LoadIssuesAsync] Skipping closed issues (hideClosedIssues is true)");
            }

            Console.WriteLine($"[LoadIssuesAsync] Before ApplyFilter - Issues: {_issues.Count}, AllIssues: {_allIssues.Count}, FilteredIssues: {_filteredIssues.Count}");
            // Apply text filter
            ApplyFilter();
            Console.WriteLine($"[LoadIssuesAsync] After ApplyFilter - Issues: {_issues.Count}, AllIssues: {_allIssues.Count}, FilteredIssues: {_filteredIssues.Count}");

            if (_filteredIssues.Any())
            {
                var firstIssue = _filteredIssues.First();
                Console.WriteLine($"[LoadIssuesAsync] Selecting first filtered issue: #{firstIssue.Number}");
                IssuesListView.SelectedItem = firstIssue;
            }
            else
            {
                Console.WriteLine("[LoadIssuesAsync] No filtered issues to select");
            }
            
            Console.WriteLine($"[LoadIssuesAsync] Completed successfully. Final counts - Issues: {_issues.Count}, AllIssues: {_allIssues.Count}, FilteredIssues: {_filteredIssues.Count}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LoadIssuesAsync] ERROR: {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine($"[LoadIssuesAsync] Stack trace: {ex.StackTrace}");
            await DisplayAlert("Error", $"Failed to load issues: {ex.Message}", "OK");
        }
        finally
        {
            HideLoadingIndicator();
            Console.WriteLine("[LoadIssuesAsync] Finished");
        }
    }

    private async void OnIssueSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        try
        {
            Console.WriteLine($"[OnIssueSelectionChanged] Selection changed event fired");
            Console.WriteLine($"[OnIssueSelectionChanged] Group mode: {_groupByTag}");
            Console.WriteLine($"[OnIssueSelectionChanged] Previous selection count: {e.PreviousSelection?.Count ?? 0}");
            Console.WriteLine($"[OnIssueSelectionChanged] Current selection count: {e.CurrentSelection?.Count ?? 0}");
            
            if (e.CurrentSelection == null)
            {
                Console.WriteLine("[OnIssueSelectionChanged] CurrentSelection is null");
                return;
            }
            
            var selectedItem = e.CurrentSelection.FirstOrDefault();
            Console.WriteLine($"[OnIssueSelectionChanged] Selected item type: {selectedItem?.GetType()?.Name ?? "null"}");
            
            if (selectedItem is IssueItem issue)
            {
                Console.WriteLine($"[OnIssueSelectionChanged] Selected issue: #{issue.Number} - {issue.Title}");
                _selectedIssue = issue;
                
                Console.WriteLine($"[OnIssueSelectionChanged] Loading issue details for #{issue.Number}");
                await LoadIssueDetails(issue.Number);
                Console.WriteLine($"[OnIssueSelectionChanged] Issue details loaded for #{issue.Number}");
            }
            else if (selectedItem is GroupedIssueItem group)
            {
                // In case a group header is selected (shouldn't happen, but being defensive)
                Console.WriteLine($"[OnIssueSelectionChanged] Group selected: {group.GroupName} with {group.Count} items");
                // Don't load anything for group headers
            }
            else if (selectedItem != null)
            {
                Console.WriteLine($"[OnIssueSelectionChanged] Unexpected selection type: {selectedItem.GetType().FullName}");
            }
            else
            {
                Console.WriteLine("[OnIssueSelectionChanged] No item selected");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OnIssueSelectionChanged] ERROR: {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine($"[OnIssueSelectionChanged] Stack trace: {ex.StackTrace}");
        }
    }

    private async Task LoadIssueDetails(int issueNumber)
    {
        try
        {
            if (_gitHubService == null || _stateService == null || _issueStateParser == null) return;

            ShowLoadingIndicator();

            // Get the specific issue
            var issue = await _gitHubService.GetIssueAsync(issueNumber);
            if (issue == null) return;

            IssueState state;
            
            // First, try to get state from embedded version in issue body
            var versionedState = _issueStateParser.ParseVersionedState(issue);
            if (versionedState != null && !_stateManager.IsStateStale(versionedState, TimeSpan.FromMinutes(5)))
            {
                // Use the versioned state - no need to fetch comments!
                state = ConvertVersionedToState(versionedState.Data);
                System.Diagnostics.Debug.WriteLine($"Issue #{issue.Number}: Using embedded state (v{versionedState.StateVersion})");
            }
            else
            {
                // Fallback: Parse from comments (for issues without embedded state or stale state)
                System.Diagnostics.Debug.WriteLine($"Issue #{issue.Number}: Parsing state from comments (embedded state {(versionedState == null ? "not found" : "is stale")})");
                
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
                state = _issueStateParser.ParseIssueState(issue, comments);
                
                // Store comments for display later
                _allComments = comments.ToList();
            }

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

            // Display issue body/description (removing embedded state markers)
            var displayBody = issue.Body;
            if (!string.IsNullOrWhiteSpace(displayBody))
            {
                // Remove the embedded state section from display
                var statePattern = $@"{Regex.Escape(IssueStateVersion.StateBeginMarker)}.*?{Regex.Escape(IssueStateVersion.StateEndMarker)}";
                displayBody = Regex.Replace(displayBody, statePattern, "", RegexOptions.Singleline).Trim();
            }
            
            IssueDescription.Text = string.IsNullOrWhiteSpace(displayBody)
                ? "No description provided"
                : displayBody;

            // Update status and visual state
            // If issue is closed, status should be "done"
            if (issue.State == Octokit.ItemState.Closed)
            {
                _currentIssueStatus = "done";
            }
            else
            {
                _currentIssueStatus = state.Status ?? "todo";
            }
            UpdateStatusButtonVisuals(_currentIssueStatus);

            StatusLabel.Text = _currentIssueStatus;

            // Update priority and visual state
            _currentIssuePriority = state.Priority ?? "medium";
            UpdatePriorityButtonVisuals(_currentIssuePriority);

            PriorityLabel.Text = _currentIssuePriority;
            AssigneeLabel.Text = state.Assignee ?? "unassigned";
            DueLabel.Text = state.DueDate?.ToString("yyyy-MM-dd") ?? "not set";
            EstimateLabel.Text = FormatEstimate(state.Estimate);

            // Update tags display
            UpdateTagsDisplay(state.Tags);

            // Update time tracking calendar
            UpdateTimeTrackingCalendar(state);

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


            // Display comments - only fetch if not already loaded
            if (_allComments == null || !_allComments.Any())
            {
                _allComments = (await _gitHubService.GetIssueCommentsAsync(issueNumber)).ToList();
            }
            DisplayComments();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to load issue details: {ex.Message}", "OK");
        }
        finally
        {
            HideLoadingIndicator();
        }
    }

    private void UpdateTagsDisplay(List<string> tags)
    {
        // Clear existing tags
        TagsContainer.Children.Clear();

        if (tags != null && tags.Any())
        {
            foreach (var tag in tags)
            {
                // Create tag pill container with rounded corners
                var tagBorder = new Border
                {
                    BackgroundColor = Color.FromArgb("#1A4A9E"),
                    StrokeThickness = 1,
                    Stroke = Color.FromArgb("#4A9EFF"),
                    Padding = new Thickness(8, 2, 4, 2),
                    Margin = new Thickness(0, 0, 5, 0),
                    HorizontalOptions = LayoutOptions.Start,
                    StrokeShape = new RoundRectangle
                    {
                        CornerRadius = new CornerRadius(10)
                    }
                };

                // Create horizontal stack for tag text and remove button
                var tagStack = new HorizontalStackLayout
                {
                    Spacing = 5
                };

                // Tag text
                var tagLabel = new Label
                {
                    Text = tag,
                    TextColor = Color.FromArgb("#E0E0E0"),
                    FontFamily = "CascadiaMono",
                    FontSize = 12,
                    VerticalTextAlignment = TextAlignment.Center
                };
                tagStack.Children.Add(tagLabel);

                // Remove button (×)
                var removeLabel = new Label
                {
                    Text = "×",
                    TextColor = Color.FromArgb("#808080"),
                    FontFamily = "CascadiaMono",
                    FontSize = 14,
                    VerticalTextAlignment = TextAlignment.Center
                };

                var removeTapGesture = new TapGestureRecognizer();
                removeTapGesture.Tapped += async (s, e) => await OnRemoveTag(tag);
                removeLabel.GestureRecognizers.Add(removeTapGesture);
                tagStack.Children.Add(removeLabel);

                tagBorder.Content = tagStack;
                TagsContainer.Children.Add(tagBorder);
            }
        }
    }

    private async Task OnRemoveTag(string tag)
    {
        if (_selectedIssue == null || string.IsNullOrWhiteSpace(tag)) return;

        try
        {
            if (_commandExecutor == null || _commandParser == null || _commandPostingService == null)
            {
                await DisplayAlert("Error", "Services not initialized", "OK");
                return;
            }

            string command = $"/notnow tags remove {tag}";

            // Parse command
            var parseResult = _commandParser.Parse(command, CommandContext.Comment);
            if (!parseResult.Commands.Any())
            {
                await DisplayAlert("Error", "Failed to parse remove tag command", "OK");
                return;
            }

            // Execute command
            var context = new CommandExecutionContext
            {
                IssueNumber = _selectedIssue.Number,
                User = "current-user",
                CommandContext = CommandContext.Comment,
                RawText = command
            };

            var result = await _commandExecutor.ExecuteCommandsAsync(parseResult.Commands, context);

            if (result.Success)
            {
                // Post the command to GitHub
                await PostCommandAndUpdateState(_selectedIssue.Number, command, result);

                // Add a small delay to ensure GitHub has processed the command
                await Task.Delay(_configuration?.GetValue<int>("PostCommandDelay", 1000) ?? 1000);

                // Update the tags in the issue item in memory
                var issueInList = _allIssues.FirstOrDefault(i => i.Number == _selectedIssue.Number);
                if (issueInList != null && issueInList.Tags.Contains(tag))
                {
                    issueInList.Tags.Remove(tag);
                    
                    // If we're in grouped mode, refresh the display to update grouping
                    if (_groupByTag)
                    {
                        Console.WriteLine($"[OnRemoveTag] Tag '{tag}' removed from issue #{_selectedIssue.Number}, refreshing grouped display");
                        ApplyFilter(); // This will call UpdateIssuesDisplay()
                    }
                }

                // Reload issue details to show updated tags
                await LoadIssueDetails(_selectedIssue.Number);
            }
            else
            {
                await DisplayAlert("Error", $"Failed to remove tag: {result.Summary}", "OK");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OnRemoveTag] ERROR: {ex.Message}");
            await DisplayAlert("Error", $"Failed to remove tag: {ex.Message}", "OK");
        }
    }

    private void OnAddTagClicked(object sender, EventArgs e)
    {
        // Show the tag input panel
        TagNameInput.Text = "";
        TagInputPanel.IsVisible = true;
        TagNameInput.Focus();
    }

    private void OnCancelTagInput(object sender, EventArgs e)
    {
        // Hide the tag input panel
        TagInputPanel.IsVisible = false;
        TagNameInput.Text = "";
    }

    private async void OnSubmitTag(object sender, EventArgs e)
    {
        if (_selectedIssue == null || string.IsNullOrWhiteSpace(TagNameInput.Text)) return;

        try
        {
            if (_commandExecutor == null || _commandParser == null || _commandPostingService == null)
            {
                await DisplayAlert("Error", "Services not initialized", "OK");
                return;
            }

            string tag = TagNameInput.Text.Trim();
            string command = $"/notnow tags add {tag}";

            // Parse command
            var parseResult = _commandParser.Parse(command, CommandContext.Comment);
            if (!parseResult.Commands.Any())
            {
                await DisplayAlert("Error", "Failed to parse add tag command", "OK");
                return;
            }

            // Execute command
            var context = new CommandExecutionContext
            {
                IssueNumber = _selectedIssue.Number,
                User = "current-user",
                CommandContext = CommandContext.Comment,
                RawText = command
            };

            var result = await _commandExecutor.ExecuteCommandsAsync(parseResult.Commands, context);

            if (result.Success)
            {
                // Post the command to GitHub
                await PostCommandAndUpdateState(_selectedIssue.Number, command, result);

                // Hide the input panel
                TagInputPanel.IsVisible = false;
                TagNameInput.Text = "";

                // Add a small delay to ensure GitHub has processed the command
                await Task.Delay(_configuration?.GetValue<int>("PostCommandDelay", 1000) ?? 1000);

                // Update the tags in the issue item in memory
                var issueInList = _allIssues.FirstOrDefault(i => i.Number == _selectedIssue.Number);
                if (issueInList != null && !issueInList.Tags.Contains(tag))
                {
                    issueInList.Tags.Add(tag);
                    
                    // If we're in grouped mode, refresh the display to update grouping
                    if (_groupByTag)
                    {
                        Console.WriteLine($"[OnSubmitTag] Tag '{tag}' added to issue #{_selectedIssue.Number}, refreshing grouped display");
                        ApplyFilter(); // This will call UpdateIssuesDisplay()
                    }
                }

                // Reload issue details to show updated tags
                await LoadIssueDetails(_selectedIssue.Number);
            }
            else
            {
                await DisplayAlert("Error", $"Failed to add tag: {result.Summary}", "OK");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OnSubmitTag] ERROR: {ex.Message}");
            await DisplayAlert("Error", $"Failed to add tag: {ex.Message}", "OK");
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

                // Display full comment body without truncation - using Editor for text selection
                var bodyEditor = new Editor
                {
                    Text = comment.Body,
                    TextColor = Color.FromArgb("#E0E0E0"),
                    FontFamily = "CascadiaMono",
                    FontSize = 13,
                    Margin = new Thickness(0, 2),
                    IsReadOnly = true,
                    BackgroundColor = Colors.Transparent,
                    VerticalOptions = LayoutOptions.Start,
                    AutoSize = EditorAutoSizeOption.TextChanges
                };
                Grid.SetRow(bodyEditor, 1);
                commentGrid.Children.Add(bodyEditor);

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

    private async void OnClosedIssuesToggled(object? sender, EventArgs e)
    {
        _hideClosedIssues = !_hideClosedIssues;

        // Update toggle appearance
        if (ClosedIssuesToggle != null && ClosedIssuesToggleBorder != null)
        {
            if (_hideClosedIssues)
            {
                // ON state - hiding closed issues (default)
                ClosedIssuesToggleBorder.BackgroundColor = Color.FromArgb("#4A9EFF");
                ClosedIssuesToggleBorder.Stroke = Color.FromArgb("#4A9EFF");
                ClosedIssuesToggle.TextColor = Color.FromArgb("#FFFFFF");
            }
            else
            {
                // OFF state - showing closed issues
                ClosedIssuesToggleBorder.BackgroundColor = Color.FromArgb("#2A2A2A");
                ClosedIssuesToggleBorder.Stroke = Color.FromArgb("#4A9EFF");
                ClosedIssuesToggle.TextColor = Color.FromArgb("#808080");
            }
        }

        // Reload issues with new filter
        await LoadIssuesAsync();
    }

    private async void OnGroupByTagToggled(object? sender, EventArgs e)
    {
        try
        {
            Console.WriteLine($"[OnGroupByTagToggled] Starting toggle. Current state: {_groupByTag}");
            _groupByTag = !_groupByTag;
            Console.WriteLine($"[OnGroupByTagToggled] New state: {_groupByTag}");

            // Update toggle appearance
            if (GroupByTagToggle != null && GroupByTagToggleBorder != null)
            {
                Console.WriteLine("[OnGroupByTagToggled] Updating toggle appearance");
                if (_groupByTag)
                {
                    // ON state - grouping by tags
                    GroupByTagToggleBorder.BackgroundColor = Color.FromArgb("#4A9EFF");
                    GroupByTagToggleBorder.Stroke = Color.FromArgb("#4A9EFF");
                    GroupByTagToggle.TextColor = Color.FromArgb("#FFFFFF");
                    ToolTipProperties.SetText(GroupByTagToggle, "Flat view");
                }
                else
                {
                    // OFF state - flat list
                    GroupByTagToggleBorder.BackgroundColor = Color.FromArgb("#2A2A2A");
                    GroupByTagToggleBorder.Stroke = Color.FromArgb("#4A9EFF");
                    GroupByTagToggle.TextColor = Color.FromArgb("#4A9EFF");
                    ToolTipProperties.SetText(GroupByTagToggle, "Group by tags");
                }
                Console.WriteLine("[OnGroupByTagToggled] Toggle appearance updated");
            }
            else
            {
                Console.WriteLine($"[OnGroupByTagToggled] WARNING: GroupByTagToggle is null: {GroupByTagToggle == null}, GroupByTagToggleBorder is null: {GroupByTagToggleBorder == null}");
            }

            // Reorganize the display
            Console.WriteLine("[OnGroupByTagToggled] Calling UpdateIssuesDisplay");
            UpdateIssuesDisplay();
            Console.WriteLine("[OnGroupByTagToggled] UpdateIssuesDisplay completed");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OnGroupByTagToggled] ERROR: {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine($"[OnGroupByTagToggled] Stack trace: {ex.StackTrace}");
            await DisplayAlert("Error", $"Failed to toggle group view: {ex.Message}", "OK");
        }
    }

    private void OnSortModeToggled(object? sender, EventArgs e)
    {
        try
        {
            Console.WriteLine($"[OnSortModeToggled] Current sort mode: {_sortMode}");
            
            // Cycle through sort modes
            _sortMode = _sortMode switch
            {
                SortMode.IssueNumberDescending => SortMode.DueDateAscending,
                SortMode.DueDateAscending => SortMode.DueDateDescending,
                SortMode.DueDateDescending => SortMode.IssueNumberDescending,
                _ => SortMode.IssueNumberDescending
            };
            
            Console.WriteLine($"[OnSortModeToggled] New sort mode: {_sortMode}");
            
            // Update button appearance based on sort mode
            if (SortModeToggle != null && SortModeToggleBorder != null)
            {
                switch (_sortMode)
                {
                    case SortMode.IssueNumberDescending:
                        SortModeToggle.Text = "\ue164"; // sort icon
                        ToolTipProperties.SetText(SortModeToggle, "Sort by: Issue # (newest first)");
                        break;
                    case SortMode.DueDateAscending:
                        SortModeToggle.Text = "\ue5db"; // sort ascending icon
                        ToolTipProperties.SetText(SortModeToggle, "Sort by: Due Date (earliest first)");
                        break;
                    case SortMode.DueDateDescending:
                        SortModeToggle.Text = "\ue5d8"; // sort descending icon  
                        ToolTipProperties.SetText(SortModeToggle, "Sort by: Due Date (latest first)");
                        break;
                }
            }
            
            // Re-apply filter with new sort
            ApplyFilter();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OnSortModeToggled] ERROR: {ex.Message}");
        }
    }

    private void UpdateIssuesDisplay()
    {
        try
        {
            Console.WriteLine($"[UpdateIssuesDisplay] Starting. Group by tag: {_groupByTag}");
            Console.WriteLine($"[UpdateIssuesDisplay] Filtered issues count: {_filteredIssues?.Count ?? -1}");
            Console.WriteLine($"[UpdateIssuesDisplay] IssuesListView null? {IssuesListView == null}");
            Console.WriteLine($"[UpdateIssuesDisplay] IssuesListView.IsGrouped: {IssuesListView?.IsGrouped ?? false}");
            Console.WriteLine($"[UpdateIssuesDisplay] Current ItemsSource type: {IssuesListView?.ItemsSource?.GetType()?.Name ?? "null"}");
            
            // Ensure we're on the UI thread
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    // Validate state before making changes
                    if (IssuesListView == null)
                    {
                        Console.WriteLine("[UpdateIssuesDisplay] ERROR: IssuesListView is null, cannot update display");
                        return;
                    }
                    
                    if (_filteredIssues == null)
                    {
                        Console.WriteLine("[UpdateIssuesDisplay] ERROR: _filteredIssues is null, initializing empty collection");
                        _filteredIssues = new ObservableCollection<IssueItem>();
                    }
                    
                    if (_groupByTag)
                    {
                        Console.WriteLine("[UpdateIssuesDisplay] Entering grouped mode");
                        
                        // CRITICAL: Unbind ItemsSource BEFORE modifying the collection
                        Console.WriteLine("[UpdateIssuesDisplay] Unbinding ItemsSource before collection modifications");
                        IssuesListView.ItemsSource = null;
                        
                        // Group issues by their first tag
                        if (_groupedIssues == null)
                        {
                            Console.WriteLine("[UpdateIssuesDisplay] Creating new grouped issues collection");
                            _groupedIssues = new ObservableCollection<GroupedIssueItem>();
                        }
                        else
                        {
                            // Clear the collection while it's not bound
                            Console.WriteLine("[UpdateIssuesDisplay] Clearing grouped issues collection");
                            _groupedIssues.Clear();
                        }
                        
                        var groupDictionary = new Dictionary<string, List<IssueItem>>();
                        
                        Console.WriteLine($"[UpdateIssuesDisplay] Processing {_filteredIssues.Count} filtered issues");
                        foreach (var issue in _filteredIssues)
                        {
                            if (issue == null)
                            {
                                Console.WriteLine("[UpdateIssuesDisplay] WARNING: Null issue in _filteredIssues, skipping");
                                continue;
                            }
                            
                            string groupKey = "Ungrouped";
                            
                            // Get first tag if available
                            if (issue.Tags != null && issue.Tags.Any())
                            {
                                groupKey = issue.Tags.First();
                                Console.WriteLine($"[UpdateIssuesDisplay] Issue #{issue.Number} grouped under '{groupKey}'");
                            }
                            else
                            {
                                Console.WriteLine($"[UpdateIssuesDisplay] Issue #{issue.Number} has no tags, placing in 'Ungrouped'");
                            }
                            
                            // Create group list if it doesn't exist
                            if (!groupDictionary.ContainsKey(groupKey))
                            {
                                Console.WriteLine($"[UpdateIssuesDisplay] Creating new group list for: {groupKey}");
                                groupDictionary[groupKey] = new List<IssueItem>();
                            }
                            
                            // Add issue to the group list
                            groupDictionary[groupKey].Add(issue);
                        }
                        
                        Console.WriteLine($"[UpdateIssuesDisplay] Created {groupDictionary.Count} groups");
                        
                        // Create GroupedIssueItem instances and sort
                        var sortedGroups = groupDictionary
                            .Select(kvp => new GroupedIssueItem(kvp.Key, kvp.Value))
                            .OrderBy(g => g.GroupName == "Ungrouped" ? 1 : 0)
                            .ThenBy(g => g.GroupName)
                            .ToList(); // Materialize the query
                        
                        Console.WriteLine("[UpdateIssuesDisplay] Adding groups to collection");
                        foreach (var group in sortedGroups)
                        {
                            Console.WriteLine($"[UpdateIssuesDisplay] Adding group '{group.GroupName}' with {group.Count} issues");
                            try
                            {
                                _groupedIssues.Add(group);
                            }
                            catch (Exception addEx)
                            {
                                Console.WriteLine($"[UpdateIssuesDisplay] ERROR adding group '{group.GroupName}': {addEx.Message}");
                                throw;
                            }
                        }
                        
                        Console.WriteLine($"[UpdateIssuesDisplay] All groups added. Total groups: {_groupedIssues.Count}");
                        
                        // Now set up the CollectionView for grouped display
                        try
                        {
                            // Set IsGrouped BEFORE setting ItemsSource
                            Console.WriteLine("[UpdateIssuesDisplay] Setting IsGrouped to true");
                            IssuesListView.IsGrouped = true;
                            
                            Console.WriteLine($"[UpdateIssuesDisplay] Setting ItemsSource to _groupedIssues with {_groupedIssues.Count} groups");
                            IssuesListView.ItemsSource = _groupedIssues;
                            
                            Console.WriteLine("[UpdateIssuesDisplay] Verifying ItemsSource was set");
                            var newSource = IssuesListView.ItemsSource;
                            Console.WriteLine($"[UpdateIssuesDisplay] New ItemsSource type: {newSource?.GetType()?.Name ?? "null"}");
                            Console.WriteLine($"[UpdateIssuesDisplay] IssuesListView.IsGrouped after setting: {IssuesListView.IsGrouped}");
                            
                            Console.WriteLine("[UpdateIssuesDisplay] Grouped mode setup complete");
                        }
                        catch (Exception groupEx)
                        {
                            Console.WriteLine($"[UpdateIssuesDisplay] ERROR setting grouped ItemsSource: {groupEx.GetType().Name}: {groupEx.Message}");
                            Console.WriteLine($"[UpdateIssuesDisplay] Inner exception: {groupEx.InnerException?.Message}");
                            throw;
                        }
                    }
                    else
                    {
                        Console.WriteLine("[UpdateIssuesDisplay] Entering flat mode");
                        
                        // CRITICAL: Unbind ItemsSource BEFORE making changes
                        Console.WriteLine("[UpdateIssuesDisplay] Unbinding ItemsSource before switching to flat mode");
                        IssuesListView.ItemsSource = null;
                        
                        try
                        {
                            // Set IsGrouped BEFORE setting ItemsSource
                            Console.WriteLine("[UpdateIssuesDisplay] Setting IsGrouped to false");
                            IssuesListView.IsGrouped = false;
                            
                            Console.WriteLine($"[UpdateIssuesDisplay] Setting ItemsSource to _filteredIssues with {_filteredIssues.Count} items");
                            IssuesListView.ItemsSource = _filteredIssues;
                            
                            Console.WriteLine("[UpdateIssuesDisplay] Verifying ItemsSource was set");
                            var newSource = IssuesListView.ItemsSource;
                            Console.WriteLine($"[UpdateIssuesDisplay] New ItemsSource type: {newSource?.GetType()?.Name ?? "null"}");
                            Console.WriteLine($"[UpdateIssuesDisplay] IssuesListView.IsGrouped after setting: {IssuesListView.IsGrouped}");
                            
                            Console.WriteLine("[UpdateIssuesDisplay] Flat mode setup complete");
                        }
                        catch (Exception flatEx)
                        {
                            Console.WriteLine($"[UpdateIssuesDisplay] ERROR setting flat ItemsSource: {flatEx.GetType().Name}: {flatEx.Message}");
                            Console.WriteLine($"[UpdateIssuesDisplay] Inner exception: {flatEx.InnerException?.Message}");
                            throw;
                        }
                    }
                    
                    Console.WriteLine("[UpdateIssuesDisplay] Completed successfully");
                }
                catch (Exception innerEx)
                {
                    Console.WriteLine($"[UpdateIssuesDisplay] ERROR on UI thread: {innerEx.GetType().Name}: {innerEx.Message}");
                    Console.WriteLine($"[UpdateIssuesDisplay] Stack trace: {innerEx.StackTrace}");
                    Console.WriteLine($"[UpdateIssuesDisplay] Inner exception: {innerEx.InnerException?.Message}");
                    
                    // Try to recover by switching back to flat mode
                    try
                    {
                        Console.WriteLine("[UpdateIssuesDisplay] Attempting recovery - switching to flat mode");
                        _groupByTag = false;
                        if (IssuesListView != null && _filteredIssues != null)
                        {
                            IssuesListView.ItemsSource = null;
                            IssuesListView.IsGrouped = false;
                            IssuesListView.ItemsSource = _filteredIssues;
                        }
                        
                        // Update toggle button appearance
                        if (GroupByTagToggle != null && GroupByTagToggleBorder != null)
                        {
                            GroupByTagToggleBorder.BackgroundColor = Color.FromArgb("#2A2A2A");
                            GroupByTagToggleBorder.Stroke = Color.FromArgb("#4A9EFF");
                            GroupByTagToggle.TextColor = Color.FromArgb("#4A9EFF");
                            ToolTipProperties.SetText(GroupByTagToggle, "Group by tags");
                        }
                    }
                    catch (Exception recoveryEx)
                    {
                        Console.WriteLine($"[UpdateIssuesDisplay] Recovery failed: {recoveryEx.Message}");
                    }
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UpdateIssuesDisplay] ERROR: {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine($"[UpdateIssuesDisplay] Stack trace: {ex.StackTrace}");
            Console.WriteLine($"[UpdateIssuesDisplay] Inner exception: {ex.InnerException?.Message}");
        }
    }

    private void OnGroupHeaderTapped(object? sender, EventArgs e)
    {
        try
        {
            Console.WriteLine("[OnGroupHeaderTapped] Header tapped");
            
            if (sender == null)
            {
                Console.WriteLine("[OnGroupHeaderTapped] Sender is null");
                return;
            }
            
            if (sender is Label label)
            {
                Console.WriteLine($"[OnGroupHeaderTapped] Sender is Label, BindingContext type: {label.BindingContext?.GetType().Name ?? "null"}");
                
                if (label.BindingContext is GroupedIssueItem group)
                {
                    Console.WriteLine($"[OnGroupHeaderTapped] Group '{group.GroupName}' tapped with {group.Count} issues");
                    // For now, groups are always expanded
                    // In future, could implement expand/collapse functionality
                }
                else
                {
                    Console.WriteLine($"[OnGroupHeaderTapped] BindingContext is not GroupedIssueItem, it's: {label.BindingContext?.GetType().Name ?? "null"}");
                }
            }
            else
            {
                Console.WriteLine($"[OnGroupHeaderTapped] Sender is not Label, it's: {sender.GetType().Name}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OnGroupHeaderTapped] ERROR: {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine($"[OnGroupHeaderTapped] Stack trace: {ex.StackTrace}");
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

    private string FormatEstimate(string? estimate)
    {
        if (string.IsNullOrWhiteSpace(estimate))
            return "not set";

        double totalMinutes = 0;

        // First check if it's in HH:mm format (e.g., "01:30")
        var timeFormatRegex = new Regex(@"^(\d{1,2}):(\d{2})$");
        var timeMatch = timeFormatRegex.Match(estimate);

        if (timeMatch.Success)
        {
            var hoursValue = int.Parse(timeMatch.Groups[1].Value);
            var minutesValue = int.Parse(timeMatch.Groups[2].Value);
            totalMinutes = hoursValue * 60 + minutesValue;
        }
        else
        {
            // Parse estimate string (e.g., "2h", "1.5h", "90m", "1d", "8h30m")
            var regex = new Regex(@"(\d+(?:\.\d+)?)\s*([dhm])", RegexOptions.IgnoreCase);
            var matches = regex.Matches(estimate);

            if (matches.Count == 0)
                return estimate; // Return as-is if we can't parse it

            foreach (Match match in matches)
            {
                var value = double.Parse(match.Groups[1].Value);
                var unit = match.Groups[2].Value.ToLower();

                switch (unit)
                {
                    case "d":
                        totalMinutes += value * 8 * 60; // 8 hours per day
                        break;
                    case "h":
                        totalMinutes += value * 60;
                        break;
                    case "m":
                        totalMinutes += value;
                        break;
                }
            }
        }

        // Convert total minutes to days, hours, minutes
        var days = (int)(totalMinutes / (8 * 60));
        var remainingMinutes = totalMinutes - (days * 8 * 60);
        var hours = (int)(remainingMinutes / 60);
        var minutes = (int)(remainingMinutes % 60);

        var parts = new List<string>();
        if (days > 0)
            parts.Add($"{days}d");
        if (hours > 0)
            parts.Add($"{hours}h");
        if (minutes > 0)
            parts.Add($"{minutes}m");

        return parts.Count > 0 ? string.Join("", parts) : "0m";
    }

    private void UpdateTimeTrackingCalendar(Core.Models.IssueState state)
    {
        WeekRowsContainer.Children.Clear();
        
        var today = DateTime.UtcNow.Date;
        var currentWeekStart = today.AddDays(-(int)today.DayOfWeek); // Sunday of current week
        var fourWeeksAgo = currentWeekStart.AddDays(-21); // Start 3 weeks before current week
        
        // Group sessions by date (using EndedAt for completed sessions, StartedAt for active ones)
        var sessionsByDate = state.Sessions
            .Where(s => (s.EndedAt ?? s.StartedAt).Date >= fourWeeksAgo)
            .GroupBy(s => (s.EndedAt ?? s.StartedAt).Date)
            .ToDictionary(g => g.Key, g => TimeSpan.FromSeconds(g.Sum(s => s.Duration.TotalSeconds)));
        
        decimal totalHours = 0;
        int daysWithTime = 0;
        
        // Generate 4 weeks
        for (int weekOffset = -3; weekOffset <= 0; weekOffset++)
        {
            var weekStart = currentWeekStart.AddDays(weekOffset * 7);
            var isCurrentWeek = weekOffset == 0;
            
            var weekGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition { Width = 60 },  // Date label column
                    new ColumnDefinition { Width = 50 },  // Day columns - wider for better readability
                    new ColumnDefinition { Width = 50 },
                    new ColumnDefinition { Width = 50 },
                    new ColumnDefinition { Width = 50 },
                    new ColumnDefinition { Width = 50 },
                    new ColumnDefinition { Width = 50 },
                    new ColumnDefinition { Width = 50 },
                    new ColumnDefinition { Width = new GridLength(15) },  // Separator
                    new ColumnDefinition { Width = new GridLength(80) }   // Week total
                }
            };

            // Add week start date label (MM/DD)
            var weekDateLabel = new Label
            {
                Text = weekStart.ToString("MM/dd"),
                TextColor = Color.FromArgb("#808080"),
                FontFamily = "CascadiaMono",
                FontSize = 12,  // Larger font
                HorizontalTextAlignment = TextAlignment.End,
                VerticalTextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 5, 0)
            };
            Grid.SetColumn(weekDateLabel, 0);
            weekGrid.Children.Add(weekDateLabel);
            
            decimal weekTotal = 0;
            
            // Add each day of the week
            for (int day = 0; day < 7; day++)
            {
                var date = weekStart.AddDays(day);
                var isFuture = date > today;
                var isToday = date == today;
                
                string timeText = "-";
                Color textColor = Color.FromArgb("#606060");
                
                if (sessionsByDate.TryGetValue(date, out var dayTime))
                {
                    var hours = (decimal)dayTime.TotalHours;
                    weekTotal += hours;
                    totalHours += hours;
                    daysWithTime++;
                    
                    // Format hours compactly
                    if (hours >= 1)
                    {
                        timeText = hours.ToString("0.#");
                    }
                    else if (hours > 0)
                    {
                        // Show minutes for times under 1 hour
                        var minutes = (int)(hours * 60);
                        timeText = $"{minutes}m";
                    }
                    
                    textColor = Color.FromArgb("#FFFFFF");
                }
                
                // Add brackets for current/future days in current week
                if (isCurrentWeek && (isToday || isFuture) && timeText != "-")
                {
                    timeText = $"[{timeText}]";
                    textColor = Color.FromArgb("#4A9EFF");
                }
                else if (isToday && timeText == "-")
                {
                    timeText = "[.]";
                    textColor = Color.FromArgb("#4A9EFF");
                }
                
                var dayLabel = new Label
                {
                    Text = timeText,
                    TextColor = textColor,
                    FontFamily = "CascadiaMono",
                    FontSize = 13,  // Larger font
                    HorizontalTextAlignment = TextAlignment.Center,
                    VerticalTextAlignment = TextAlignment.Center
                };
                
                Grid.SetColumn(dayLabel, day + 1);  // +1 because column 0 is now the date label
                weekGrid.Children.Add(dayLabel);
            }

            // Add separator
            var separator = new Label
            {
                Text = "│",
                TextColor = Color.FromArgb("#404040"),
                FontFamily = "CascadiaMono",
                FontSize = 13,  // Larger font
                HorizontalTextAlignment = TextAlignment.Center
            };
            Grid.SetColumn(separator, 8);  // Column 8 now (was 7)
            weekGrid.Children.Add(separator);

            // Add week total
            var weekTotalLabel = new Label
            {
                Text = weekTotal > 0 ? $"{weekTotal:0.#}h" : "-",
                TextColor = isCurrentWeek ? Color.FromArgb("#4A9EFF") : Color.FromArgb("#FFFFFF"),
                FontFamily = "CascadiaMono",
                FontSize = 13,  // Larger font
                HorizontalTextAlignment = TextAlignment.Start
            };

            if (isCurrentWeek && weekTotal > 0)
            {
                weekTotalLabel.Text += " ←";
            }

            Grid.SetColumn(weekTotalLabel, 9);  // Column 9 now (was 8)
            weekGrid.Children.Add(weekTotalLabel);
            
            WeekRowsContainer.Children.Add(weekGrid);
        }
        
        // Update total only (removed average per day)
        TotalTimeLabel.Text = $"Total: {totalHours:0.#}h";
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

            if (currentStatus == "done")
            {
                // Reopen the subtask directly (no form needed)
                string command = $"/notnow reopen {subtaskId}";

                // Parse command
                var parseResult = _commandParser.Parse(command, CommandContext.Comment);
                if (!parseResult.Commands.Any())
                {
                    await DisplayAlert("Error", "Failed to parse reopen command", "OK");
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
                    await PostCommandAndUpdateState(_selectedIssue.Number, command, result);

                    // Add a small delay to ensure GitHub has processed the command
                    await Task.Delay(_configuration?.GetValue<int>("PostCommandDelay", 1000) ?? 1000);

                    // Reload issue details to show updated subtask
                    await LoadIssueDetails(_selectedIssue.Number);
                }
                else
                {
                    await DisplayAlert("Error", $"Failed to reopen subtask: {result.Summary}", "OK");
                }
            }
            else
            {
                // Show completion form for pending => done
                _pendingSubtaskId = subtaskId;

                // Clear and show the completion panel
                CompletionTimeInput.Text = "";
                CompletionNotesInput.Text = "";
                SubtaskCompletionPanel.IsVisible = true;
                CompletionTimeInput.Focus();
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to handle subtask: {ex.Message}", "OK");
        }
    }

    private void OnLogTimeButtonClicked(object sender, EventArgs e)
    {
        // Show the log time form
        LogTimePanel.IsVisible = true;
        
        // Clear previous inputs
        LogTimeDurationInput.Text = "";
        LogTimeDescriptionInput.Text = "";
        LogTimeDateInput.Text = "";
        
        // Focus on duration input
        LogTimeDurationInput.Focus();
    }

    private async void OnLogTimeConfirm(object sender, EventArgs e)
    {
        if (_selectedIssue == null) return;

        // Validate duration is provided
        string duration = LogTimeDurationInput.Text?.Trim();
        if (string.IsNullOrWhiteSpace(duration))
        {
            await DisplayAlert("Error", "Duration is required", "OK");
            return;
        }

        // Validate duration format (e.g., 1d, 8h, 30m, 1d8h, 8h30m, 1d8h30m)
        var durationRegex = new Regex(@"^(?:(\d+)d)?(?:(\d+)h)?(?:(\d+)m)?$", RegexOptions.IgnoreCase);
        var match = durationRegex.Match(duration);

        if (!match.Success || match.Value == "")
        {
            await DisplayAlert("Error", "Duration must be in format like: 1d, 8h, 30m, 1d8h, 8h30m, or 1d8h30m", "OK");
            return;
        }

        // Check that at least one time unit was specified
        var hasValue = match.Groups[1].Success || match.Groups[2].Success || match.Groups[3].Success;
        if (!hasValue)
        {
            await DisplayAlert("Error", "Duration must include at least one time unit (d, h, or m)", "OK");
            return;
        }

        // Validate reasonable values
        var days = match.Groups[1].Success ? int.Parse(match.Groups[1].Value) : 0;
        var hours = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 0;
        var minutes = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;

        if (days > 365)
        {
            await DisplayAlert("Error", "Duration days cannot exceed 365", "OK");
            return;
        }
        if (hours > 23)
        {
            await DisplayAlert("Error", "Duration hours cannot exceed 23", "OK");
            return;
        }
        if (minutes > 59)
        {
            await DisplayAlert("Error", "Duration minutes cannot exceed 59", "OK");
            return;
        }

        try
        {
            // Check if services are initialized
            if (_commandParser == null)
            {
                await DisplayAlert("Error", "Command parser is not initialized", "OK");
                return;
            }
            if (_commandExecutor == null)
            {
                await DisplayAlert("Error", "Command executor is not initialized", "OK");
                return;
            }
            if (_commandPostingService == null)
            {
                await DisplayAlert("Error", "Command posting service is not initialized", "OK");
                return;
            }

            // Build the time command
            var commandBuilder = new System.Text.StringBuilder();
            commandBuilder.Append($"/notnow time {duration}");

            // Add description if provided
            string description = LogTimeDescriptionInput.Text?.Trim();
            if (!string.IsNullOrWhiteSpace(description))
            {
                // Escape quotes in description
                string escapedDescription = description.Replace("\"", "\\\"");
                commandBuilder.Append($" --description \"{escapedDescription}\"");
            }

            // Add date if provided
            string date = LogTimeDateInput.Text?.Trim();
            if (!string.IsNullOrWhiteSpace(date))
            {
                commandBuilder.Append($" --date {date}");
            }

            string command = commandBuilder.ToString();

            // Parse command
            var parseResult = _commandParser.Parse(command, CommandContext.Comment);
            if (!parseResult.Commands.Any())
            {
                await DisplayAlert("Error", "Failed to parse time command", "OK");
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
                await PostCommandAndUpdateState(_selectedIssue.Number, command, result);

                // Hide the form
                LogTimePanel.IsVisible = false;

                // Add a small delay to ensure GitHub has processed the command
                await Task.Delay(_configuration?.GetValue<int>("PostCommandDelay", 1000) ?? 1000);

                // Reload issue details to show updated time tracking
                await LoadIssueDetails(_selectedIssue.Number);
            }
            else
            {
                await DisplayAlert("Error", $"Failed to log time: {result.Summary}", "OK");
            }
        }
        catch (Exception ex)
        {
            var errorDetails = $"Failed to log time:\n\nException: {ex.GetType().Name}\n\nMessage: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}";
            if (ex.InnerException != null)
            {
                errorDetails += $"\n\nInner Exception: {ex.InnerException.Message}";
            }
            await DisplayAlert("Error", errorDetails, "OK");
        }
    }

    private void OnLogTimeCancel(object sender, EventArgs e)
    {
        // Hide the form
        LogTimePanel.IsVisible = false;
        
        // Clear the input fields
        LogTimeDurationInput.Text = "";
        LogTimeDescriptionInput.Text = "";
        LogTimeDateInput.Text = "";
    }

    private void OnCancelSubtaskCompletion(object? sender, EventArgs e)
    {
        // Hide the panel and clear pending state
        SubtaskCompletionPanel.IsVisible = false;
        _pendingSubtaskId = null;
        CompletionTimeInput.Text = "";
        CompletionNotesInput.Text = "";
    }

    private async void OnConfirmSubtaskCompletion(object? sender, EventArgs e)
    {
        if (_selectedIssue == null || string.IsNullOrWhiteSpace(_pendingSubtaskId)) return;

        try
        {
            if (_commandExecutor == null || _commandParser == null || _commandPostingService == null) return;

            // Build the complete command with optional parameters
            var command = $"/notnow complete {_pendingSubtaskId}";

            var time = CompletionTimeInput.Text?.Trim();
            var notes = CompletionNotesInput.Text?.Trim();

            if (!string.IsNullOrWhiteSpace(time))
            {
                command += $" --time {time}";
            }

            if (!string.IsNullOrWhiteSpace(notes))
            {
                // Escape quotes in notes
                var escapedNotes = notes.Replace("\"", "\\\"");
                command += $" --notes \"{escapedNotes}\"";
            }

            // Parse command
            var parseResult = _commandParser.Parse(command, CommandContext.Comment);
            if (!parseResult.Commands.Any())
            {
                await DisplayAlert("Error", "Failed to parse complete command", "OK");
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
                await PostCommandAndUpdateState(_selectedIssue.Number, command, result);

                // Hide the panel
                SubtaskCompletionPanel.IsVisible = false;
                _pendingSubtaskId = null;
                CompletionTimeInput.Text = "";
                CompletionNotesInput.Text = "";

                // Add a small delay to ensure GitHub has processed the command
                await Task.Delay(_configuration?.GetValue<int>("PostCommandDelay", 1000) ?? 1000);

                // Reload issue details to show updated subtask
                await LoadIssueDetails(_selectedIssue.Number);
            }
            else
            {
                await DisplayAlert("Error", $"Failed to complete subtask: {result.Summary}", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to complete subtask: {ex.Message}", "OK");
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

        // Validate estimate format if provided
        if (!string.IsNullOrWhiteSpace(estimate))
        {
            // Check format like 1d, 8h, 30m, 1d8h, 8h30m, 1d8h30m
            var estimateRegex = new Regex(@"^(?:(\d+)d)?(?:(\d+)h)?(?:(\d+)m)?$", RegexOptions.IgnoreCase);
            var match = estimateRegex.Match(estimate);

            if (!match.Success || match.Value == "")
            {
                await DisplayAlert("Error", "Estimate must be in format like: 1d, 8h, 30m, 1d8h, 8h30m, or 1d8h30m", "OK");
                return;
            }

            // Check that at least one time unit was specified
            var hasValue = match.Groups[1].Success || match.Groups[2].Success || match.Groups[3].Success;
            if (!hasValue)
            {
                await DisplayAlert("Error", "Estimate must include at least one time unit (d, h, or m)", "OK");
                return;
            }

            // Validate reasonable values
            var days = match.Groups[1].Success ? int.Parse(match.Groups[1].Value) : 0;
            var hours = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 0;
            var minutes = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;

            if (days > 365)
            {
                await DisplayAlert("Error", "Estimate days cannot exceed 365", "OK");
                return;
            }
            if (hours > 23)
            {
                await DisplayAlert("Error", "Estimate hours cannot exceed 23", "OK");
                return;
            }
            if (minutes > 59)
            {
                await DisplayAlert("Error", "Estimate minutes cannot exceed 59", "OK");
                return;
            }
        }

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
                await PostCommandAndUpdateState(_selectedIssue.Number, command, result);

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
                await PostCommandAndUpdateState(_selectedIssue.Number, command, result);

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

                // Post the /notnow status todo command when reopening
                if (_commandPostingService != null)
                {
                    var statusCommand = "/notnow status todo";
                    var statusResult = new ExecutionResult
                    {
                        Results = new List<CommandResult>
                        {
                            CommandResult.Ok("Status set to todo", new { Status = "todo" })
                        }
                    };

                    await PostCommandAndUpdateState(issue.Number, statusCommand, statusResult);
                }
            }
            else
            {
                // Close the issue
                await _gitHubService.CloseIssueAsync(issue.Number);
                issue.IsClosed = true;

                // Post the /notnow status done command when closing
                if (_commandPostingService != null)
                {
                    var statusCommand = "/notnow status done";
                    var statusResult = new ExecutionResult
                    {
                        Results = new List<CommandResult>
                        {
                            CommandResult.Ok("Status set to done", new { Status = "done" })
                        }
                    };

                    await PostCommandAndUpdateState(issue.Number, statusCommand, statusResult);
                }
            }

            // Add delay to allow GitHub to process the state change
            await Task.Delay(1000);

            // Refresh the issue display to show the new state
            await RefreshIssues();

            // Check if the state actually changed, if not retry once
            var updatedIssue = _issues.FirstOrDefault(i => i.Number == issue.Number);
            if (updatedIssue != null && updatedIssue.IsClosed == !issue.IsClosed)
            {
                // State didn't change yet, wait and try again
                await Task.Delay(2000);
                await RefreshIssues();
            }

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
        // Set default due date to +7 days from today
        IssueDueDateInput.Text = DateTime.Today.AddDays(7).ToString("yyyy-MM-dd");
        IssueEstimateInput.Text = "";
        IssueTitleInput.Focus();
    }

    private void OnCancelIssueInput(object? sender, EventArgs e)
    {
        // Hide the issue input panel
        IssueInputPanel.IsVisible = false;
        IssueTitleInput.Text = "";
        IssueDescriptionInput.Text = "";
        IssueDueDateInput.Text = "";
        IssueEstimateInput.Text = "";
    }

    private async void OnCreateIssue(object? sender, EventArgs e)
    {
        Console.WriteLine("[OnCreateIssue] Starting issue creation");
        Console.WriteLine($"[OnCreateIssue] Current groupByTag mode: {_groupByTag}");
        Console.WriteLine($"[OnCreateIssue] Current _groupedIssues count: {_groupedIssues?.Count ?? -1}");
        Console.WriteLine($"[OnCreateIssue] Current _filteredIssues count: {_filteredIssues?.Count ?? -1}");

        if (string.IsNullOrWhiteSpace(IssueTitleInput.Text))
        {
            await DisplayAlert("Error", "Title is required for the issue", "OK");
            return;
        }

        var title = IssueTitleInput.Text.Trim();
        var description = IssueDescriptionInput.Text?.Trim() ?? "";
        var dueDate = IssueDueDateInput.Text?.Trim();
        var estimate = IssueEstimateInput.Text?.Trim();

        // Validate due date format if provided
        if (!string.IsNullOrWhiteSpace(dueDate))
        {
            // Check YYYY-MM-DD format
            var dateRegex = new Regex(@"^\d{4}-\d{2}-\d{2}$");
            if (!dateRegex.IsMatch(dueDate))
            {
                await DisplayAlert("Error", "Due date must be in YYYY-MM-DD format", "OK");
                return;
            }

            // Try to parse as a valid date
            if (!DateTime.TryParseExact(dueDate, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out _))
            {
                await DisplayAlert("Error", "Due date is not a valid date", "OK");
                return;
            }
        }

        // Validate estimate format if provided
        if (!string.IsNullOrWhiteSpace(estimate))
        {
            // Check format like 1d, 8h, 30m, 1d8h, 8h30m, 1d8h30m
            var estimateRegex = new Regex(@"^(?:(\d+)d)?(?:(\d+)h)?(?:(\d+)m)?$", RegexOptions.IgnoreCase);
            var match = estimateRegex.Match(estimate);

            if (!match.Success || match.Value == "")
            {
                await DisplayAlert("Error", "Estimate must be in format like: 1d, 8h, 30m, 1d8h, 8h30m, or 1d8h30m", "OK");
                return;
            }

            // Check that at least one time unit was specified
            var hasValue = match.Groups[1].Success || match.Groups[2].Success || match.Groups[3].Success;
            if (!hasValue)
            {
                await DisplayAlert("Error", "Estimate must include at least one time unit (d, h, or m)", "OK");
                return;
            }

            // Validate reasonable values
            var days = match.Groups[1].Success ? int.Parse(match.Groups[1].Value) : 0;
            var hours = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 0;
            var minutes = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;

            if (days > 365)
            {
                await DisplayAlert("Error", "Estimate days cannot exceed 365", "OK");
                return;
            }
            if (hours > 23)
            {
                await DisplayAlert("Error", "Estimate hours cannot exceed 23", "OK");
                return;
            }
            if (minutes > 59)
            {
                await DisplayAlert("Error", "Estimate minutes cannot exceed 59", "OK");
                return;
            }
        }

        // Hide the input panel
        Console.WriteLine("[OnCreateIssue] Hiding issue input panel");
        IssueInputPanel.IsVisible = false;

        try
        {
            if (_gitHubService == null || _commandPostingService == null)
            {
                Console.WriteLine("[OnCreateIssue] ERROR: Services are null");
                return;
            }

            Console.WriteLine("[OnCreateIssue] Showing loading indicator");
            ShowLoadingIndicator();

            // Create the new issue on GitHub
            Console.WriteLine($"[OnCreateIssue] Creating issue on GitHub with title: {title}");
            var newIssue = await _gitHubService.CreateIssueAsync(title, description);
            Console.WriteLine($"[OnCreateIssue] Issue created with number: {newIssue?.Number}");

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

                // Post the init command to GitHub and update embedded state
                await PostCommandAndUpdateState(newIssue.Number, initCommand, executionResult);

                // Post the /notnow status todo command to set initial status
                var statusCommand = "/notnow status todo";
                var statusResult = new ExecutionResult
                {
                    Results = new List<CommandResult>
                    {
                        CommandResult.Ok("Status set to todo", new { Status = "todo" })
                    }
                };

                await PostCommandAndUpdateState(newIssue.Number, statusCommand, statusResult);

                // Post the /notnow priority medium command to set initial priority
                var priorityCommand = "/notnow priority medium";
                var priorityResult = new ExecutionResult
                {
                    Results = new List<CommandResult>
                    {
                        CommandResult.Ok("Priority set to medium", new { Priority = "medium" })
                    }
                };

                await PostCommandAndUpdateState(newIssue.Number, priorityCommand, priorityResult);

                // Auto-assign to current user if available
                if (_currentUser != null)
                {
                    var assignCommand = $"/notnow assign {_currentUser.Login}";
                    var assignResult = new ExecutionResult
                    {
                        Results = new List<CommandResult>
                        {
                            CommandResult.Ok($"Assigned to {_currentUser.Login}", new { Assignee = _currentUser.Login })
                        }
                    };

                    await PostCommandAndUpdateState(newIssue.Number, assignCommand, assignResult);
                }

                // Set due date if provided
                if (!string.IsNullOrWhiteSpace(dueDate))
                {
                    var dueCommand = $"/notnow due {dueDate}";
                    var dueResult = new ExecutionResult
                    {
                        Results = new List<CommandResult>
                        {
                            CommandResult.Ok($"Due date set to {dueDate}", new { DueDate = dueDate })
                        }
                    };

                    await PostCommandAndUpdateState(newIssue.Number, dueCommand, dueResult);
                }

                // Set estimate if provided
                if (!string.IsNullOrWhiteSpace(estimate))
                {
                    var estimateCommand = $"/notnow estimate {estimate}";
                    var estimateResult = new ExecutionResult
                    {
                        Results = new List<CommandResult>
                        {
                            CommandResult.Ok($"Estimate set to {estimate}", new { Estimate = estimate })
                        }
                    };

                    await PostCommandAndUpdateState(newIssue.Number, estimateCommand, estimateResult);
                }

                // Clear inputs
                IssueTitleInput.Text = "";
                IssueDescriptionInput.Text = "";
                IssueDueDateInput.Text = "";
                IssueEstimateInput.Text = "";

                // Add a small delay to ensure GitHub has processed the new issue
                Console.WriteLine("[OnCreateIssue] Waiting 1 second for GitHub to process");
                await Task.Delay(1000);

                // Refresh the issues list
                Console.WriteLine("[OnCreateIssue] Calling RefreshIssues");
                Console.WriteLine($"[OnCreateIssue] Before refresh - _groupByTag: {_groupByTag}");
                await RefreshIssues();
                Console.WriteLine("[OnCreateIssue] RefreshIssues completed");
                Console.WriteLine($"[OnCreateIssue] After refresh - _groupByTag: {_groupByTag}");
                Console.WriteLine($"[OnCreateIssue] After refresh - _issues count: {_issues?.Count ?? -1}");
                Console.WriteLine($"[OnCreateIssue] After refresh - _filteredIssues count: {_filteredIssues?.Count ?? -1}");
                Console.WriteLine($"[OnCreateIssue] After refresh - _groupedIssues count: {_groupedIssues?.Count ?? -1}");

                // If the new issue isn't in the list yet, try once more
                Console.WriteLine($"[OnCreateIssue] Checking if issue #{newIssue.Number} is in _issues list");
                if (!_issues.Any(i => i.Number == newIssue.Number))
                {
                    await Task.Delay(2000);
                    await RefreshIssues();
                }

                // Select and load the newly created issue
                _selectedIssue = new IssueItem
                {
                    Number = newIssue.Number,
                    DisplayText = $"#{newIssue.Number} {(newIssue.Title.Length > 45 ? newIssue.Title.Substring(0, 45) + "..." : newIssue.Title)}"
                };

                // Find and select the issue in the CollectionView
                try
                {
                    if (_groupByTag)
                    {
                        // In grouped mode, find the issue within the groups
                        Console.WriteLine($"[OnCreateIssue] Searching for issue #{newIssue.Number} in grouped view");
                        foreach (var group in _groupedIssues)
                        {
                            var issueInGroup = group.FirstOrDefault(i => i.Number == newIssue.Number);
                            if (issueInGroup != null)
                            {
                                Console.WriteLine($"[OnCreateIssue] Found issue #{newIssue.Number} in group '{group.GroupName}'");
                                // In grouped mode, we can't directly select the item
                                // Just load the details instead
                                break;
                            }
                        }
                    }
                    else
                    {
                        // In flat mode, select normally
                        var issueInList = _filteredIssues.FirstOrDefault(i => i.Number == newIssue.Number);
                        if (issueInList != null)
                        {
                            Console.WriteLine($"[OnCreateIssue] Selecting issue #{newIssue.Number} in flat view");
                            IssuesListView.SelectedItem = issueInList;
                        }
                    }
                }
                catch (Exception selectEx)
                {
                    Console.WriteLine($"[OnCreateIssue] Error selecting issue: {selectEx.Message}");
                    // Continue anyway - at least load the details
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
        finally
        {
            HideLoadingIndicator();
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
                await PostCommandAndUpdateState(_selectedIssue.Number, command, result);

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

    private async void OnDeveloperModeClicked(object sender, EventArgs e)
    {
        if (_selectedIssue == null || _gitHubService == null || _issueStateParser == null)
        {
            await DisplayAlert("Developer Mode", "No issue selected or services not initialized", "OK");
            return;
        }

        try
        {
            // Get all issues to find the selected one
            var issues = await _gitHubService.GetIssuesAsync(Octokit.ItemStateFilter.All);
            var issue = issues.FirstOrDefault(i => i.Number == _selectedIssue.Number);

            if (issue == null)
            {
                await DisplayAlert("Error", "Could not find the selected issue", "OK");
                return;
            }

            // Get the comments
            var comments = await _gitHubService.GetIssueCommentsAsync(_selectedIssue.Number);

            // Parse the issue state
            var issueState = _issueStateParser.ParseIssueState(issue, comments);

            // Serialize the state to JSON for display
            var jsonOptions = new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            };

            string stateJson = System.Text.Json.JsonSerializer.Serialize(issueState, jsonOptions);

            // Display in the developer mode panel
            DeveloperModeContent.Text = stateJson;
            DeveloperModePanel.IsVisible = true;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to load issue state: {ex.Message}", "OK");
        }
    }

    private string _currentIssueStatus = "todo";

    private async void OnStatusTodoClicked(object sender, EventArgs e)
    {
        await SetIssueStatus("todo");
    }

    private async void OnStatusInProgressClicked(object sender, EventArgs e)
    {
        await SetIssueStatus("in_progress");
    }

    private async void OnStatusBlockedClicked(object sender, EventArgs e)
    {
        await SetIssueStatus("blocked");
    }

    private async Task SetIssueStatus(string status)
    {
        if (_selectedIssue == null || _commandPostingService == null)
            return;

        // Don't allow status changes on closed issues
        if (_selectedIssue.IsClosed || _currentIssueStatus == "done")
            return;

        try
        {
            // Don't do anything if status is already set
            if (_currentIssueStatus == status)
                return;

            var statusCommand = $"/notnow status {status}";
            var statusResult = new ExecutionResult
            {
                Results = new List<CommandResult>
                {
                    CommandResult.Ok($"Status set to {status}", new { Status = status })
                }
            };

            await PostCommandAndUpdateState(_selectedIssue.Number, statusCommand, statusResult);

            // Update the current status
            _currentIssueStatus = status;
            UpdateStatusButtonVisuals(status);

            // Update the issue in the list
            var issueInList = _issues.FirstOrDefault(i => i.Number == _selectedIssue.Number);
            if (issueInList != null)
            {
                issueInList.Status = status;
            }

            // Refresh issue details to show the updated state
            await LoadIssueDetails(_selectedIssue.Number);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to set status: {ex.Message}", "OK");
        }
    }

    private void UpdateStatusButtonVisuals(string activeStatus)
    {
        // Check if issue is closed (done status)
        bool isDisabled = activeStatus == "done" || (_selectedIssue?.IsClosed ?? false);
        
        // Reset all buttons to default state
        TodoStatusButton.BackgroundColor = Color.FromArgb("#2A2A2A");
        TodoStatusLabel.TextColor = Color.FromArgb("#4A9EFF");
        TodoStatusButton.IsEnabled = !isDisabled;
        TodoStatusLabel.Opacity = isDisabled ? 0.5 : 1.0;
        
        InProgressStatusButton.BackgroundColor = Color.FromArgb("#2A2A2A");
        InProgressStatusLabel.TextColor = Color.FromArgb("#4A9EFF");
        InProgressStatusButton.IsEnabled = !isDisabled;
        InProgressStatusLabel.Opacity = isDisabled ? 0.5 : 1.0;
        
        BlockedStatusButton.BackgroundColor = Color.FromArgb("#2A2A2A");
        BlockedStatusLabel.TextColor = Color.FromArgb("#4A9EFF");
        BlockedStatusButton.IsEnabled = !isDisabled;
        BlockedStatusLabel.Opacity = isDisabled ? 0.5 : 1.0;

        // Only highlight the active button if not disabled
        if (!isDisabled)
        {
            switch (activeStatus)
            {
                case "todo":
                    TodoStatusButton.BackgroundColor = Color.FromArgb("#4A9EFF");
                    TodoStatusLabel.TextColor = Color.FromArgb("#FFFFFF");
                    break;
                case "in_progress":
                    InProgressStatusButton.BackgroundColor = Color.FromArgb("#4A9EFF");
                    InProgressStatusLabel.TextColor = Color.FromArgb("#FFFFFF");
                    break;
                case "blocked":
                    BlockedStatusButton.BackgroundColor = Color.FromArgb("#4A9EFF");
                    BlockedStatusLabel.TextColor = Color.FromArgb("#FFFFFF");
                    break;
            }
        }
    }

    private string _currentIssuePriority = "medium";
    
    private async void OnPriorityCriticalClicked(object sender, EventArgs e)
    {
        await SetIssuePriority("critical");
    }

    private async void OnPriorityHighClicked(object sender, EventArgs e)
    {
        await SetIssuePriority("high");
    }

    private async void OnPriorityMediumClicked(object sender, EventArgs e)
    {
        await SetIssuePriority("medium");
    }

    private async void OnPriorityLowClicked(object sender, EventArgs e)
    {
        await SetIssuePriority("low");
    }

    private async Task SetIssuePriority(string priority)
    {
        if (_selectedIssue == null || _commandPostingService == null)
            return;

        // Don't allow priority changes on closed issues
        if (_selectedIssue.IsClosed || _currentIssueStatus == "done")
            return;

        try
        {
            // Don't do anything if priority is already set
            if (_currentIssuePriority == priority)
                return;

            var priorityCommand = $"/notnow priority {priority}";
            var priorityResult = new ExecutionResult
            {
                Results = new List<CommandResult>
                {
                    CommandResult.Ok($"Priority set to {priority}", new { Priority = priority })
                }
            };

            await PostCommandAndUpdateState(_selectedIssue.Number, priorityCommand, priorityResult);

            // Update the current priority
            _currentIssuePriority = priority;
            UpdatePriorityButtonVisuals(priority);

            // Update the issue in the list
            var issueInList = _issues.FirstOrDefault(i => i.Number == _selectedIssue.Number);
            if (issueInList != null)
            {
                issueInList.Priority = priority;
            }

            // Refresh issue details to show the updated state
            await LoadIssueDetails(_selectedIssue.Number);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to set priority: {ex.Message}", "OK");
        }
    }

    private void UpdatePriorityButtonVisuals(string activePriority)
    {
        // Check if issue is closed
        bool isDisabled = _selectedIssue?.IsClosed ?? false;
        
        // Reset all buttons to default state
        CriticalPriorityButton.BackgroundColor = Color.FromArgb("#2A2A2A");
        CriticalPriorityLabel.TextColor = Color.FromArgb("#4A9EFF");
        CriticalPriorityButton.IsEnabled = !isDisabled;
        CriticalPriorityLabel.Opacity = isDisabled ? 0.5 : 1.0;
        
        HighPriorityButton.BackgroundColor = Color.FromArgb("#2A2A2A");
        HighPriorityLabel.TextColor = Color.FromArgb("#4A9EFF");
        HighPriorityButton.IsEnabled = !isDisabled;
        HighPriorityLabel.Opacity = isDisabled ? 0.5 : 1.0;
        
        MediumPriorityButton.BackgroundColor = Color.FromArgb("#2A2A2A");
        MediumPriorityLabel.TextColor = Color.FromArgb("#4A9EFF");
        MediumPriorityButton.IsEnabled = !isDisabled;
        MediumPriorityLabel.Opacity = isDisabled ? 0.5 : 1.0;
        
        LowPriorityButton.BackgroundColor = Color.FromArgb("#2A2A2A");
        LowPriorityLabel.TextColor = Color.FromArgb("#4A9EFF");
        LowPriorityButton.IsEnabled = !isDisabled;
        LowPriorityLabel.Opacity = isDisabled ? 0.5 : 1.0;

        // Only highlight the active button if not disabled
        if (!isDisabled)
        {
            switch (activePriority)
            {
                case "critical":
                    CriticalPriorityButton.BackgroundColor = Color.FromArgb("#4A9EFF");
                    CriticalPriorityLabel.TextColor = Color.FromArgb("#FFFFFF");
                    break;
                case "high":
                    HighPriorityButton.BackgroundColor = Color.FromArgb("#4A9EFF");
                    HighPriorityLabel.TextColor = Color.FromArgb("#FFFFFF");
                    break;
                case "medium":
                    MediumPriorityButton.BackgroundColor = Color.FromArgb("#4A9EFF");
                    MediumPriorityLabel.TextColor = Color.FromArgb("#FFFFFF");
                    break;
                case "low":
                    LowPriorityButton.BackgroundColor = Color.FromArgb("#4A9EFF");
                    LowPriorityLabel.TextColor = Color.FromArgb("#FFFFFF");
                    break;
            }
        }
    }

    // Priority Filters
    private bool _filterCriticalPriority = true;
    private bool _filterHighPriority = true;
    private bool _filterMediumPriority = true;
    private bool _filterLowPriority = true;

    private async void OnPriorityFilterCriticalClicked(object sender, EventArgs e)
    {
        _filterCriticalPriority = !_filterCriticalPriority;
        UpdatePriorityFilterVisual(CriticalPriorityFilter, CriticalPriorityFilterLabel, _filterCriticalPriority);
        await ApplyPriorityFilters();
    }

    private async void OnPriorityFilterHighClicked(object sender, EventArgs e)
    {
        _filterHighPriority = !_filterHighPriority;
        UpdatePriorityFilterVisual(HighPriorityFilter, HighPriorityFilterLabel, _filterHighPriority);
        await ApplyPriorityFilters();
    }

    private async void OnPriorityFilterMediumClicked(object sender, EventArgs e)
    {
        _filterMediumPriority = !_filterMediumPriority;
        UpdatePriorityFilterVisual(MediumPriorityFilter, MediumPriorityFilterLabel, _filterMediumPriority);
        await ApplyPriorityFilters();
    }

    private async void OnPriorityFilterLowClicked(object sender, EventArgs e)
    {
        _filterLowPriority = !_filterLowPriority;
        UpdatePriorityFilterVisual(LowPriorityFilter, LowPriorityFilterLabel, _filterLowPriority);
        await ApplyPriorityFilters();
    }

    private void UpdatePriorityFilterVisual(Border border, Label label, bool isActive)
    {
        if (isActive)
        {
            border.BackgroundColor = Color.FromArgb("#4A9EFF");
            label.TextColor = Color.FromArgb("#FFFFFF");
        }
        else
        {
            border.BackgroundColor = Color.FromArgb("#2A2A2A");
            label.TextColor = Color.FromArgb("#4A9EFF");
        }
    }

    private async Task ApplyPriorityFilters()
    {
        await LoadIssuesAsync();
    }

    private void OnCloseDeveloperMode(object sender, EventArgs e)
    {
        DeveloperModePanel.IsVisible = false;
        DeveloperModeContent.Text = "";
    }

    private void InitializeRepositorySelector()
    {
        if (_gitHubServiceManager == null) return;

        var repositories = _gitHubServiceManager.GetRepositories();
        if (!repositories.Any())
        {
            RepositorySelector.IsEnabled = false;
            return;
        }

        // Populate the picker
        RepositorySelector.ItemsSource = repositories.Select(r => r.DisplayName).ToList();

        // Select default repository
        var defaultRepoId = _configuration?.GetValue<string>("DefaultRepositoryId");
        var defaultRepo = repositories.FirstOrDefault(r => r.Id == defaultRepoId) ?? repositories.First();

        _currentRepositoryId = defaultRepo.Id;
        _gitHubServiceManager.CurrentRepositoryId = _currentRepositoryId;
        _gitHubService = _gitHubServiceManager.GetService(_currentRepositoryId);

        // Now that CurrentRepositoryId is set, create a service scope to get fresh instances
        // of services that depend on IGitHubService
        var serviceProvider = Handler?.MauiContext?.Services;
        if (serviceProvider != null)
        {
            _currentServiceScope = serviceProvider.CreateScope();
            var scopedProvider = _currentServiceScope.ServiceProvider;

            // Get fresh instances that will use the current repository
            _commandParser = scopedProvider.GetService<ICommandParser>();
            _commandPostingService = scopedProvider.GetService<ICommandPostingService>();
            _commandExecutor = scopedProvider.GetService<ICommandExecutor>();
            _issueStateParser = scopedProvider.GetService<IIssueStateParser>();
            _stateManager = scopedProvider.GetService<IIssueStateManager>() ?? new IssueStateManager();
            Console.WriteLine("[TerminalPage] Repository-specific services loaded");
        }

        var selectedIndex = repositories.IndexOf(defaultRepo);
        if (selectedIndex >= 0)
        {
            RepositorySelector.SelectedIndex = selectedIndex;
        }

        // Load the current user
        _ = Task.Run(async () => await LoadCurrentUserAsync());
    }

    private CancellationTokenSource? _loadingAnimationCts;

    private void ShowLoadingIndicator()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (LoadingIndicator != null)
            {
                LoadingIndicator.IsVisible = true;

                // Cancel any existing animation
                _loadingAnimationCts?.Cancel();
                _loadingAnimationCts = new CancellationTokenSource();

                // Start continuous rotation animation in background
                Task.Run(async () =>
                {
                    try
                    {
                        while (!_loadingAnimationCts.Token.IsCancellationRequested)
                        {
                            await MainThread.InvokeOnMainThreadAsync(async () =>
                            {
                                if (LoadingIndicator != null && LoadingIndicator.IsVisible)
                                {
                                    await LoadingIndicator.RotateTo(360, 333);
                                    LoadingIndicator.Rotation = 0;
                                }
                            });

                            if (_loadingAnimationCts.Token.IsCancellationRequested)
                                break;
                        }
                    }
                    catch (TaskCanceledException)
                    {
                        // Animation was cancelled, this is expected
                    }
                });
            }
        });
    }

    private void HideLoadingIndicator()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (LoadingIndicator != null)
            {
                // Cancel the rotation animation
                _loadingAnimationCts?.Cancel();

                LoadingIndicator.IsVisible = false;
                LoadingIndicator.Rotation = 0;
            }
        });
    }

    private async Task LoadCurrentUserAsync()
    {
        try
        {
            if (_gitHubService == null) return;

            ShowLoadingIndicator();
            _currentUser = await _gitHubService.GetCurrentUserAsync();

            // Update the UI with the username on the main thread
            // User label removed from UI
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"[TerminalPage] Failed to load current user: {ex.Message}");
            // User label removed from UI
        }
        finally
        {
            HideLoadingIndicator();
        }
    }

    private async void OnRepositorySelectionChanged(object? sender, EventArgs e)
    {
        if (RepositorySelector.SelectedIndex < 0 || _gitHubServiceManager == null) return;

        var repositories = _gitHubServiceManager.GetRepositories();
        if (RepositorySelector.SelectedIndex >= repositories.Count) return;

        var selectedRepo = repositories[RepositorySelector.SelectedIndex];
        if (selectedRepo.Id == _currentRepositoryId) return;

        // Save any active work
        if (_selectedIssue != null && !string.IsNullOrEmpty(_selectedIssue.Title))
        {
            // Could prompt to save work here if needed
        }

        // Switch repository
        _currentRepositoryId = selectedRepo.Id;
        _gitHubServiceManager.CurrentRepositoryId = _currentRepositoryId;
        _gitHubService = _gitHubServiceManager.GetService(_currentRepositoryId);

        // Reload scoped services that depend on IGitHubService
        // We need fresh instances since they cache the IGitHubService at construction
        var serviceProvider = Handler?.MauiContext?.Services;
        if (serviceProvider != null)
        {
            // Dispose the previous service scope if it exists
            _currentServiceScope?.Dispose();

            // Create a new scope for getting fresh services
            _currentServiceScope = serviceProvider.CreateScope();
            var scopedProvider = _currentServiceScope.ServiceProvider;

            // Get fresh instances from the new scope
            _commandPostingService = scopedProvider.GetService<ICommandPostingService>();
            _commandExecutor = scopedProvider.GetService<ICommandExecutor>();
            _issueStateParser = scopedProvider.GetService<IIssueStateParser>();
            _stateManager = scopedProvider.GetService<IIssueStateManager>() ?? new IssueStateManager();
        }

        // Clear current state
        _selectedIssue = null;
        _allComments = null;
        _issues.Clear();
        ClearIssueDetails();

        // Load issues for new repository
        await LoadIssuesAsync();
    }

    private void OnLogoClicked(object sender, EventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://notnowboss.com",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            DisplayAlert("Error", $"Failed to open website: {ex.Message}", "OK");
        }
    }

    private void OnHideWindow(object sender, EventArgs e)
    {
        try
        {
            // Call the ToggleVisibility method from the App class
            if (Application.Current is App app)
            {
                app.ToggleVisibility();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TerminalPage] OnHideWindow error: {ex.Message}");
            DisplayAlert("Error", $"Failed to hide window: {ex.Message}", "OK");
        }
    }

    private void OnQuitApplication(object sender, EventArgs e)
    {
        try
        {
            // Quit the application
            Application.Current?.Quit();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TerminalPage] OnQuitApplication error: {ex.Message}");
            DisplayAlert("Error", $"Failed to quit application: {ex.Message}", "OK");
        }
    }

    private void OnExpandWindow(object sender, EventArgs e)
    {
#if WINDOWS
        try
        {
            var window = Application.Current?.Windows?.FirstOrDefault();
            if (window?.Handler is Microsoft.Maui.Handlers.WindowHandler handler)
            {
                var handle = WinRT.Interop.WindowNative.GetWindowHandle(handler.PlatformView);
                var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(handle);
                var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

                var displayArea = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(windowId, Microsoft.UI.Windowing.DisplayAreaFallback.Primary);
                var workAreaWidth = displayArea.WorkArea.Width;
                var workAreaHeight = displayArea.WorkArea.Height;

                // Toggle between expanded and normal height
                var currentHeight = appWindow.Size.Height;
                var expandedHeight = workAreaHeight;
                var normalHeight = (int)(workAreaHeight * 0.6); // Use default 60% height

                // If current height is less than 90% of work area, expand to full height
                // Otherwise, restore to normal height
                if (currentHeight < workAreaHeight * 0.9)
                {
                    // Expand to full work area height
                    appWindow.MoveAndResize(new Windows.Graphics.RectInt32
                    {
                        X = 0,
                        Y = 0,
                        Width = workAreaWidth,
                        Height = workAreaHeight
                    });

                    // Update the icon to show "restore" state
                    if (sender is Label expandButton)
                    {
                        expandButton.Text = "\ue94d"; // Restore icon
                        ToolTipProperties.SetText(expandButton, "Restore window size");
                    }
                }
                else
                {
                    // Restore to normal height (60% of screen)
                    appWindow.MoveAndResize(new Windows.Graphics.RectInt32
                    {
                        X = 0,
                        Y = 0,
                        Width = workAreaWidth,
                        Height = normalHeight
                    });

                    // Update the icon to show "expand" state
                    if (sender is Label expandButton)
                    {
                        expandButton.Text = "\ue94f"; // Expand icon
                        ToolTipProperties.SetText(expandButton, "Expand window");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TerminalPage] OnExpandWindow error: {ex.Message}");
            DisplayAlert("Error", $"Failed to resize window: {ex.Message}", "OK");
        }
#endif
    }

    private void OnSettingsClicked(object sender, EventArgs e)
    {
        try
        {
            var settingsPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

            // Ensure file exists
            if (!File.Exists(settingsPath))
            {
                // Create default settings file
                var defaultConfig = new
                {
                    GitHubRepositories = new[]
                    {
                        new
                        {
                            Id = "default",
                            DisplayName = "Default Repository",
                            Owner = "your-username",
                            Repository = "your-repo",
                            PersonalAccessToken = "ghp_your_token_here"
                        }
                    },
                    DefaultRepositoryId = "default",
                    Window = new
                    {
                        HeightPercentage = 0.6,
                        AnimationSpeed = 15
                    }
                };

                var json = System.Text.Json.JsonSerializer.Serialize(defaultConfig, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(settingsPath, json);
            }

            // Open in default editor
            Process.Start(new ProcessStartInfo
            {
                FileName = settingsPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            DisplayAlert("Error", $"Failed to open settings: {ex.Message}", "OK");
        }
    }

    private void SetupConfigWatcher()
    {
        try
        {
            var settingsPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
            var directory = System.IO.Path.GetDirectoryName(settingsPath);

            if (string.IsNullOrEmpty(directory)) return;

            _configWatcher = new FileSystemWatcher(directory)
            {
                Filter = "appsettings.json",
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
            };

            _configWatcher.Changed += OnConfigFileChanged;
            _configWatcher.EnableRaisingEvents = true;
        }
        catch (Exception ex)
        {
            // Silently fail - config watching is not critical
            System.Diagnostics.Debug.WriteLine($"Failed to setup config watcher: {ex.Message}");
        }
    }

    private async void OnConfigFileChanged(object sender, FileSystemEventArgs e)
    {
        // Debounce multiple change events
        var now = DateTime.UtcNow;
        if ((now - _lastConfigReload).TotalMilliseconds < 1000)
            return;
        _lastConfigReload = now;

        // Wait a bit for file to be written completely
        await Task.Delay(500);

        // Reload configuration on UI thread
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await ReloadConfiguration();
        });
    }

    private async Task ReloadConfiguration()
    {
        try
        {
            // Save current selection
            var currentRepoId = _currentRepositoryId;
            var currentIssueNumber = _selectedIssue?.Number;

            // Reload configuration
            var settingsPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
            if (!File.Exists(settingsPath)) return;

            var json = await File.ReadAllTextAsync(settingsPath);
            var config = System.Text.Json.JsonSerializer.Deserialize<ConfigRoot>(json);

            if (config?.GitHubRepositories == null || !config.GitHubRepositories.Any())
            {
                await DisplayAlert("Configuration Error", "No repositories configured in appsettings.json", "OK");
                return;
            }

            // Update service manager
            _gitHubServiceManager?.UpdateServices(config.GitHubRepositories);

            // Reinitialize repository selector
            InitializeRepositorySelector();

            // Try to restore previous selection
            var repositories = _gitHubServiceManager?.GetRepositories();
            if (repositories != null)
            {
                var prevRepo = repositories.FirstOrDefault(r => r.Id == currentRepoId);
                if (prevRepo != null)
                {
                    var index = repositories.IndexOf(prevRepo);
                    if (index >= 0)
                    {
                        RepositorySelector.SelectedIndex = index;
                    }
                }
                else
                {
                    // Previous repo no longer exists, load first one
                    await LoadIssuesAsync();
                }
            }

            await DisplayAlert("Settings Reloaded", "Configuration has been refreshed", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Configuration Error", $"Failed to reload settings: {ex.Message}", "OK");
        }
    }

    private NotNow.Core.Models.IssueState ConvertVersionedToState(IssueStateData data)
    {
        return new NotNow.Core.Models.IssueState
        {
            IssueNumber = data.IssueNumber,
            Title = data.Title,
            Status = data.Status,
            Priority = data.Priority,
            Type = data.Type,
            Assignee = data.Assignee,
            Estimate = data.Estimate,
            DueDate = data.DueDate,
            Tags = new List<string>(data.Tags),
            Subtasks = new List<Subtask>(data.Subtasks),
            Sessions = new List<WorkSession>(data.Sessions),
            TotalTimeSpent = data.TotalTimeSpent,
            LastUpdated = DateTime.UtcNow,
            IsInitialized = data.IsInitialized,
            ActiveSession = null
        };
    }

    private async Task UpdateEmbeddedStateAfterCommand(int issueNumber, string command)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"UpdateEmbeddedStateAfterCommand starting for issue #{issueNumber}, command: {command}");

            if (_gitHubService == null)
            {
                System.Diagnostics.Debug.WriteLine("UpdateEmbeddedStateAfterCommand: _gitHubService is null");
                return;
            }
            if (_issueStateParser == null)
            {
                System.Diagnostics.Debug.WriteLine("UpdateEmbeddedStateAfterCommand: _issueStateParser is null");
                return;
            }
            if (_stateManager == null)
            {
                System.Diagnostics.Debug.WriteLine("UpdateEmbeddedStateAfterCommand: _stateManager is null");
                return;
            }

            // Get the current issue
            var issue = await _gitHubService.GetIssueAsync(issueNumber);
            if (issue == null) return;

            // Get current state (from embedded or parse from comments)
            NotNow.Core.Models.IssueState currentState;
            var versionedState = _issueStateParser.ParseVersionedState(issue);
            
            if (versionedState != null)
            {
                // Use existing versioned state
                currentState = ConvertVersionedToState(versionedState.Data);
            }
            else
            {
                // Parse from comments for the first time
                var comments = await _gitHubService.GetIssueCommentsAsync(issueNumber);
                currentState = _issueStateParser.ParseIssueState(issue, comments);
            }

            // Apply the new command to get updated state
            var newState = _issueStateParser.ParseCommandIntoState(currentState, command, DateTime.UtcNow);

            // Create or update versioned state
            IssueStateVersion newVersionedState;
            var clientId = Environment.MachineName; // Or get from config
            
            if (versionedState != null)
            {
                newVersionedState = _stateManager.IncrementVersion(versionedState, newState, command, clientId);
            }
            else
            {
                newVersionedState = _stateManager.CreateNewVersion(newState, command, clientId);
            }

            // Update issue body with embedded state
            var updatedBody = _stateManager.EmbedStateInBody(issue.Body, newVersionedState);
            System.Diagnostics.Debug.WriteLine($"UpdateEmbeddedStateAfterCommand: Generated updated body (length: {updatedBody?.Length})");

            // Update the issue on GitHub
            await _gitHubService.UpdateIssueAsync(issueNumber, body: updatedBody);

            System.Diagnostics.Debug.WriteLine($"UpdateEmbeddedStateAfterCommand: Successfully updated embedded state for issue #{issueNumber} to version {newVersionedState.StateVersion}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to update embedded state: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Full exception: {ex}");
            Console.WriteLine($"[UpdateEmbeddedState] ERROR: {ex.Message}");
            // Don't throw - this is a best-effort optimization
        }
    }

    private async Task PostCommandAndUpdateState(int issueNumber, string command, ExecutionResult result)
    {
        // Post the command with metadata to GitHub
        await _commandPostingService.PostCommandToGitHubAsync(issueNumber, command, result);
        
        // Update embedded state in issue body if command was successful
        if (result.Success)
        {
            await UpdateEmbeddedStateAfterCommand(issueNumber, command);
            
            // Refresh the issues list to show updated task counts
            // Check if the command affects task counts
            if (command.Contains("subtask") || command.Contains("complete") || command.Contains("reopen"))
            {
                await RefreshIssueInList(issueNumber);
            }
        }
    }

    private async Task RefreshIssueInList(int issueNumber)
    {
        try
        {
            if (_gitHubService == null || _issueStateParser == null) return;

            // Find the issue in the list
            var issueItem = _issues.FirstOrDefault(i => i.Number == issueNumber);
            if (issueItem == null) return;

            // Get the updated issue from GitHub
            var issue = await _gitHubService.GetIssueAsync(issueNumber);
            if (issue == null) return;

            // Try to extract task counts from embedded state
            var versionedState = _issueStateParser.ParseVersionedState(issue);
            if (versionedState != null)
            {
                var taskCounts = versionedState.Data.GetTaskCounts();
                
                // Update the task counts - INotifyPropertyChanged will trigger UI update
                issueItem.OpenTaskCount = taskCounts.Open;
                issueItem.TotalTaskCount = taskCounts.Total;
                
                System.Diagnostics.Debug.WriteLine($"RefreshIssueInList: Updated issue #{issueNumber} task counts to [{taskCounts.Open}/{taskCounts.Total}]");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RefreshIssueInList: Failed to refresh issue #{issueNumber}: {ex.Message}");
        }
    }

    private void ClearIssueDetails()
    {
        IssueTitle.Text = "";
        IssueDescription.Text = "";
        StatusLabel.Text = "";
        PriorityLabel.Text = "";
        AssigneeLabel.Text = "";
        DueLabel.Text = "";
        EstimateLabel.Text = "";
        SubtasksList.Children.Clear();
        CommentsList.Children.Clear();
        TagsContainer.Children.Clear();
        WeekRowsContainer.Children.Clear();
        TotalTimeLabel.Text = "Total: 0h";
    }

    // Helper class for deserializing config
    private class ConfigRoot
    {
        public List<GitHubRepositoryConfig> GitHubRepositories { get; set; } = new();
        public string? DefaultRepositoryId { get; set; }
    }

    private void ApplyFilter()
    {
        try
        {
            Console.WriteLine($"[ApplyFilter] Starting. Filter text: '{_filterText}'");
            Console.WriteLine($"[ApplyFilter] All issues count: {_allIssues?.Count ?? -1}");
            Console.WriteLine($"[ApplyFilter] Sort mode: {_sortMode}");
            
            if (_filteredIssues == null)
            {
                Console.WriteLine("[ApplyFilter] ERROR: _filteredIssues is null, creating new instance");
                _filteredIssues = new ObservableCollection<IssueItem>();
            }
            
            _filteredIssues.Clear();
            Console.WriteLine("[ApplyFilter] Cleared filtered issues");

            if (_allIssues == null)
            {
                Console.WriteLine("[ApplyFilter] ERROR: _allIssues is null");
                return;
            }

            // First, filter the issues
            var filtered = new List<IssueItem>();
            foreach (var issue in _allIssues)
            {
                bool matchesFilter = true;

                // Apply text filter if present
                if (!string.IsNullOrWhiteSpace(_filterText))
                {
                    var filterLower = _filterText.ToLower();
                    
                    // Check if title contains the filter text
                    bool titleMatch = issue.Title?.ToLower().Contains(filterLower) ?? false;
                    
                    // Check if any tag contains the filter text
                    bool tagMatch = issue.Tags?.Any(tag => tag.ToLower().Contains(filterLower)) ?? false;
                    
                    matchesFilter = titleMatch || tagMatch;
                }

                if (matchesFilter)
                {
                    filtered.Add(issue);
                }
            }
            
            // Now sort the filtered issues based on the current sort mode
            IEnumerable<IssueItem> sorted = _sortMode switch
            {
                SortMode.IssueNumberDescending => 
                    filtered.OrderByDescending(i => i.Number),
                    
                SortMode.DueDateAscending =>
                    // Nulls first, then ascending by date
                    filtered.OrderBy(i => i.DueDate.HasValue)
                           .ThenBy(i => i.DueDate),
                           
                SortMode.DueDateDescending =>
                    // Nulls first, then descending by date
                    filtered.OrderBy(i => i.DueDate.HasValue)
                           .ThenByDescending(i => i.DueDate),
                           
                _ => filtered.OrderByDescending(i => i.Number)
            };
            
            // Add sorted items to the filtered collection
            foreach (var issue in sorted)
            {
                _filteredIssues.Add(issue);
            }
            
            Console.WriteLine($"[ApplyFilter] Filter applied. Filtered issues count: {_filteredIssues.Count}");
            
            // Update the display based on grouping mode
            Console.WriteLine($"[ApplyFilter] Calling UpdateIssuesDisplay (groupByTag: {_groupByTag})");
            UpdateIssuesDisplay();
            Console.WriteLine("[ApplyFilter] UpdateIssuesDisplay completed");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ApplyFilter] ERROR: {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine($"[ApplyFilter] Stack trace: {ex.StackTrace}");
        }
    }

    private void OnIssueFilterTextChanged(object? sender, TextChangedEventArgs e)
    {
        _filterText = e.NewTextValue ?? string.Empty;
        ApplyFilter();
        
        // Select first item if any matches exist
        if (_filteredIssues.Any())
        {
            IssuesListView.SelectedItem = _filteredIssues.First();
        }
    }

    public class IssueItem : System.ComponentModel.INotifyPropertyChanged
    {
        private int _openTaskCount;
        private int _totalTaskCount;
        private string _status = "todo";
        private string _priority = "medium";
        private DateTime? _dueDate;
        private List<string> _tags = new List<string>();

        public int Number { get; set; }
        public string Title { get; set; } = "";
        public string DisplayText { get; set; } = "";
        public bool IsSelected { get; set; }
        public bool IsClosed { get; set; }

        // Due date property
        public DateTime? DueDate
        {
            get => _dueDate;
            set
            {
                if (_dueDate != value)
                {
                    _dueDate = value;
                    OnPropertyChanged(nameof(DueDate));
                    OnPropertyChanged(nameof(DueDateIcon));
                    OnPropertyChanged(nameof(DueDateIconColor));
                    OnPropertyChanged(nameof(DueDateIconVisible));
                    OnPropertyChanged(nameof(DueDateListColor));
                }
            }
        }
        public List<string> Tags
        {
            get => _tags;
            set
            {
                _tags = value ?? new List<string>();
                OnPropertyChanged(nameof(Tags));
            }
        }

        // Due date icon - only show if due tomorrow, today, or overdue
        public string DueDateIcon => "\ue878"; // Codepoint e878

        public string DueDateIconColor
        {
            get
            {
                if (!_dueDate.HasValue) return "#00000000"; // Transparent

                var today = DateTime.Today;
                var dueDate = _dueDate.Value.Date;

                if (dueDate < today) return "#FF4444"; // Red for overdue
                if (dueDate == today) return "#FFA500"; // Orange for today
                if (dueDate == today.AddDays(1)) return "#00FF00"; // Green for tomorrow

                return "#00000000"; // Transparent
            }
        }

        public bool DueDateIconVisible
        {
            get
            {
                if (!_dueDate.HasValue) return false;

                var today = DateTime.Today;
                var dueDate = _dueDate.Value.Date;

                // Show icon if due tomorrow, today, or overdue
                return dueDate <= today.AddDays(1);
            }
        }

        public string DueDateListColor
        {
            get
            {
                // If no due date is set, return white
                if (!_dueDate.HasValue) return "#FFFFFF";

                var today = DateTime.Today;
                var dueDate = _dueDate.Value.Date;
                var daysUntilDue = (dueDate - today).TotalDays;

                // If due date is 14 days or less away, return white
                if (daysUntilDue <= 14) return "#FFFFFF";
                
                // If due date is more than 14 days but less than 30 days, return light gray
                if (daysUntilDue <= 30) return "#B0B0B0";
                
                // If due date is more than 30 days away, return dark gray
                return "#606060";
            }
        }

        // Priority property with notification
        public string Priority
        {
            get => _priority;
            set
            {
                if (_priority != value)
                {
                    _priority = value;
                    OnPropertyChanged(nameof(Priority));
                    OnPropertyChanged(nameof(CriticalPriorityIcon));
                    OnPropertyChanged(nameof(CriticalPriorityIconVisible));
                }
            }
        }

        // Critical priority icon - only show for critical priority issues
        public string CriticalPriorityIcon => "\uef55"; // Codepoint ef55
        public string CriticalPriorityIconColor => "#FF0000"; // Red color
        public bool CriticalPriorityIconVisible => Priority == "critical";

        // Status property with notification
        public string Status 
        { 
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged(nameof(Status));
                    OnPropertyChanged(nameof(StatusIcon));
                    OnPropertyChanged(nameof(StatusIconVisible));
                }
            }
        }
        
        // Status icon for the issue list - only show for in_progress and blocked
        public string StatusIcon => Status switch
        {
            "in_progress" => "\ue88b",  // In progress icon
            "blocked" => "\uE14B",      // Blocked icon
            _ => ""
        };
        
        public bool StatusIconVisible => Status == "in_progress" || Status == "blocked";
        
        // Task count properties with property change notification
        public int OpenTaskCount 
        { 
            get => _openTaskCount;
            set
            {
                if (_openTaskCount != value)
                {
                    _openTaskCount = value;
                    OnPropertyChanged(nameof(OpenTaskCount));
                    OnPropertyChanged(nameof(TaskCountIcon));
                    OnPropertyChanged(nameof(TaskCountIconVisible));
                    OnPropertyChanged(nameof(FullDisplayText));
                }
            }
        }
        
        public int TotalTaskCount 
        { 
            get => _totalTaskCount;
            set
            {
                if (_totalTaskCount != value)
                {
                    _totalTaskCount = value;
                    OnPropertyChanged(nameof(TotalTaskCount));
                    OnPropertyChanged(nameof(TaskCountIcon));
                    OnPropertyChanged(nameof(TaskCountIconVisible));
                    OnPropertyChanged(nameof(FullDisplayText));
                }
            }
        }
        
        // Task count icon - shows icon if there are any open subtasks
        public string TaskCountIcon
        {
            get
            {
                // Show e2e6 icon if there are any open subtasks, otherwise no icon
                return OpenTaskCount > 0 ? "\ue2e6" : "";
            }
        }

        public bool TaskCountIconVisible => OpenTaskCount > 0;

        // Legacy property for backward compatibility
        public string TaskCountDisplay => TotalTaskCount > 0 ? $" [{OpenTaskCount}/{TotalTaskCount}]" : "";

        // Display properties
        public string IssueNumberDisplay => $"#{Number}";
        public string TitleDisplay => Title.Length > 45 ? Title.Substring(0, 45) + "..." : Title;
        public string CheckboxText => IsClosed ? "☑" : "☐";
        public Color TextColor => IsClosed ? Color.FromArgb("#808080") : Color.FromArgb("#E0E0E0");
        public TextDecorations TextDecorations => IsClosed ? TextDecorations.Strikethrough : TextDecorations.None;
        
        // Combined display with task count
        public string FullDisplayText => DisplayText + TaskCountDisplay;
        
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }
    }

    public class GroupedIssueItem : List<IssueItem>
    {
        public string GroupName { get; private set; }

        public GroupedIssueItem(string groupName, List<IssueItem> items) : base(items ?? new List<IssueItem>())
        {
            GroupName = groupName;
            Console.WriteLine($"[GroupedIssueItem] Created group '{groupName}' with {items?.Count ?? 0} items");
        }

        // Display properties for the group header
        public string GroupDisplay => $"{GroupName} ({Count})";
        public string ExpandCollapseIcon => "\ue5cf"; // Always expanded for now
        public bool IsExpanded => true; // Always expanded for now
    }

    #region Timer Methods

    private void InitializeTimers()
    {
        // Initialize work timer (counts up)
        _workTimer = new System.Timers.Timer(1000); // Update every second
        _workTimer.Elapsed += OnWorkTimerElapsed;

        // Initialize countdown timer (counts down)
        _countdownTimer = new System.Timers.Timer(1000); // Update every second
        _countdownTimer.Elapsed += OnCountdownTimerElapsed;

        // Initialize UI
        UpdateWorkTimerDisplay();
        UpdateCountdownTimerDisplay();
    }

    private void OnWorkTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        var currentElapsed = _workTimerElapsed + (DateTime.Now - _workTimerStartTime);
        MainThread.BeginInvokeOnMainThread(() =>
        {
            UpdateWorkTimerDisplay();
        });
    }

    private void OnCountdownTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        _countdownRemainingSeconds--;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_countdownRemainingSeconds <= 0)
            {
                // Timer finished
                _countdownTimer?.Stop();
                _countdownTimerRunning = false;
                _countdownRemainingSeconds = 0;

                UpdateCountdownTimerDisplay();
                UpdateCountdownTimerButton();

                // Show notification
                ShowTimerNotification();
            }
            else
            {
                UpdateCountdownTimerDisplay();
            }
        });
    }

    private void UpdateWorkTimerDisplay()
    {
        var displayTime = _workTimerRunning ?
            _workTimerElapsed + (DateTime.Now - _workTimerStartTime) :
            _workTimerElapsed;

        var days = (int)displayTime.TotalDays;
        var hours = displayTime.Hours;
        var minutes = displayTime.Minutes;
        var seconds = displayTime.Seconds;

        WorkTimerDisplay.Text = FormatTime(days, hours, minutes, seconds);
    }

    private void UpdateCountdownTimerDisplay()
    {
        var totalSeconds = _countdownRemainingSeconds;
        var days = totalSeconds / 86400; // 24 * 60 * 60
        totalSeconds %= 86400;
        var hours = totalSeconds / 3600; // 60 * 60
        totalSeconds %= 3600;
        var minutes = totalSeconds / 60;
        var seconds = totalSeconds % 60;

        CountdownTimerDisplay.Text = FormatTime(days, hours, minutes, seconds);
    }

    private string FormatTime(int days, int hours, int minutes, int seconds)
    {
        var parts = new List<string>();

        if (days > 0)
            parts.Add($"{days}d");
        if (hours > 0 || days > 0)
            parts.Add($"{hours}h");
        if (minutes > 0 || hours > 0 || days > 0)
            parts.Add($"{minutes}m");

        parts.Add($"{seconds}s");

        return string.Join("", parts);
    }

    private void UpdateWorkTimerButton()
    {
        if (_workTimerRunning)
        {
            WorkTimerPlayPauseButton.Text = "\ue034"; // Pause icon
            ToolTipProperties.SetText(WorkTimerPlayPauseButton, "Pause work timer");
        }
        else
        {
            WorkTimerPlayPauseButton.Text = "\ue037"; // Play icon
            ToolTipProperties.SetText(WorkTimerPlayPauseButton, "Start work timer");
        }
    }

    private void UpdateCountdownTimerButton()
    {
        if (_countdownTimerRunning)
        {
            CountdownTimerStartStopButton.Text = "\ue047"; // Stop/Reset icon
            ToolTipProperties.SetText(CountdownTimerStartStopButton, "Stop countdown timer");
        }
        else
        {
            CountdownTimerStartStopButton.Text = "\ue037"; // Start icon
            ToolTipProperties.SetText(CountdownTimerStartStopButton, "Start countdown timer");
        }
    }

    private void OnWorkTimerToggle(object sender, EventArgs e)
    {
        if (_workTimerRunning)
        {
            // Pause timer - preserve elapsed time for resume
            _workTimer?.Stop();
            _workTimerElapsed += DateTime.Now - _workTimerStartTime;
            _workTimerRunning = false;
            _workTimerStopped = false; // This is a pause, not a stop
        }
        else
        {
            if (_workTimerStopped)
            {
                // Start fresh after being stopped - reset to zero
                _workTimerElapsed = TimeSpan.Zero;
                _workTimerStopped = false;
            }
            // Start/Resume timer
            _workTimerStartTime = DateTime.Now;
            _workTimer?.Start();
            _workTimerRunning = true;
        }

        UpdateWorkTimerButton();
        UpdateWorkTimerDisplay();
    }

    private void OnWorkTimerReset(object sender, EventArgs e)
    {
        // Stop timer - retain current value until play is pressed again
        _workTimer?.Stop();
        if (_workTimerRunning)
        {
            _workTimerElapsed += DateTime.Now - _workTimerStartTime;
        }
        _workTimerRunning = false;
        _workTimerStopped = true; // Mark as stopped (not just paused)

        UpdateWorkTimerButton();
        UpdateWorkTimerDisplay();
    }

    private void OnCountdownTimerUp(object sender, EventArgs e)
    {
        if (!_countdownTimerRunning)
        {
            // If currently at 1 minute, go to 5 minutes, otherwise add 5 minutes
            if (_countdownSeconds == 60) // If currently at 1 minute
            {
                _countdownSeconds = 300; // Go to 5 minutes
            }
            else
            {
                _countdownSeconds += 300; // Add 5 minutes
            }
            _countdownRemainingSeconds = _countdownSeconds;
        }
        else
        {
            // Same logic for running timer
            if (_countdownRemainingSeconds == 60) // If currently at 1 minute
            {
                _countdownRemainingSeconds = 300; // Go to 5 minutes
            }
            else
            {
                _countdownRemainingSeconds += 300; // Add 5 minutes
            }
        }

        UpdateCountdownTimerDisplay();
    }

    private void OnCountdownTimerDown(object sender, EventArgs e)
    {
        if (!_countdownTimerRunning)
        {
            // Allow going from 5 minutes to 1 minute, otherwise use 5-minute decrements
            if (_countdownSeconds == 300) // If currently at 5 minutes
            {
                _countdownSeconds = 60; // Go to 1 minute
            }
            else
            {
                _countdownSeconds = Math.Max(60, _countdownSeconds - 300); // Minimum 1 minute
            }
            _countdownRemainingSeconds = _countdownSeconds;
        }
        else
        {
            // Same logic for running timer
            if (_countdownRemainingSeconds <= 300 && _countdownRemainingSeconds > 60)
            {
                _countdownRemainingSeconds = 60; // Go to 1 minute if between 1-5 minutes
            }
            else
            {
                _countdownRemainingSeconds = Math.Max(1, _countdownRemainingSeconds - 300);
            }
        }

        UpdateCountdownTimerDisplay();
    }

    private void OnCountdownTimerStartStop(object sender, EventArgs e)
    {
        if (_countdownTimerRunning)
        {
            // Stop and reset timer
            _countdownTimer?.Stop();
            _countdownTimerRunning = false;
            _countdownRemainingSeconds = _countdownSeconds;
        }
        else
        {
            // Start timer
            if (_countdownRemainingSeconds <= 0)
            {
                _countdownRemainingSeconds = _countdownSeconds;
            }
            _countdownTimer?.Start();
            _countdownTimerRunning = true;
        }

        UpdateCountdownTimerButton();
        UpdateCountdownTimerDisplay();
    }

    private async void ShowTimerNotification()
    {
        var title = "NotNow - Timer Finished";
        var message = "Your countdown timer has completed!";

        try
        {
#if WINDOWS
            Console.WriteLine("[Timer] Attempting Windows toast notification...");

            // Try Windows 10/11 toast notifications first
            try
            {
                var toastNotifier = Windows.UI.Notifications.ToastNotificationManager.CreateToastNotifier("NotNow");

                var toastXml = Windows.UI.Notifications.ToastNotificationManager.GetTemplateContent(
                    Windows.UI.Notifications.ToastTemplateType.ToastText02);

                var textElements = toastXml.GetElementsByTagName("text");
                textElements[0].AppendChild(toastXml.CreateTextNode(title));
                textElements[1].AppendChild(toastXml.CreateTextNode(message));

                var toast = new Windows.UI.Notifications.ToastNotification(toastXml);

                // Add audio
                var audioElement = toastXml.CreateElement("audio");
                audioElement.SetAttribute("src", "ms-winsoundevent:Notification.Default");
                toastXml.DocumentElement?.AppendChild(audioElement);

                toastNotifier.Show(toast);
                Console.WriteLine("[Timer] Windows toast notification sent successfully");
                return;
            }
            catch (Exception toastEx)
            {
                Console.WriteLine($"[Timer] Toast notification failed: {toastEx.GetType().Name}");
                Console.WriteLine($"[Timer] Toast exception message: {toastEx.Message}");
                Console.WriteLine($"[Timer] Toast exception stack trace: {toastEx.StackTrace}");

                if (toastEx.InnerException != null)
                {
                    Console.WriteLine($"[Timer] Toast inner exception: {toastEx.InnerException.Message}");
                }
            }

            // Try WinRT notifications as second option
            try
            {
                Console.WriteLine("[Timer] Trying WinRT notification approach...");

                var notificationManager = Windows.UI.Notifications.ToastNotificationManager.GetDefault();
                var notifier = notificationManager.CreateToastNotifier();

                var template = Windows.UI.Notifications.ToastNotificationManager.GetTemplateContent(
                    Windows.UI.Notifications.ToastTemplateType.ToastText02);

                var textNodes = template.GetElementsByTagName("text");
                textNodes[0].InnerText = title;
                textNodes[1].InnerText = message;

                var notification = new Windows.UI.Notifications.ToastNotification(template);
                notifier.Show(notification);

                Console.WriteLine("[Timer] WinRT notification sent successfully");
                return;
            }
            catch (Exception winrtEx)
            {
                Console.WriteLine($"[Timer] WinRT notification failed: {winrtEx.GetType().Name}");
                Console.WriteLine($"[Timer] WinRT exception message: {winrtEx.Message}");
                Console.WriteLine($"[Timer] WinRT exception stack trace: {winrtEx.StackTrace}");

                if (winrtEx.InnerException != null)
                {
                    Console.WriteLine($"[Timer] WinRT inner exception: {winrtEx.InnerException.Message}");
                }
            }

            Console.WriteLine("[Timer] All Windows notification methods failed, falling back to DisplayAlert");
#endif
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Timer] General notification error: {ex.GetType().Name}");
            Console.WriteLine($"[Timer] General exception message: {ex.Message}");
            Console.WriteLine($"[Timer] General exception stack trace: {ex.StackTrace}");

            if (ex.InnerException != null)
            {
                Console.WriteLine($"[Timer] General inner exception: {ex.InnerException.Message}");
            }
        }

        // Final fallback - use MAUI DisplayAlert
        try
        {
            Console.WriteLine("[Timer] Using DisplayAlert fallback");
            await DisplayAlert(title, message, "OK");
        }
        catch (Exception fallbackEx)
        {
            Console.WriteLine($"[Timer] Even DisplayAlert failed: {fallbackEx.Message}");
        }
    }

    #endregion

    public void Dispose()
    {
        _workTimer?.Dispose();
        _countdownTimer?.Dispose();
        _currentServiceScope?.Dispose();
        _configWatcher?.Dispose();
    }
}