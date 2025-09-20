using NotNow.Core.Commands.Framework;
using System.Text.RegularExpressions;

namespace NotNow.Core.Commands.Modules.Core;

public class DueCommandArgs : CommandArgs { }

public class DueCommandHandler : CommandHandler<DueCommandArgs>
{
    public override async Task<CommandResult> ExecuteAsync(CommandExecutionContext context, DueCommandArgs args)
    {
        var date = args.GetParameter<DateTime>("date");

        if (date < DateTime.Today)
        {
            return CommandResult.Failure("Due date cannot be in the past");
        }

        var result = new
        {
            DueDate = date,
            SetBy = context.User,
            SetAt = DateTime.UtcNow
        };

        return await Task.FromResult(CommandResult.Ok($"Due date set to {date:yyyy-MM-dd}", result));
    }
}

public class EstimateCommandArgs : CommandArgs { }

public class EstimateCommandHandler : CommandHandler<EstimateCommandArgs>
{
    public override async Task<CommandResult> ExecuteAsync(CommandExecutionContext context, EstimateCommandArgs args)
    {
        var duration = args.GetParameter<string>("duration");
        var update = args.GetOption<bool>("update");

        var timeSpan = ParseDuration(duration);
        if (timeSpan == TimeSpan.Zero)
        {
            return CommandResult.Failure("Invalid duration format. Use format like '2h30m'");
        }

        var result = new
        {
            Estimate = timeSpan,
            TotalHours = timeSpan.TotalHours,
            IsUpdate = update,
            SetBy = context.User,
            SetAt = DateTime.UtcNow
        };

        var action = update ? "updated" : "set";
        return await Task.FromResult(CommandResult.Ok($"Estimate {action} to {duration}", result));
    }

    private TimeSpan ParseDuration(string value)
    {
        var pattern = @"(?:(\d+)h)?(?:(\d+)m)?";
        var match = Regex.Match(value, pattern, RegexOptions.IgnoreCase);

        if (match.Success)
        {
            var hours = match.Groups[1].Success ? int.Parse(match.Groups[1].Value) : 0;
            var minutes = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 0;
            return new TimeSpan(hours, minutes, 0);
        }

        return TimeSpan.Zero;
    }
}

public class TagsCommandArgs : CommandArgs { }

public class TagsCommandHandler : CommandHandler<TagsCommandArgs>
{
    public override async Task<CommandResult> ExecuteAsync(CommandExecutionContext context, TagsCommandArgs args)
    {
        var action = args.GetParameter<string>("action", "add").ToLower();
        var tagsString = args.GetParameter<string>("tags");

        if (string.IsNullOrEmpty(tagsString) && action != "list")
        {
            return CommandResult.Failure("Tags are required");
        }

        var tags = tagsString?.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim())
            .ToList() ?? new List<string>();

        var result = new
        {
            Action = action,
            Tags = tags,
            UpdatedBy = context.User,
            UpdatedAt = DateTime.UtcNow
        };

        var message = action switch
        {
            "add" => $"Added tags: {string.Join(", ", tags)}",
            "remove" => $"Removed tags: {string.Join(", ", tags)}",
            "set" => $"Tags set to: {string.Join(", ", tags)}",
            _ => "Tags updated"
        };

        return await Task.FromResult(CommandResult.Ok(message, result));
    }
}

public class PriorityCommandArgs : CommandArgs { }

public class PriorityCommandHandler : CommandHandler<PriorityCommandArgs>
{
    public override async Task<CommandResult> ExecuteAsync(CommandExecutionContext context, PriorityCommandArgs args)
    {
        var level = args.GetParameter<string>("level")?.ToLower();

        var validLevels = new[] { "low", "medium", "high", "critical" };
        if (!validLevels.Contains(level))
        {
            return CommandResult.Failure($"Invalid priority. Valid values: {string.Join(", ", validLevels)}");
        }

        var result = new
        {
            Priority = level,
            SetBy = context.User,
            SetAt = DateTime.UtcNow
        };

        return await Task.FromResult(CommandResult.Ok($"Priority set to {level}", result));
    }
}