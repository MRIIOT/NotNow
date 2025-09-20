using NotNow.Core.Commands.Framework;
using System.Text.RegularExpressions;

namespace NotNow.Core.Commands.Modules.Subtasks;

public class SubtaskArgs : CommandArgs { }

public class SubtaskHandler : CommandHandler<SubtaskArgs>
{
    // Mock storage for demo
    internal static readonly Dictionary<int, List<Subtask>> _subtasks = new();

    public override async Task<CommandResult> ExecuteAsync(CommandExecutionContext context, SubtaskArgs args)
    {
        var action = args.GetParameter<string>("action", "add").ToLower();
        var title = args.GetParameter<string>("title");

        if (!_subtasks.ContainsKey(context.IssueNumber))
            _subtasks[context.IssueNumber] = new List<Subtask>();

        var subtasks = _subtasks[context.IssueNumber];

        switch (action)
        {
            case "add":
                return await AddSubtask(context, args, title, subtasks);

            case "list":
                return await ListSubtasks(subtasks);

            case "remove":
                var removeId = args.GetOption<string>("id");
                if (string.IsNullOrEmpty(removeId))
                    return CommandResult.Failure("Subtask ID required for remove action");

                var toRemove = subtasks.FirstOrDefault(s => s.Id == removeId);
                if (toRemove == null)
                    return CommandResult.Failure($"Subtask '{removeId}' not found");

                subtasks.Remove(toRemove);
                return CommandResult.Ok($"Removed subtask '{toRemove.Title}'");

            default:
                return CommandResult.Failure($"Unknown action: {action}. Valid actions: add, list, remove");
        }
    }

    private async Task<CommandResult> AddSubtask(CommandExecutionContext context, SubtaskArgs args, string? title, List<Subtask> subtasks)
    {
        if (string.IsNullOrEmpty(title))
            return CommandResult.Failure("Title required for adding subtask");

        var id = args.GetOption<string>("id") ?? $"st{subtasks.Count + 1}";
        var estimate = args.GetOption<string>("estimate");
        var depends = args.GetOption<string>("depends");
        var assignee = args.GetOption<string>("assignee");

        var subtask = new Subtask
        {
            Id = id,
            Title = title,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = context.User,
            Estimate = ParseDuration(estimate),
            Dependencies = depends?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>(),
            Assignee = assignee,
            IsCompleted = false
        };

        subtasks.Add(subtask);

        return await Task.FromResult(CommandResult.Ok($"Added subtask '{title}' with ID '{id}'", subtask));
    }

    private async Task<CommandResult> ListSubtasks(List<Subtask> subtasks)
    {
        if (subtasks.Count == 0)
            return await Task.FromResult(CommandResult.Ok("No subtasks", new { Count = 0 }));

        var result = new
        {
            Count = subtasks.Count,
            Completed = subtasks.Count(s => s.IsCompleted),
            Subtasks = subtasks.Select(s => new
            {
                s.Id,
                s.Title,
                s.IsCompleted,
                EstimateHours = s.Estimate?.TotalHours,
                s.Assignee
            })
        };

        var message = $"Found {subtasks.Count} subtasks ({result.Completed} completed)";
        return await Task.FromResult(CommandResult.Ok(message, result));
    }

    private TimeSpan? ParseDuration(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        var pattern = @"(?:(\d+)h)?(?:(\d+)m)?";
        var match = Regex.Match(value, pattern, RegexOptions.IgnoreCase);

        if (match.Success)
        {
            var hours = match.Groups[1].Success ? int.Parse(match.Groups[1].Value) : 0;
            var minutes = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 0;
            return new TimeSpan(hours, minutes, 0);
        }

        return null;
    }
}

public class CompleteSubtaskArgs : CommandArgs { }

public class CompleteSubtaskHandler : CommandHandler<CompleteSubtaskArgs>
{
    private static readonly Dictionary<int, List<Subtask>> _subtasks = SubtaskHandler._subtasks;

    public override async Task<CommandResult> ExecuteAsync(CommandExecutionContext context, CompleteSubtaskArgs args)
    {
        var id = args.GetParameter<string>("id");
        var time = args.GetOption<string>("time");
        var notes = args.GetOption<string>("notes");

        if (!_subtasks.TryGetValue(context.IssueNumber, out var subtasks))
            return CommandResult.Failure("No subtasks found for this issue");

        var subtask = subtasks.FirstOrDefault(s => s.Id == id);
        if (subtask == null)
            return CommandResult.Failure($"Subtask '{id}' not found");

        if (subtask.IsCompleted)
            return CommandResult.Failure($"Subtask '{id}' is already completed");

        subtask.IsCompleted = true;
        subtask.CompletedAt = DateTime.UtcNow;
        subtask.CompletedBy = context.User;
        subtask.TimeSpent = ParseDuration(time);
        subtask.CompletionNotes = notes;

        var result = new
        {
            SubtaskId = id,
            Title = subtask.Title,
            CompletedAt = subtask.CompletedAt,
            TimeSpent = subtask.TimeSpent?.TotalHours,
            Notes = notes
        };

        var message = $"Completed subtask '{subtask.Title}'";
        if (subtask.TimeSpent.HasValue)
            message += $" (time: {time})";

        return await Task.FromResult(CommandResult.Ok(message, result));
    }

    private TimeSpan? ParseDuration(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        var pattern = @"(?:(\d+)h)?(?:(\d+)m)?";
        var match = Regex.Match(value, pattern, RegexOptions.IgnoreCase);

        if (match.Success)
        {
            var hours = match.Groups[1].Success ? int.Parse(match.Groups[1].Value) : 0;
            var minutes = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 0;
            return new TimeSpan(hours, minutes, 0);
        }

        return null;
    }
}

public class ReopenSubtaskArgs : CommandArgs { }

public class ReopenSubtaskHandler : CommandHandler<ReopenSubtaskArgs>
{
    private static readonly Dictionary<int, List<Subtask>> _subtasks = SubtaskHandler._subtasks;

    public override async Task<CommandResult> ExecuteAsync(CommandExecutionContext context, ReopenSubtaskArgs args)
    {
        var id = args.GetParameter<string>("id");
        var reason = args.GetOption<string>("reason");

        if (!_subtasks.TryGetValue(context.IssueNumber, out var subtasks))
            return CommandResult.Failure("No subtasks found for this issue");

        var subtask = subtasks.FirstOrDefault(s => s.Id == id);
        if (subtask == null)
            return CommandResult.Failure($"Subtask '{id}' not found");

        if (!subtask.IsCompleted)
            return CommandResult.Failure($"Subtask '{id}' is not completed");

        subtask.IsCompleted = false;
        subtask.ReopenedAt = DateTime.UtcNow;
        subtask.ReopenedBy = context.User;

        var result = new
        {
            SubtaskId = id,
            Title = subtask.Title,
            ReopenedAt = subtask.ReopenedAt,
            Reason = reason
        };

        var message = $"Reopened subtask '{subtask.Title}'";
        if (!string.IsNullOrEmpty(reason))
            message += $": {reason}";

        return await Task.FromResult(CommandResult.Ok(message, result));
    }
}

// Helper class for subtask data
public class Subtask
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public bool IsCompleted { get; set; }
    public TimeSpan? Estimate { get; set; }
    public TimeSpan? TimeSpent { get; set; }
    public List<string> Dependencies { get; set; } = new();
    public string? Assignee { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = "";
    public DateTime? CompletedAt { get; set; }
    public string? CompletedBy { get; set; }
    public string? CompletionNotes { get; set; }
    public DateTime? ReopenedAt { get; set; }
    public string? ReopenedBy { get; set; }
}