using System.Text.RegularExpressions;
using NotNow.Core.Commands.Framework;
using NotNow.Core.Commands.Registry;

namespace NotNow.Core.Commands.Parser;

public class ModularCommandParser : ICommandParser
{
    private readonly ICommandRegistry _registry;
    private static readonly Regex CommandPattern = new(@"/notnow\s+(\S+)(.*)$",
        RegexOptions.Multiline | RegexOptions.IgnoreCase);

    public ModularCommandParser(ICommandRegistry registry)
    {
        _registry = registry;
    }

    public bool ContainsCommand(string text)
    {
        return CommandPattern.IsMatch(text);
    }

    public ParseResult Parse(string text, CommandContext context)
    {
        var results = new List<ParsedCommand>();
        var matches = CommandPattern.Matches(text);

        foreach (Match match in matches)
        {
            var commandName = match.Groups[1].Value;
            var argumentsText = match.Groups[2].Value.Trim();

            var registration = _registry.GetCommand(commandName);
            if (registration == null)
            {
                results.Add(new ParsedCommand
                {
                    Name = commandName,
                    IsValid = false,
                    Error = $"Unknown command: {commandName}",
                    RawText = match.Value,
                    Position = match.Index
                });
                continue;
            }

            if (!registration.Context.HasFlag(context))
            {
                results.Add(new ParsedCommand
                {
                    Name = commandName,
                    Registration = registration,
                    IsValid = false,
                    Error = $"Command '{commandName}' not allowed in {context}",
                    RawText = match.Value,
                    Position = match.Index
                });
                continue;
            }

            var parsed = ParseArguments(argumentsText, registration.Schema);
            parsed.Name = commandName;
            parsed.Registration = registration;
            parsed.RawText = match.Value;
            parsed.Position = match.Index;

            results.Add(parsed);
        }

        return new ParseResult { Commands = results };
    }

    private ParsedCommand ParseArguments(string text, CommandSchema schema)
    {
        var command = new ParsedCommand { IsValid = true };

        if (string.IsNullOrWhiteSpace(text))
            return ValidateCommand(command, schema);

        var tokens = TokenizeArguments(text);
        var parameterIndex = 0;

        for (int i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];

            if (token.StartsWith("--"))
            {
                // Long option
                var optionName = token.Substring(2);
                var option = schema.Options.FirstOrDefault(o =>
                    o.LongName.Equals(optionName, StringComparison.OrdinalIgnoreCase));

                if (option != null)
                {
                    if (option.Type == typeof(bool))
                    {
                        command.Options[option.LongName] = true;
                    }
                    else if (i + 1 < tokens.Count && !tokens[i + 1].StartsWith("-"))
                    {
                        i++;
                        command.Options[option.LongName] = ConvertValue(tokens[i], option.Type);
                    }
                    else
                    {
                        command.IsValid = false;
                        command.Error = $"Option --{optionName} requires a value";
                    }
                }
            }
            else if (token.StartsWith("-") && token.Length == 2)
            {
                // Short option
                var optionChar = token[1].ToString();
                var option = schema.Options.FirstOrDefault(o =>
                    o.ShortName.Equals(optionChar, StringComparison.OrdinalIgnoreCase));

                if (option != null)
                {
                    if (option.Type == typeof(bool))
                    {
                        command.Options[option.LongName] = true;
                    }
                    else if (i + 1 < tokens.Count && !tokens[i + 1].StartsWith("-"))
                    {
                        i++;
                        command.Options[option.LongName] = ConvertValue(tokens[i], option.Type);
                    }
                }
            }
            else
            {
                // Positional parameter
                if (parameterIndex < schema.Parameters.Count)
                {
                    var param = schema.Parameters[parameterIndex];
                    command.Arguments[param.Name] = ConvertValue(token, param.Type);

                    // Validate parameter
                    if (param.Validator != null && !param.Validator(command.Arguments[param.Name]))
                    {
                        command.IsValid = false;
                        command.Error = $"Invalid value for parameter '{param.Name}': {token}";
                    }

                    parameterIndex++;
                }
            }
        }

        return ValidateCommand(command, schema);
    }

    private List<string> TokenizeArguments(string text)
    {
        var tokens = new List<string>();
        var current = "";
        var inQuotes = false;

        for (int i = 0; i < text.Length; i++)
        {
            var c = text[i];

            if (c == '"' && (i == 0 || text[i - 1] != '\\'))
            {
                inQuotes = !inQuotes;
            }
            else if (c == ' ' && !inQuotes)
            {
                if (!string.IsNullOrEmpty(current))
                {
                    tokens.Add(current);
                    current = "";
                }
            }
            else
            {
                current += c;
            }
        }

        if (!string.IsNullOrEmpty(current))
            tokens.Add(current);

        return tokens;
    }

    private object ConvertValue(string value, Type targetType)
    {
        if (targetType == typeof(string))
            return value;

        if (targetType == typeof(int))
            return int.TryParse(value, out var intVal) ? intVal : 0;

        if (targetType == typeof(bool))
            return bool.TryParse(value, out var boolVal) ? boolVal : value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);

        if (targetType == typeof(DateTime))
            return DateTime.TryParse(value, out var dateVal) ? dateVal : DateTime.Now;

        if (targetType == typeof(TimeSpan))
            return ParseDuration(value);

        return value;
    }

    private TimeSpan ParseDuration(string value)
    {
        var pattern = @"(?:(\d+)h)?(?:(\d+)m)?(?:(\d+)s)?";
        var match = Regex.Match(value, pattern, RegexOptions.IgnoreCase);

        if (match.Success)
        {
            var hours = match.Groups[1].Success ? int.Parse(match.Groups[1].Value) : 0;
            var minutes = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 0;
            var seconds = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;

            return new TimeSpan(hours, minutes, seconds);
        }

        return TimeSpan.Zero;
    }

    private ParsedCommand ValidateCommand(ParsedCommand command, CommandSchema schema)
    {
        // Check required parameters
        foreach (var param in schema.Parameters.Where(p => p.Required))
        {
            if (!command.Arguments.ContainsKey(param.Name))
            {
                command.IsValid = false;
                command.Error = $"Required parameter '{param.Name}' is missing";
                return command;
            }
        }

        // Check required options
        foreach (var option in schema.Options.Where(o => o.Required))
        {
            if (!command.Options.ContainsKey(option.LongName))
            {
                command.IsValid = false;
                command.Error = $"Required option '--{option.LongName}' is missing";
                return command;
            }
        }

        // Apply defaults
        foreach (var param in schema.Parameters.Where(p => p.DefaultValue != null))
        {
            if (!command.Arguments.ContainsKey(param.Name))
            {
                command.Arguments[param.Name] = param.DefaultValue;
            }
        }

        foreach (var option in schema.Options.Where(o => o.DefaultValue != null))
        {
            if (!command.Options.ContainsKey(option.LongName))
            {
                command.Options[option.LongName] = option.DefaultValue;
            }
        }

        return command;
    }
}