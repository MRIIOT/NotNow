using NotNow.Core.Commands.Framework;
using NotNow.Core.Commands.Registry;

namespace NotNow.Core.Console;

public interface ICommandAutoCompleter
{
    List<string> GetSuggestions(string input, CommandContext context);
    CommandHelp? GetCommandHelp(string commandName);
    string FormatSuggestion(string suggestion, string input);
}

public class CommandAutoCompleter : ICommandAutoCompleter
{
    private readonly ICommandRegistry _registry;

    public CommandAutoCompleter(ICommandRegistry registry)
    {
        _registry = registry;
    }

    public List<string> GetSuggestions(string input, CommandContext context)
    {
        if (string.IsNullOrWhiteSpace(input))
            return _registry.GetCommandNames(context);

        // Check if we're completing a command name or parameters
        var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0)
            return _registry.GetCommandNames(context);

        // If input ends with space, we're looking for parameter/option suggestions
        if (input.EndsWith(" "))
        {
            return GetParameterSuggestions(parts[0], context);
        }

        // Still typing command name
        if (parts.Length == 1)
        {
            return _registry.GetCommandSuggestions(parts[0], context);
        }

        // Working on parameters/options
        var lastPart = parts[^1];
        if (lastPart.StartsWith("--"))
        {
            return GetOptionSuggestions(parts[0], lastPart.Substring(2));
        }
        else if (lastPart.StartsWith("-"))
        {
            return GetShortOptionSuggestions(parts[0], lastPart.Substring(1));
        }

        return new List<string>();
    }

    private List<string> GetParameterSuggestions(string commandName, CommandContext context)
    {
        var command = _registry.GetCommand(commandName);
        if (command == null || !command.Context.HasFlag(context))
            return new List<string>();

        var suggestions = new List<string>();

        // Add parameter hints
        var requiredParams = command.Schema.Parameters
            .Where(p => p.Required)
            .Select(p => $"<{p.Name}>")
            .ToList();

        if (requiredParams.Any())
        {
            suggestions.Add(string.Join(" ", requiredParams));
        }

        // Add common options
        foreach (var option in command.Schema.Options)
        {
            suggestions.Add($"--{option.LongName}");
            if (!string.IsNullOrEmpty(option.ShortName))
            {
                suggestions.Add($"-{option.ShortName}");
            }
        }

        return suggestions;
    }

    private List<string> GetOptionSuggestions(string commandName, string prefix)
    {
        var command = _registry.GetCommand(commandName);
        if (command == null)
            return new List<string>();

        return command.Schema.Options
            .Where(o => o.LongName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(o => $"--{o.LongName}")
            .ToList();
    }

    private List<string> GetShortOptionSuggestions(string commandName, string prefix)
    {
        var command = _registry.GetCommand(commandName);
        if (command == null)
            return new List<string>();

        return command.Schema.Options
            .Where(o => !string.IsNullOrEmpty(o.ShortName) &&
                       o.ShortName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(o => $"-{o.ShortName}")
            .ToList();
    }

    public CommandHelp? GetCommandHelp(string commandName)
    {
        var command = _registry.GetCommand(commandName);
        if (command == null)
            return null;

        return new CommandHelp
        {
            Name = command.Name,
            Aliases = command.Aliases,
            Description = command.Description,
            Parameters = command.Schema.Parameters.Select(p => new ParameterHelp
            {
                Name = p.Name,
                Type = p.Type.Name,
                Required = p.Required,
                Description = p.Description
            }).ToList(),
            Options = command.Schema.Options.Select(o => new OptionHelp
            {
                LongName = o.LongName,
                ShortName = o.ShortName,
                Type = o.Type.Name,
                Required = o.Required,
                Description = o.Description
            }).ToList()
        };
    }

    public string FormatSuggestion(string suggestion, string input)
    {
        if (string.IsNullOrEmpty(input))
            return suggestion;

        var matchIndex = suggestion.IndexOf(input, StringComparison.OrdinalIgnoreCase);
        if (matchIndex < 0)
            return suggestion;

        // Highlight matching part
        var before = suggestion.Substring(0, matchIndex);
        var match = suggestion.Substring(matchIndex, input.Length);
        var after = suggestion.Substring(matchIndex + input.Length);

        return $"{before}[{match}]{after}";
    }
}

public class CommandHelp
{
    public string Name { get; set; } = "";
    public string[] Aliases { get; set; } = Array.Empty<string>();
    public string Description { get; set; } = "";
    public List<ParameterHelp> Parameters { get; set; } = new();
    public List<OptionHelp> Options { get; set; } = new();
}

public class ParameterHelp
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public bool Required { get; set; }
    public string Description { get; set; } = "";
}

public class OptionHelp
{
    public string LongName { get; set; } = "";
    public string ShortName { get; set; } = "";
    public string Type { get; set; } = "";
    public bool Required { get; set; }
    public string Description { get; set; } = "";
}