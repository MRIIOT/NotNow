namespace NotNow.Core.Commands.Framework;

public interface ICommandModule
{
    string ModuleName { get; }
    string Version { get; }
    List<CommandRegistration> GetCommands();
    Task OnModuleInitialize(IServiceProvider services);
}

public class CommandRegistration
{
    public string Name { get; set; } = string.Empty;
    public string[] Aliases { get; set; } = Array.Empty<string>();
    public string Description { get; set; } = string.Empty;
    public CommandContext Context { get; set; }
    public Type HandlerType { get; set; } = typeof(ICommandHandler);
    public CommandSchema Schema { get; set; } = new();
}

[Flags]
public enum CommandContext
{
    None = 0,
    IssueBody = 1,
    Comment = 2,
    Both = IssueBody | Comment
}