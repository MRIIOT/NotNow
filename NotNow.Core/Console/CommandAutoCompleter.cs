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
            return GetParameterSuggestions(parts, context);
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
        else
        {
            // Partial word completion for subcommands
            return GetSubcommandSuggestions(parts);
        }
    }

    private List<string> GetParameterSuggestions(string[] parts, CommandContext context)
    {
        var commandName = parts[0];
        var command = _registry.GetCommand(commandName);
        if (command == null || !command.Context.HasFlag(context))
            return new List<string>();

        var suggestions = new List<string>();

        // Check how many non-option parameters we already have
        var nonOptionParts = parts.Skip(1).Where(p => !p.StartsWith("-")).ToList();

        // Special handling for commands with subcommands
        if (commandName.Equals("subtask", StringComparison.OrdinalIgnoreCase))
        {
            if (nonOptionParts.Count == 0)
            {
                // First parameter - show actions
                suggestions.Add("add");
                suggestions.Add("complete");
                suggestions.Add("remove");
                suggestions.Add("list");
            }
            else if (nonOptionParts[0].Equals("add", StringComparison.OrdinalIgnoreCase))
            {
                if (nonOptionParts.Count == 1)
                {
                    suggestions.Add("\"<title>\"");
                }
            }
        }
        else if (commandName.Equals("status", StringComparison.OrdinalIgnoreCase) && nonOptionParts.Count == 0)
        {
            suggestions.Add("todo");
            suggestions.Add("in_progress");
            suggestions.Add("done");
            suggestions.Add("blocked");
        }
        else if (commandName.Equals("priority", StringComparison.OrdinalIgnoreCase) && nonOptionParts.Count == 0)
        {
            suggestions.Add("low");
            suggestions.Add("medium");
            suggestions.Add("high");
            suggestions.Add("critical");
        }
        else if (commandName.Equals("type", StringComparison.OrdinalIgnoreCase) && nonOptionParts.Count == 0)
        {
            suggestions.Add("bug");
            suggestions.Add("feature");
            suggestions.Add("task");
            suggestions.Add("enhancement");
        }
        else if (commandName.Equals("assign", StringComparison.OrdinalIgnoreCase) && nonOptionParts.Count == 0)
        {
            suggestions.Add("@<username>");
        }
        else
        {
            // Generic parameter hints
            var remainingParams = command.Schema.Parameters.Skip(nonOptionParts.Count);
            foreach (var param in remainingParams.Take(1))
            {
                if (param.Required)
                    suggestions.Add($"<{param.Name}>");
            }
        }

        // Always add available options
        foreach (var option in command.Schema.Options)
        {
            // Don't suggest options that are already in the command
            if (!parts.Any(p => p.Equals($"--{option.LongName}", StringComparison.OrdinalIgnoreCase)))
            {
                suggestions.Add($"--{option.LongName}");
            }
            if (!string.IsNullOrEmpty(option.ShortName) &&
                !parts.Any(p => p.Equals($"-{option.ShortName}", StringComparison.OrdinalIgnoreCase)))
            {
                suggestions.Add($"-{option.ShortName}");
            }
        }

        return suggestions;
    }

    private List<string> GetSubcommandSuggestions(string[] parts)
    {
        var commandName = parts[0];
        var lastPart = parts[^1];
        var suggestions = new List<string>();

        // Special handling for subtask command
        if (commandName.Equals("subtask", StringComparison.OrdinalIgnoreCase))
        {
            if (parts.Length == 2)
            {
                // Completing the action
                var actions = new[] { "add", "complete", "remove", "list" };
                suggestions.AddRange(actions.Where(a => a.StartsWith(lastPart, StringComparison.OrdinalIgnoreCase)));
            }
        }
        else if (commandName.Equals("status", StringComparison.OrdinalIgnoreCase) && parts.Length == 2)
        {
            var statuses = new[] { "todo", "in_progress", "done", "blocked" };
            suggestions.AddRange(statuses.Where(s => s.StartsWith(lastPart, StringComparison.OrdinalIgnoreCase)));
        }
        else if (commandName.Equals("priority", StringComparison.OrdinalIgnoreCase) && parts.Length == 2)
        {
            var priorities = new[] { "low", "medium", "high", "critical" };
            suggestions.AddRange(priorities.Where(p => p.StartsWith(lastPart, StringComparison.OrdinalIgnoreCase)));
        }
        else if (commandName.Equals("type", StringComparison.OrdinalIgnoreCase) && parts.Length == 2)
        {
            var types = new[] { "bug", "feature", "task", "enhancement" };
            suggestions.AddRange(types.Where(t => t.StartsWith(lastPart, StringComparison.OrdinalIgnoreCase)));
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