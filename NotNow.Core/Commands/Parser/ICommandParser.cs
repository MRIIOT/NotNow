using NotNow.Core.Commands.Framework;

namespace NotNow.Core.Commands.Parser;

public interface ICommandParser
{
    ParseResult Parse(string text, CommandContext context);
    bool ContainsCommand(string text);
}

public class ParseResult
{
    public List<ParsedCommand> Commands { get; set; } = new();
    public bool HasErrors => Commands.Any(c => !c.IsValid);
}

public class ParsedCommand
{
    public string Name { get; set; } = string.Empty;
    public CommandRegistration? Registration { get; set; }
    public Dictionary<string, object> Arguments { get; set; } = new();
    public Dictionary<string, object> Options { get; set; } = new();
    public bool IsValid { get; set; }
    public string? Error { get; set; }
    public string RawText { get; set; } = string.Empty;
    public int Position { get; set; }
}