using NotNow.Core.Commands.Framework;

namespace NotNow.Core.Console;

public class CommandInputWithHints
{
    private readonly ICommandAutoCompleter _autoCompleter;
    private readonly CommandContext _context;

    public CommandInputWithHints(ICommandAutoCompleter autoCompleter, CommandContext context)
    {
        _autoCompleter = autoCompleter;
        _context = context;
    }

    public string ReadCommand(string prompt = "/notnow ")
    {
        System.Console.Write(prompt);
        var input = "";
        var currentLine = System.Console.CursorTop;

        while (true)
        {
            var key = System.Console.ReadKey(true);

            if (key.Key == ConsoleKey.Enter)
            {
                ClearSuggestions(currentLine);
                System.Console.WriteLine();
                return input;
            }
            else if (key.Key == ConsoleKey.Tab && !key.Modifiers.HasFlag(ConsoleModifiers.Shift))
            {
                var suggestions = _autoCompleter.GetSuggestions(input, _context);
                if (suggestions.Count > 0)
                {
                    ShowSuggestions(suggestions, currentLine);

                    // Auto-complete first suggestion if there's only one
                    if (suggestions.Count == 1)
                    {
                        // Clear current input
                        for (int i = 0; i < input.Length; i++)
                            System.Console.Write("\b \b");

                        // Determine what to complete
                        var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        string newInput;

                        if (parts.Length == 0)
                        {
                            newInput = suggestions[0] + " ";
                        }
                        else if (input.EndsWith(" "))
                        {
                            newInput = input + suggestions[0];
                        }
                        else
                        {
                            var prefix = string.Join(" ", parts.Take(parts.Length - 1));
                            if (parts.Length > 1)
                                prefix += " ";
                            newInput = prefix + suggestions[0];
                            if (parts.Length == 1) // Just completed a command
                                newInput += " ";
                        }

                        input = newInput;
                        System.Console.Write(input);
                    }
                }
            }
            else if (key.Key == ConsoleKey.Backspace)
            {
                if (input.Length > 0)
                {
                    input = input.Substring(0, input.Length - 1);
                    System.Console.Write("\b \b");
                    ClearSuggestions(currentLine);
                }
            }
            else if (key.Key == ConsoleKey.Escape)
            {
                ClearSuggestions(currentLine);
                for (int i = 0; i < input.Length; i++)
                    System.Console.Write("\b \b");
                input = "";
            }
            else if (!char.IsControl(key.KeyChar))
            {
                input += key.KeyChar;
                System.Console.Write(key.KeyChar);
                ClearSuggestions(currentLine);
            }
        }
    }

    private void ShowSuggestions(List<string> suggestions, int inputLine)
    {
        var currentPos = System.Console.GetCursorPosition();
        System.Console.SetCursorPosition(0, inputLine + 1);

        // Clear previous suggestions
        for (int i = 0; i < 5; i++)
        {
            System.Console.Write(new string(' ', System.Console.WindowWidth - 1));
            if (i < 4) System.Console.WriteLine();
        }

        System.Console.SetCursorPosition(0, inputLine + 1);

        // Show suggestions
        System.Console.ForegroundColor = ConsoleColor.DarkGray;
        System.Console.WriteLine("Suggestions (press Tab to complete):");

        for (int i = 0; i < Math.Min(suggestions.Count, 4); i++)
        {
            System.Console.ForegroundColor = ConsoleColor.Yellow;
            System.Console.Write($"  → ");
            System.Console.ForegroundColor = ConsoleColor.Gray;
            System.Console.WriteLine(suggestions[i]);
        }

        if (suggestions.Count > 4)
        {
            System.Console.ForegroundColor = ConsoleColor.DarkGray;
            System.Console.WriteLine($"  ... and {suggestions.Count - 4} more");
        }

        System.Console.ResetColor();
        System.Console.SetCursorPosition(currentPos.Left, currentPos.Top);
    }

    private void ClearSuggestions(int inputLine)
    {
        var currentPos = System.Console.GetCursorPosition();
        System.Console.SetCursorPosition(0, inputLine + 1);

        // Clear up to 6 lines
        for (int i = 0; i < 6; i++)
        {
            System.Console.Write(new string(' ', System.Console.WindowWidth - 1));
            if (i < 5) System.Console.WriteLine();
        }

        System.Console.SetCursorPosition(currentPos.Left, currentPos.Top);
    }
}