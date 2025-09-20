namespace NotNow.Core.Commands.Framework;

public class CommandSchema
{
    public List<CommandParameter> Parameters { get; set; } = new();
    public List<CommandOption> Options { get; set; } = new();
    public ValidationRules Validation { get; set; } = new();
}

public class CommandParameter
{
    public string Name { get; set; } = string.Empty;
    public Type Type { get; set; } = typeof(string);
    public bool Required { get; set; }
    public object? DefaultValue { get; set; }
    public string Description { get; set; } = string.Empty;
    public Func<object, bool>? Validator { get; set; }
}

public class CommandOption
{
    public string LongName { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public Type Type { get; set; } = typeof(string);
    public bool Required { get; set; }
    public object? DefaultValue { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class ValidationRules
{
    public bool RequireIssueContext { get; set; }
    public bool RequireAuthentication { get; set; }
    public string[]? RequiredPermissions { get; set; }
}