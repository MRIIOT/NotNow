using System.Text;
using System.Text.Json;
using NotNow.Core.Commands.Execution;
using NotNow.GitHubService.Interfaces;

namespace NotNow.Core.Services;

public class CommandPostingService : ICommandPostingService
{
    private readonly IGitHubService _githubService;
    private readonly JsonSerializerOptions _jsonOptions;

    public CommandPostingService(IGitHubService githubService)
    {
        _githubService = githubService;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task<bool> PostCommandToGitHubAsync(int issueNumber, string commandText, ExecutionResult result)
    {
        try
        {
            var comment = FormatCommandComment(commandText, result);
            await _githubService.AddCommentToIssueAsync(issueNumber, comment);
            return true;
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"Failed to post command to GitHub: {ex.Message}");
            return false;
        }
    }

    private string FormatCommandComment(string commandText, ExecutionResult result)
    {
        var sb = new StringBuilder();

        // Add the command
        sb.AppendLine(commandText);

        // Add hidden metadata section
        sb.AppendLine();
        sb.AppendLine("<!-- notnow-metadata");
        sb.AppendLine($"executed: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"success: {result.Success}");

        // Add command results as JSON
        foreach (var commandResult in result.Results)
        {
            sb.AppendLine("result:");

            if (commandResult.Success)
            {
                sb.AppendLine($"  status: success");
                sb.AppendLine($"  message: {commandResult.Message}");

                if (commandResult.Data != null)
                {
                    var json = JsonSerializer.Serialize(commandResult.Data, _jsonOptions);
                    sb.AppendLine($"  data: {json}");
                }
            }
            else
            {
                sb.AppendLine($"  status: failed");
                sb.AppendLine($"  error: {commandResult.Error}");
            }
        }

        sb.AppendLine("-->");

        // Add visible feedback if there were errors
        if (!result.Success)
        {
            sb.AppendLine();
            sb.AppendLine("> ⚠️ **Command execution had errors:**");
            foreach (var error in result.Results.Where(r => !r.Success))
            {
                sb.AppendLine($"> - {error.Error}");
            }
        }
        else if (result.Results.Any())
        {
            // Add visible success feedback
            sb.AppendLine();
            sb.AppendLine("> ✅ **Command executed successfully**");

            // Show important visible changes
            foreach (var successResult in result.Results.Where(r => r.Success))
            {
                // Extract and display key information based on command type
                var displayMessage = ExtractDisplayMessage(successResult);
                if (!string.IsNullOrEmpty(displayMessage))
                {
                    sb.AppendLine($"> - {displayMessage}");
                }
            }
        }

        return sb.ToString();
    }

    private string ExtractDisplayMessage(Commands.Framework.CommandResult result)
    {
        // Extract key information for display
        if (result.Data == null)
            return result.Message;

        try
        {
            var json = JsonSerializer.Serialize(result.Data, _jsonOptions);
            var data = JsonSerializer.Deserialize<JsonElement>(json);

            // Handle different command types
            if (result.Message.StartsWith("Status changed"))
            {
                if (data.TryGetProperty("newStatus", out var status))
                {
                    return $"Status changed to **{status.GetString()}**";
                }
            }
            else if (result.Message.StartsWith("Issue assigned"))
            {
                if (data.TryGetProperty("newAssignee", out var assignee))
                {
                    return $"Assigned to **{assignee.GetString()}**";
                }
            }
            else if (result.Message.StartsWith("Logged"))
            {
                if (data.TryGetProperty("totalHours", out var hours))
                {
                    var h = hours.GetDouble();
                    var timeStr = h >= 1 ? $"{h:F1}h" : $"{(int)(h * 60)}m";
                    return $"Logged **{timeStr}** of work";
                }
            }
            else if (result.Message.StartsWith("Priority"))
            {
                if (data.TryGetProperty("priority", out var priority))
                {
                    return $"Priority set to **{priority.GetString()}**";
                }
            }
            else if (result.Message.StartsWith("Due date"))
            {
                if (data.TryGetProperty("dueDate", out var due))
                {
                    var date = due.GetDateTime();
                    return $"Due date set to **{date:yyyy-MM-dd}**";
                }
            }
            else if (result.Message.StartsWith("Work session started"))
            {
                return "⏱️ Started work session";
            }
            else if (result.Message.StartsWith("Work session stopped"))
            {
                if (data.TryGetProperty("totalHours", out var hours))
                {
                    var h = hours.GetDouble();
                    var timeStr = h >= 1 ? $"{h:F1}h" : $"{(int)(h * 60)}m";
                    return $"⏹️ Stopped work session (duration: **{timeStr}**)";
                }
            }
            else if (result.Message.StartsWith("Added subtask"))
            {
                if (data.TryGetProperty("title", out var title) &&
                    data.TryGetProperty("id", out var id))
                {
                    return $"Added subtask: **{title.GetString()}** (`{id.GetString()}`)";
                }
            }
            else if (result.Message.StartsWith("Completed subtask"))
            {
                if (data.TryGetProperty("title", out var title))
                {
                    return $"✅ Completed subtask: **{title.GetString()}**";
                }
            }
        }
        catch
        {
            // If parsing fails, just return the message
        }

        return result.Message;
    }
}