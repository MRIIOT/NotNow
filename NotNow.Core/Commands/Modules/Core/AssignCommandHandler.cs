using NotNow.Core.Commands.Framework;

namespace NotNow.Core.Commands.Modules.Core;

public class AssignCommandArgs : CommandArgs { }

public class AssignCommandHandler : CommandHandler<AssignCommandArgs>
{
    public override async Task<CommandResult> ExecuteAsync(CommandExecutionContext context, AssignCommandArgs args)
    {
        var user = args.GetParameter<string>("user");

        if (string.IsNullOrEmpty(user))
        {
            return CommandResult.Failure("User is required");
        }

        // Normalize username
        if (!user.StartsWith("@"))
            user = "@" + user;

        var result = new
        {
            PreviousAssignee = null as string, // Would get from current state
            NewAssignee = user,
            AssignedBy = context.User,
            AssignedAt = DateTime.UtcNow
        };

        return await Task.FromResult(CommandResult.Ok($"Issue assigned to {user}", result));
    }
}