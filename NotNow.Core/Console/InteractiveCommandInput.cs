using NotNow.Core.Commands.Framework;
using System.Text;

namespace NotNow.Core.Console;

public class InteractiveCommandInput
{
    private readonly ICommandAutoCompleter _autoCompleter;
    private readonly CommandContext _context;
    private StringBuilder _currentInput = new();
    private List<string> _suggestions = new();
    private int _selectedSuggestion = -1;
    private List<string> _commandHistory = new();
    private int _historyIndex = -1;

    public InteractiveCommandInput(ICommandAutoCompleter autoCompleter, CommandContext context)
    {
        _autoCompleter = autoCompleter;
        _context = context;
    }

    public string ReadCommand(string prompt = "/notnow ")
    {
        System.Console.Write(prompt);
        _currentInput.Clear();
        _currentInput.Append(prompt);

        int cursorPosition = prompt.Length;
        _suggestions.Clear();
        _selectedSuggestion = -1;

        while (true)
        {
            var key = System.Console.ReadKey(true);

            switch (key.Key)
            {
                case ConsoleKey.Enter:
                    System.Console.WriteLine();
                    var command = _currentInput.ToString();
                    if (!string.IsNullOrWhiteSpace(command) && command != prompt)
                    {
                        _commandHistory.Add(command);
                        _historyIndex = _commandHistory.Count;
                    }
                    return command.Substring(prompt.Length);

                case ConsoleKey.Tab:
                    HandleTabCompletion(prompt, ref cursorPosition, key);
                    break;

                case ConsoleKey.Backspace:
                    if (cursorPosition > prompt.Length)
                    {
                        _currentInput.Remove(cursorPosition - 1, 1);
                        cursorPosition--;
                        RedrawLine(prompt, cursorPosition);
                        UpdateSuggestions(prompt);
                    }
                    break;

                case ConsoleKey.Delete:
                    if (cursorPosition < _currentInput.Length)
                    {
                        _currentInput.Remove(cursorPosition, 1);
                        RedrawLine(prompt, cursorPosition);
                        UpdateSuggestions(prompt);
                    }
                    break;

                case ConsoleKey.LeftArrow:
                    if (cursorPosition > prompt.Length)
                    {
                        cursorPosition--;
                        System.Console.SetCursorPosition(cursorPosition, System.Console.CursorTop);
                    }
                    break;

                case ConsoleKey.RightArrow:
                    if (cursorPosition < _currentInput.Length)
                    {
                        cursorPosition++;
                        System.Console.SetCursorPosition(cursorPosition, System.Console.CursorTop);
                    }
                    break;

                case ConsoleKey.UpArrow:
                    NavigateHistory(-1, prompt, ref cursorPosition);
                    break;

                case ConsoleKey.DownArrow:
                    NavigateHistory(1, prompt, ref cursorPosition);
                    break;

                case ConsoleKey.Escape:
                    ClearSuggestions();
                    break;

                default:
                    if (!char.IsControl(key.KeyChar))
                    {
                        _currentInput.Insert(cursorPosition, key.KeyChar);
                        cursorPosition++;
                        RedrawLine(prompt, cursorPosition);
                        UpdateSuggestions(prompt);
                    }
                    break;
            }
        }
    }

