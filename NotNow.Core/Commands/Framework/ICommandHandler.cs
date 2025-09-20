namespace NotNow.Core.Commands.Framework;

public interface ICommandHandler
{
    Task<CommandResult> ExecuteAsync(CommandExecutionContext context, CommandArgs args);
}

public abstract class CommandHandler<TArgs> : ICommandHandler
    where TArgs : CommandArgs, new()
{
    public abstract Task<CommandResult> ExecuteAsync(CommandExecutionContext context, TArgs args);

    public async Task<CommandResult> ExecuteAsync(CommandExecutionContext context, CommandArgs args)
    {
        // Create a new instance of the specific args type and copy properties
        var typedArgs = new TArgs
        {
            Parameters = args.Parameters,
            Options = args.Options
        };

        return await ExecuteAsync(context, typedArgs);
    }
}

public class CommandArgs
{
    public Dictionary<string, object> Parameters { get; set; } = new();
    public Dictionary<string, object> Options { get; set; } = new();

    public T GetParameter<T>(string name, T defaultValue = default!)
    {
        if (Parameters.TryGetValue(name, out var value))
        {
            if (value is T typedValue)
                return typedValue;

            try
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }

        return defaultValue;
    }

    public T GetOption<T>(string name, T defaultValue = default!)
    {
        if (Options.TryGetValue(name, out var value))
        {
            if (value is T typedValue)
                return typedValue;

            try
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }

        return defaultValue;
    }
}

public class CommandExecutionContext
{
    public int IssueNumber { get; set; }
    public string User { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public CommandContext CommandContext { get; set; }
    public string RawText { get; set; } = string.Empty;
}

public class CommandResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public object? Data { get; set; }
    public string? Error { get; set; }

    public static CommandResult Ok(string message, object? data = null)
    {
        return new CommandResult
        {
            Success = true,
            Message = message,
            Data = data
        };
    }

    public static CommandResult Failure(string error)
    {
        return new CommandResult
        {
            Success = false,
            Error = error,
            Message = $"Command failed: {error}"
        };
    }
}