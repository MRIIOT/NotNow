using Microsoft.Extensions.DependencyInjection;
using NotNow.Core.Commands.Framework;
using NotNow.Core.Commands.Parser;
using NotNow.Core.Commands.Registry;

namespace NotNow.Core.Commands.Execution;

public class CommandExecutor : ICommandExecutor
{
    private readonly ICommandRegistry _registry;
    private readonly IServiceProvider _services;
    private readonly ICommandParser _parser;

    public CommandExecutor(
        ICommandRegistry registry,
        IServiceProvider services,
        ICommandParser parser)
    {
        _registry = registry;
        _services = services;
        _parser = parser;
    }

    public async Task<ExecutionResult> ExecuteAsync(string text, CommandExecutionContext context)
    {
        var parseResult = _parser.Parse(text, context.CommandContext);
        return await ExecuteCommandsAsync(parseResult.Commands, context);
    }

    public async Task<ExecutionResult> ExecuteCommandsAsync(List<ParsedCommand> commands, CommandExecutionContext context)
    {
        var results = new List<CommandResult>();

        foreach (var command in commands)
        {
            if (!command.IsValid)
            {
                results.Add(CommandResult.Failure(command.Error ?? "Invalid command"));
                continue;
            }

            if (command.Registration == null)
            {
                results.Add(CommandResult.Failure($"Command '{command.Name}' not found"));
                continue;
            }

            try
            {
                var handler = _services.GetService(command.Registration.HandlerType) as ICommandHandler;

                if (handler == null)
                {
                    results.Add(CommandResult.Failure($"Handler not found for command '{command.Name}'"));
                    continue;
                }

                var args = BuildCommandArgs(command);
                context.RawText = command.RawText;

                var result = await handler.ExecuteAsync(context, args);
                results.Add(result);
            }
            catch (Exception ex)
            {
                results.Add(CommandResult.Failure($"Error executing '{command.Name}': {ex.Message}"));
            }
        }

        return new ExecutionResult { Results = results };
    }

    private CommandArgs BuildCommandArgs(ParsedCommand command)
    {
        return new CommandArgs
        {
            Parameters = command.Arguments,
            Options = command.Options
        };
    }
}