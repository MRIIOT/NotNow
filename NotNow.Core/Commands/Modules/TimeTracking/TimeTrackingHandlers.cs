using NotNow.Core.Commands.Framework;
using System.Text.RegularExpressions;

namespace NotNow.Core.Commands.Modules.TimeTracking;

public class TimeCommandArgs : CommandArgs { }

public class TimeCommandHandler : CommandHandler<TimeCommandArgs>
{
    public override async Task<CommandResult> ExecuteAsync(CommandExecutionContext context, TimeCommandArgs args)
    {
        var duration = args.GetParameter<string>("duration");
        var description = args.GetOption<string>("description");
        var date = args.GetOption<DateTime>("date", DateTime.Today);

        var timeSpan = ParseDuration(duration);
        if (timeSpan == TimeSpan.Zero)
        {
            return CommandResult.Failure("Invalid duration format. Use format like '2h30m'");
        }

        var result = new
        {
            Duration = timeSpan,
            TotalHours = timeSpan.TotalHours,
            Description = description,
            Date = date,
            LoggedBy = context.User,
            LoggedAt = DateTime.UtcNow
        };

        var message = $"Logged {duration}";
        if (!string.IsNullOrEmpty(description))
            message += $" - {description}";

        return await Task.FromResult(CommandResult.Ok(message, result));
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

public class StartWorkArgs : CommandArgs { }

public class StartWorkHandler : CommandHandler<StartWorkArgs>
{
    // In a real implementation, this would interact with a session service
    internal static readonly Dictionary<string, DateTime> _activeSessions = new();

    public override async Task<CommandResult> ExecuteAsync(CommandExecutionContext context, StartWorkArgs args)
    {
        var sessionKey = $"{context.IssueNumber}:{context.User}";

        if (_activeSessions.ContainsKey(sessionKey))
        {
            return CommandResult.Failure("Work session already in progress. Stop it first.");
        }

        var description = args.GetOption<string>("description");
        var startTime = DateTime.UtcNow;

        _activeSessions[sessionKey] = startTime;

        var result = new
        {
            SessionId = Guid.NewGuid().ToString(),
            StartedAt = startTime,
            Description = description,
            User = context.User
        };

        var message = "Work session started";
        if (!string.IsNullOrEmpty(description))
            message += $": {description}";

        return await Task.FromResult(CommandResult.Ok(message, result));
    }
}

public class StopWorkArgs : CommandArgs { }

public class StopWorkHandler : CommandHandler<StopWorkArgs>
{
    private static readonly Dictionary<string, DateTime> _activeSessions = StartWorkHandler._activeSessions;

    public override async Task<CommandResult> ExecuteAsync(CommandExecutionContext context, StopWorkArgs args)
    {
        var sessionKey = $"{context.IssueNumber}:{context.User}";

        if (!_activeSessions.TryGetValue(sessionKey, out var startTime))
        {
            return CommandResult.Failure("No active work session found");
        }

        var description = args.GetOption<string>("description");
        var endTime = DateTime.UtcNow;
        var duration = endTime - startTime;

        _activeSessions.Remove(sessionKey);

        var result = new
        {
            StartedAt = startTime,
            EndedAt = endTime,
            Duration = duration,
            TotalHours = duration.TotalHours,
            Description = description,
            User = context.User
        };

        var message = $"Work session stopped. Duration: {FormatDuration(duration)}";
        if (!string.IsNullOrEmpty(description))
            message += $" - {description}";

        return await Task.FromResult(CommandResult.Ok(message, result));
    }

    private string FormatDuration(TimeSpan duration)
    {
        var hours = (int)duration.TotalHours;
        var minutes = duration.Minutes;
        return $"{hours}h{minutes}m";
    }
}

public class SessionInfoArgs : CommandArgs { }

public class SessionInfoHandler : CommandHandler<SessionInfoArgs>
{
    private static readonly Dictionary<string, DateTime> _activeSessions = StartWorkHandler._activeSessions;

    public override async Task<CommandResult> ExecuteAsync(CommandExecutionContext context, SessionInfoArgs args)
    {
        var sessionKey = $"{context.IssueNumber}:{context.User}";

        if (!_activeSessions.TryGetValue(sessionKey, out var startTime))
        {
            return CommandResult.Ok("No active work session", new { Active = false });
        }

        var currentDuration = DateTime.UtcNow - startTime;

        var result = new
        {
            Active = true,
            StartedAt = startTime,
            CurrentDuration = currentDuration,
            CurrentHours = currentDuration.TotalHours,
            User = context.User
        };

        var message = $"Active session: {FormatDuration(currentDuration)} (started {startTime:HH:mm})";
        return await Task.FromResult(CommandResult.Ok(message, result));
    }

    private string FormatDuration(TimeSpan duration)
    {
        var hours = (int)duration.TotalHours;
        var minutes = duration.Minutes;
        return $"{hours}h{minutes}m";
    }
}

public class TimeSpentArgs : CommandArgs { }

public class TimeSpentHandler : CommandHandler<TimeSpentArgs>
{
    public override async Task<CommandResult> ExecuteAsync(CommandExecutionContext context, TimeSpentArgs args)
    {
        var user = args.GetOption<string>("user");
        var from = args.GetOption<DateTime>("from", DateTime.Today.AddDays(-30));
        var to = args.GetOption<DateTime>("to", DateTime.Today);

        // In real implementation, this would aggregate from stored time entries
        var result = new
        {
            TotalHours = 42.5, // Mock data
            Period = new { From = from, To = to },
            FilteredBy = user,
            IssueNumber = context.IssueNumber
        };

        var message = $"Total time spent: 42h30m";
        if (!string.IsNullOrEmpty(user))
            message += $" by {user}";
        message += $" ({from:yyyy-MM-dd} to {to:yyyy-MM-dd})";

        return await Task.FromResult(CommandResult.Ok(message, result));
    }
}