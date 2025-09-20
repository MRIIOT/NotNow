using NotNow.Core.Commands.Framework;
using NotNow.Core.Commands.Parser;

namespace NotNow.Core.Commands.Execution;

public interface ICommandExecutor
{
    Task<ExecutionResult> ExecuteAsync(string text, CommandExecutionContext context);
    Task<ExecutionResult> ExecuteCommandsAsync(List<ParsedCommand> commands, CommandExecutionContext context);
}

public class ExecutionResult
{
    public List<CommandResult> Results { get; set; } = new();
    public bool Success => Results.All(r => r.Success);
    public string Summary => GetSummary();

    private string GetSummary()
    {
        if (Results.Count == 0)
            return "No commands executed";

        var successful = Results.Count(r => r.Success);
        var failed = Results.Count(r => !r.Success);

        if (failed == 0)
            return $"All {successful} command(s) executed successfully";

        return $"{successful} succeeded, {failed} failed";
    }
}