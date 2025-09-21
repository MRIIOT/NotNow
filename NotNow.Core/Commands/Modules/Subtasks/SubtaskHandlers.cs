using Microsoft.Extensions.DependencyInjection;
using NotNow.Core.Commands.Framework;
using NotNow.Core.Models;
using NotNow.Core.Services;
using System.Linq;
using System.Text.RegularExpressions;

namespace NotNow.Core.Commands.Modules.Subtasks;

public class SubtaskArgs : CommandArgs { }

public class SubtaskHandler : CommandHandler<SubtaskArgs>
{
    private readonly IServiceProvider _serviceProvider;

    public SubtaskHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public override async Task<CommandResult> ExecuteAsync(CommandExecutionContext context, SubtaskArgs args)
    {
        var action = args.GetParameter<string>("action", "add").ToLower();
        var title = args.GetParameter<string>("title");

        // Get the state service
        var stateService = _serviceProvider.GetRequiredService<IIssueStateService>();
        var issueState = stateService.GetOrCreateState(context.IssueNumber);

        // Use subtasks from the parsed state
        var subtasks = issueState.Subtasks;

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

        string id;
        if (!string.IsNullOrEmpty(args.GetOption<string>("id")))
        {
            id = args.GetOption<string>("id")!;
        }
        else
        {
            // Generate a unique ID by finding the highest existing st# and incrementing
            var existingIds = subtasks
                .Where(s => s.Id?.StartsWith("st") == true)
                .Select(s =>
                {
                    if (int.TryParse(s.Id.Substring(2), out int num))
                        return num;
                    return 0;
                })
                .DefaultIfEmpty(0)
                .Max();

            id = $"st{existingIds + 1}";
        }

        var estimate = args.GetOption<string>("estimate");
        var assignee = args.GetOption<string>("assignee");

        var subtask = new Subtask
        {
            Id = id,
            Title = title,
            Status = "pending",
            Estimate = estimate,
            Assignee = assignee
        };

        subtasks.Add(subtask);

        return await Task.FromResult(CommandResult.Ok($"Added subtask '{title}' with ID '{id}'", new
        {
            Id = id,
            Title = title,
            Estimate = estimate,
            Assignee = assignee
        }));
    }

    private async Task<CommandResult> ListSubtasks(List<Subtask> subtasks)
    {
        if (subtasks.Count == 0)
            return await Task.FromResult(CommandResult.Ok("No subtasks", new { Count = 0 }));

        var result = new
        {
            Count = subtasks.Count,
            Completed = subtasks.Count(s => s.Status == "done"),
            Subtasks = subtasks.Select(s => new
            {
                s.Id,
                s.Title,
                Status = s.Status,
                IsCompleted = s.Status == "done",
                Estimate = s.Estimate,
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
    private readonly IServiceProvider _serviceProvider;

    public CompleteSubtaskHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public override async Task<CommandResult> ExecuteAsync(CommandExecutionContext context, CompleteSubtaskArgs args)
    {
        var id = args.GetParameter<string>("id");
        var time = args.GetOption<string>("time");
        var notes = args.GetOption<string>("notes");

        // Get the state service
        var stateService = _serviceProvider.GetRequiredService<IIssueStateService>();
        var issueState = stateService.GetOrCreateState(context.IssueNumber);
        var subtasks = issueState.Subtasks;

        var subtask = subtasks.FirstOrDefault(s => s.Id == id);
        if (subtask == null)
            return CommandResult.Failure($"Subtask '{id}' not found");

        if (subtask.Status == "done")
            return CommandResult.Failure($"Subtask '{id}' is already completed");

        subtask.Status = "done";
        subtask.CompletedAt = DateTime.UtcNow;

        var result = new
        {
            SubtaskId = id,
            Title = subtask.Title,
            CompletedAt = subtask.CompletedAt,
            Notes = notes
        };

        var message = $"Completed subtask '{subtask.Title}'";
        if (!string.IsNullOrEmpty(time))
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
    private readonly IServiceProvider _serviceProvider;

    public ReopenSubtaskHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public override async Task<CommandResult> ExecuteAsync(CommandExecutionContext context, ReopenSubtaskArgs args)
    {
        var id = args.GetParameter<string>("id");
        var reason = args.GetOption<string>("reason");

        // Get the state service
        var stateService = _serviceProvider.GetRequiredService<IIssueStateService>();
        var issueState = stateService.GetOrCreateState(context.IssueNumber);
        var subtasks = issueState.Subtasks;

        var subtask = subtasks.FirstOrDefault(s => s.Id == id);
        if (subtask == null)
            return CommandResult.Failure($"Subtask '{id}' not found");

        if (subtask.Status != "done")
            return CommandResult.Failure($"Subtask '{id}' is not completed");

        subtask.Status = "pending";

        var result = new
        {
            SubtaskId = id,
            Title = subtask.Title,
            Reason = reason
        };

        var message = $"Reopened subtask '{subtask.Title}'";
        if (!string.IsNullOrEmpty(reason))
            message += $": {reason}";

        return await Task.FromResult(CommandResult.Ok(message, result));
    }
}