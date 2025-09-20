using NotNow.Core.Commands.Framework;

namespace NotNow.Core.Commands.Modules.Core;

public class StatusCommandArgs : CommandArgs { }

public class StatusCommandHandler : CommandHandler<StatusCommandArgs>
{
    public override async Task<CommandResult> ExecuteAsync(CommandExecutionContext context, StatusCommandArgs args)
    {
        var status = args.GetParameter<string>("status");
        var reason = args.GetOption<string>("reason");

        if (string.IsNullOrEmpty(status))
        {
            return CommandResult.Failure("Status is required");
        }

        var validStatuses = new[] { "todo", "in_progress", "review", "testing", "done", "blocked" };
        if (!validStatuses.Contains(status.ToLower()))
        {
            return CommandResult.Failure($"Invalid status. Valid values: {string.Join(", ", validStatuses)}");
        }

        var result = new
        {
            PreviousStatus = "todo", // Would get from current state
            NewStatus = status,
            Reason = reason,
            UpdatedBy = context.User,
            UpdatedAt = DateTime.UtcNow
        };

        var message = $"Status changed to '{status}'";
        if (!string.IsNullOrEmpty(reason))
            message += $": {reason}";

        return await Task.FromResult(CommandResult.Ok(message, result));
    }
}