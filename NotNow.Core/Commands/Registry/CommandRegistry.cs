using NotNow.Core.Commands.Framework;

namespace NotNow.Core.Commands.Registry;

public class CommandRegistry : ICommandRegistry
{
    private readonly Dictionary<string, CommandRegistration> _commands;
    private readonly Dictionary<string, ICommandModule> _modules;
    private readonly IServiceProvider _services;

    public CommandRegistry(IServiceProvider services)
    {
        _commands = new Dictionary<string, CommandRegistration>(StringComparer.OrdinalIgnoreCase);
        _modules = new Dictionary<string, ICommandModule>();
        _services = services;
    }

    public void RegisterModule(ICommandModule module)
    {
        if (_modules.ContainsKey(module.ModuleName))
            throw new InvalidOperationException($"Module {module.ModuleName} already registered");

        _modules[module.ModuleName] = module;

        foreach (var command in module.GetCommands())
        {
            RegisterCommand(command);
        }

        module.OnModuleInitialize(_services).Wait();
    }

    public void RegisterCommand(CommandRegistration registration)
    {
        _commands[registration.Name] = registration;

        if (registration.Aliases != null)
        {
            foreach (var alias in registration.Aliases)
            {
                _commands[alias] = registration;
            }
        }
    }

    public CommandRegistration? GetCommand(string name)
    {
        return _commands.TryGetValue(name, out var registration) ? registration : null;
    }

    public List<CommandRegistration> GetAllCommands()
    {
        return _commands.Values.Distinct().ToList();
    }

    public List<CommandRegistration> GetCommandsForContext(CommandContext context)
    {
        return _commands.Values
            .Where(c => c.Context.HasFlag(context))
            .Distinct()
            .ToList();
    }

    public List<ICommandModule> GetModules()
    {
        return _modules.Values.ToList();
    }

    public bool IsCommandAvailable(string name, CommandContext context)
    {
        var command = GetCommand(name);
        return command != null && command.Context.HasFlag(context);
    }

    public List<string> GetCommandNames(CommandContext? context = null)
    {
        var query = _commands.Values.Distinct();

        if (context.HasValue)
        {
            query = query.Where(c => c.Context.HasFlag(context.Value));
        }

        return query.Select(c => c.Name).Distinct().ToList();
    }

    public List<string> GetCommandSuggestions(string prefix, CommandContext context)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            return GetCommandNames(context);

        return GetCommandsForContext(context)
            .SelectMany(c => new[] { c.Name }.Concat(c.Aliases ?? Array.Empty<string>()))
            .Where(name => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .OrderBy(n => n)
            .ToList();
    }
}