using NotNow.Core.Commands.Framework;
using System.Text;

namespace NotNow.Core.Console;

public class SimpleCommandInput
{
    private readonly ICommandAutoCompleter _autoCompleter;
    private readonly CommandContext _context;

    public SimpleCommandInput(ICommandAutoCompleter autoCompleter, CommandContext context)
    {
        _autoCompleter = autoCompleter;
        _context = context;
    }

    public string ReadCommand(string prompt = "/notnow ")
    {
        System.Console.Write(prompt);

        var input = new StringBuilder();
        var cursorPos = 0;
        var suggestions = new List<string>();
        var selectedSuggestion = -1;

        while (true)
        {
            var key = System.Console.ReadKey(true);

            if (key.Key == ConsoleKey.Enter)
            {
                System.Console.WriteLine();
                return input.ToString();
            }
            else if (key.Key == ConsoleKey.Tab)
            {
                var currentInput = input.ToString();
                suggestions = _autoCompleter.GetSuggestions(currentInput, _context);

                if (suggestions.Count > 0)
                {
                    // Cycle through suggestions
                    selectedSuggestion = (selectedSuggestion + 1) % suggestions.Count;

                    // Clear current input and replace with suggestion
                    while (cursorPos > 0)
                    {
                        System.Console.Write("\b \b");
                        cursorPos--;
                    }
                    input.Clear();

                    // Handle partial matches
                    var parts = currentInput.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 0 && !currentInput.EndsWith(" "))
                    {
                        // Keep everything except the last part
                        if (parts.Length > 1)
                        {
                            var prefix = string.Join(" ", parts.Take(parts.Length - 1)) + " ";
                            input.Append(prefix);
                            System.Console.Write(prefix);
                            cursorPos = prefix.Length;
                        }
                    }
                    else if (currentInput.EndsWith(" "))
                    {
                        input.Append(currentInput);
                        System.Console.Write(currentInput);
                        cursorPos = currentInput.Length;
                    }

                    // Add the suggestion
                    var suggestion = suggestions[selectedSuggestion];
                    input.Append(suggestion);
                    System.Console.Write(suggestion);
                    cursorPos = input.Length;

                    // Add space after command completion
                    if (parts.Length == 0 || (parts.Length == 1 && !currentInput.EndsWith(" ")))
                    {
                        input.Append(" ");
                        System.Console.Write(" ");
                        cursorPos++;
                    }
                }
            }
            else if (key.Key == ConsoleKey.Backspace)
            {
                if (cursorPos > 0)
                {
                    input.Remove(input.Length - 1, 1);
                    cursorPos--;
                    System.Console.Write("\b \b");
                    selectedSuggestion = -1;
                }
            }
            else if (key.Key == ConsoleKey.Escape)
            {
                // Clear line
                while (cursorPos > 0)
                {
                    System.Console.Write("\b \b");
                    cursorPos--;
                }
                input.Clear();
                selectedSuggestion = -1;
            }
            else if (!char.IsControl(key.KeyChar))
            {
                input.Append(key.KeyChar);
                System.Console.Write(key.KeyChar);
                cursorPos++;
                selectedSuggestion = -1;
            }
        }
    }
}