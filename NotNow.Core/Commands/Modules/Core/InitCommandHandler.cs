using NotNow.Core.Commands.Framework;
using NotNow.GitHubService.Interfaces;

namespace NotNow.Core.Commands.Modules.Core;

public class InitCommandArgs : CommandArgs { }

public class InitCommandHandler : CommandHandler<InitCommandArgs>
{
    private readonly IGitHubService _githubService;

    public InitCommandHandler(IGitHubService githubService)
    {
        _githubService = githubService;
    }

    public override async Task<CommandResult> ExecuteAsync(CommandExecutionContext context, InitCommandArgs args)
    {
        var type = args.GetOption<string>("type", "task");
        var priority = args.GetOption<string>("priority", "medium");
        var workflow = args.GetOption<string>("workflow", "standard");

        // Initialize issue metadata
        var metadata = new
        {
            Initialized = true,
            InitializedAt = DateTime.UtcNow,
            Type = type,
            Priority = priority,
            Workflow = workflow,
            User = context.User
        };

        return CommandResult.Ok($"Issue initialized as {type} with {priority} priority", metadata);
    }
}