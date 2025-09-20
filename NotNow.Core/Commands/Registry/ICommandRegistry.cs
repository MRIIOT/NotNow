using NotNow.Core.Commands.Framework;

namespace NotNow.Core.Commands.Registry;

public interface ICommandRegistry
{
    void RegisterModule(ICommandModule module);
    void RegisterCommand(CommandRegistration registration);
    CommandRegistration? GetCommand(string name);
    List<CommandRegistration> GetAllCommands();
    List<CommandRegistration> GetCommandsForContext(CommandContext context);
    List<ICommandModule> GetModules();
    bool IsCommandAvailable(string name, CommandContext context);
    List<string> GetCommandNames(CommandContext? context = null);
    List<string> GetCommandSuggestions(string prefix, CommandContext context);
}