    private void HandleTabCompletion(string prompt, ref int cursorPosition, ConsoleKeyInfo key)
    {
        if (_suggestions.Count == 0)
        {
            UpdateSuggestions(prompt);
        }

        if (_suggestions.Count == 0)
            return;

        if (key.Modifiers == ConsoleModifiers.Shift)
        {
            // Shift+Tab - go backwards
            _selectedSuggestion--;
            if (_selectedSuggestion < 0)
                _selectedSuggestion = _suggestions.Count - 1;
        }
        else
        {
            // Tab - go forwards
            _selectedSuggestion++;
            if (_selectedSuggestion >= _suggestions.Count)
                _selectedSuggestion = 0;
        }

        // Apply the selected suggestion
        var input = _currentInput.ToString().Substring(prompt.Length);
        var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var suggestion = _suggestions[_selectedSuggestion];

        if (parts.Length <= 1)
        {
            // Replace entire input with suggestion
            _currentInput.Clear();
            _currentInput.Append(prompt);
            _currentInput.Append(suggestion);
        }
        else
        {
            // Replace last part with suggestion
            var lastSpaceIndex = input.LastIndexOf(' ');
            _currentInput.Length = prompt.Length + lastSpaceIndex + 1;
            _currentInput.Append(suggestion);
        }

        cursorPosition = _currentInput.Length;
        RedrawLine(prompt, cursorPosition);
        ShowSuggestions();
    }

    private void UpdateSuggestions(string prompt)
    {
        var input = _currentInput.ToString().Substring(prompt.Length);
        _suggestions = _autoCompleter.GetSuggestions(input, _context);
        _selectedSuggestion = -1;

        if (_suggestions.Count > 0)
        {
            ShowSuggestions();
        }
        else
        {
            ClearSuggestions();
        }
    }

    private void ShowSuggestions()
    {
        if (_suggestions.Count == 0)
            return;

        var currentLine = System.Console.CursorTop;
        System.Console.SetCursorPosition(0, currentLine + 1);

        // Clear any previous suggestions
        for (int i = 0; i < Math.Min(5, _suggestions.Count); i++)
        {
            System.Console.WriteLine(new string(' ', System.Console.WindowWidth - 1));
        }

        System.Console.SetCursorPosition(0, currentLine + 1);

        // Show suggestions (max 5)
        for (int i = 0; i < Math.Min(5, _suggestions.Count); i++)
        {
            if (i == _selectedSuggestion)
            {
                System.Console.ForegroundColor = ConsoleColor.Black;
                System.Console.BackgroundColor = ConsoleColor.Gray;
            }
            else
            {
                System.Console.ForegroundColor = ConsoleColor.DarkGray;
            }

            System.Console.WriteLine($"  {_suggestions[i]}");
            System.Console.ResetColor();
        }

        if (_suggestions.Count > 5)
        {
            System.Console.ForegroundColor = ConsoleColor.DarkGray;
            System.Console.WriteLine($"  ... and {_suggestions.Count - 5} more");
            System.Console.ResetColor();
        }

        // Return cursor to input line
        System.Console.SetCursorPosition(_currentInput.Length, currentLine);
    }

    private void ClearSuggestions()
    {
        var currentLine = System.Console.CursorTop;
        var linesToClear = Math.Min(6, _suggestions.Count + 1);

        for (int i = 1; i <= linesToClear; i++)
        {
            System.Console.SetCursorPosition(0, currentLine + i);
            System.Console.Write(new string(' ', System.Console.WindowWidth - 1));
        }

        System.Console.SetCursorPosition(_currentInput.Length, currentLine);
        _suggestions.Clear();
        _selectedSuggestion = -1;
    }

    private void NavigateHistory(int direction, string prompt, ref int cursorPosition)
    {
        if (_commandHistory.Count == 0)
            return;

        _historyIndex += direction;

        if (_historyIndex < 0)
            _historyIndex = 0;
        else if (_historyIndex >= _commandHistory.Count)
            _historyIndex = _commandHistory.Count - 1;

        _currentInput.Clear();
        _currentInput.Append(_commandHistory[_historyIndex]);
        cursorPosition = _currentInput.Length;
        RedrawLine(prompt, cursorPosition);
    }

    private void RedrawLine(string prompt, int cursorPosition)
    {
        System.Console.SetCursorPosition(0, System.Console.CursorTop);
        System.Console.Write(new string(' ', System.Console.WindowWidth - 1));
        System.Console.SetCursorPosition(0, System.Console.CursorTop);
        System.Console.Write(_currentInput.ToString());
        System.Console.SetCursorPosition(cursorPosition, System.Console.CursorTop);
    }
}