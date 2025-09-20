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

        // Ensure we're not too close to bottom of window
        if (currentLine > System.Console.WindowHeight - 5)
        {
            // Scroll up by clearing screen and repositioning
            System.Console.Clear();
            System.Console.Write(prompt);
            currentLine = System.Console.CursorTop;
        }

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
                    // Auto-complete first suggestion if there's only one
                    if (suggestions.Count == 1)
                    {
                        // Clear any previous suggestions first
                        ClearSuggestions(currentLine);

                        // Move cursor back to start of input (after prompt)
                        System.Console.SetCursorPosition(prompt.Length, currentLine);

                        // Clear the current input from screen
                        System.Console.Write(new string(' ', input.Length));

                        // Move cursor back to start of input again
                        System.Console.SetCursorPosition(prompt.Length, currentLine);

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
                    else
                    {
                        // Multiple suggestions - show them
                        ShowSuggestions(suggestions, currentLine);
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
                // Clear suggestions first to avoid cursor issues
                ClearSuggestions(currentLine);

                // Now add the character and write it
                input += key.KeyChar;
                System.Console.Write(key.KeyChar);
            }
        }
    }

    private void ShowSuggestions(List<string> suggestions, int inputLine)
    {
        // Save the exact cursor position before we do anything
        var originalCursorLeft = System.Console.CursorLeft;
        var originalCursorTop = inputLine;  // Force it to be the input line!

        // Check if we have enough room to show suggestions
        var availableLines = System.Console.WindowHeight - inputLine - 2;
        if (availableLines < 2)
        {
            // Not enough room, don't show suggestions
            return;
        }

        // Move to suggestion area (line below input)
        System.Console.SetCursorPosition(0, inputLine + 1);

        // Clear previous suggestions - need more lines now that we show all
        var linesToClear = Math.Min(suggestions.Count + 2, availableLines);
        for (int i = 0; i < linesToClear; i++)
        {
            System.Console.Write(new string(' ', System.Console.WindowWidth - 1));
            if (i < linesToClear - 1) System.Console.WriteLine();
        }

        // Move back to start of suggestion area
        System.Console.SetCursorPosition(0, inputLine + 1);

        // Show suggestions
        System.Console.ForegroundColor = ConsoleColor.DarkGray;
        System.Console.WriteLine("Suggestions (press Tab to complete):");

        // Show only as many suggestions as we have room for
        var maxSuggestions = Math.Min(suggestions.Count, availableLines - 1);
        for (int i = 0; i < maxSuggestions; i++)
        {
            System.Console.ForegroundColor = ConsoleColor.Yellow;
            System.Console.Write($"  → ");
            System.Console.ForegroundColor = ConsoleColor.Gray;
            System.Console.WriteLine(suggestions[i]);
        }

        if (suggestions.Count > maxSuggestions)
        {
            System.Console.ForegroundColor = ConsoleColor.DarkGray;
            System.Console.Write($"  ... and {suggestions.Count - maxSuggestions} more");
        }

        System.Console.ResetColor();

        // Restore cursor to exactly where it was on the input line
        System.Console.SetCursorPosition(originalCursorLeft, originalCursorTop);
    }

    private void ClearSuggestions(int inputLine)
    {
        // Save current cursor position - force it to input line
        var savedLeft = System.Console.CursorLeft;
        var savedTop = inputLine;  // Always use the input line!

        // Check bounds
        if (inputLine + 1 >= System.Console.WindowHeight)
        {
            // No room for suggestions anyway
            return;
        }

        // Move to suggestion area
        System.Console.SetCursorPosition(0, inputLine + 1);

        // Clear all potential suggestion lines (up to bottom of window)
        var linesToClear = Math.Max(0, System.Console.WindowHeight - inputLine - 1);
        for (int i = 0; i < linesToClear; i++)
        {
            System.Console.Write(new string(' ', System.Console.WindowWidth - 1));
            if (i < linesToClear - 1) System.Console.WriteLine();
        }

        // Restore cursor to exactly where it was on the input line
        System.Console.SetCursorPosition(savedLeft, savedTop);
    }
}