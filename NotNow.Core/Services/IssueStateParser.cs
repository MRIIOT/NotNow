using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using NotNow.Core.Models;
using Octokit;

namespace NotNow.Core.Services;

public interface IIssueStateParser
{
    IssueState ParseIssueState(Issue issue, IReadOnlyList<IssueComment> comments);
}

public class IssueStateParser : IIssueStateParser
{
    // Match /notnow commands - stop at newline or next /notnow
    private static readonly Regex CommandRegex = new(@"/notnow\s+(\w+)(?:\s+([^\r\n]+?))?(?=\r?\n|/notnow|$)", RegexOptions.Multiline | RegexOptions.IgnoreCase);
    private static readonly Regex TimeSpanRegex = new(@"(\d+)([hHmM])");

    public IssueState ParseIssueState(Issue issue, IReadOnlyList<IssueComment> comments)
    {
        var state = new IssueState
        {
            IssueNumber = issue.Number,
            Title = issue.Title
        };

        // Parse commands from issue body first
        if (!string.IsNullOrWhiteSpace(issue.Body))
        {
            ParseCommands(issue.Body, state, issue.CreatedAt.UtcDateTime);
        }

        // Then parse commands from comments in chronological order
        foreach (var comment in comments.OrderBy(c => c.CreatedAt))
        {
            ParseCommands(comment.Body, state, comment.CreatedAt.UtcDateTime);
        }

        // Calculate total time spent from all sessions
        state.TotalTimeSpent = TimeSpan.FromSeconds(
            state.Sessions.Sum(s => s.Duration.TotalSeconds));

        return state;
    }

    private void ParseCommands(string text, IssueState state, DateTime timestamp)
    {
        var matches = CommandRegex.Matches(text);

        foreach (Match match in matches)
        {
            var command = match.Groups[1].Value.ToLower();
            var args = match.Groups[2].Value.Trim();

            state.LastUpdated = timestamp;

            switch (command)
            {
                case "init":
                    state.IsInitialized = true;
                    break;

                case "status":
                    state.Status = args.ToLower();
                    break;

                case "priority":
                    state.Priority = args.ToLower();
                    break;

                case "type":
                    state.Type = args.ToLower();
                    break;

                case "assign":
                case "assignee":
                    state.Assignee = args.TrimStart('@');
                    break;

                case "estimate":
                    state.Estimate = ParseTimeSpan(args)?.ToString(@"hh\:mm") ?? args;
                    break;

                case "due":
                    if (DateTime.TryParse(args, out var dueDate))
                    {
                        state.DueDate = dueDate;
                    }
                    break;

                case "tags":
                case "tag":
                    var tags = args.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(t => t.Trim())
                        .Where(t => !string.IsNullOrEmpty(t));
                    state.Tags.AddRange(tags);
                    state.Tags = state.Tags.Distinct().ToList();
                    break;

                case "subtask":
                    ParseSubtaskCommand(args, state);
                    break;

                case "start":
                    ParseStartCommand(args, state, timestamp);
                    break;

                case "stop":
                    ParseStopCommand(args, state, timestamp);
                    break;

                case "time":
                    ParseTimeCommand(args, state, timestamp);
                    break;

                case "complete":
                    state.Status = "done";
                    // Mark all subtasks as complete
                    foreach (var subtask in state.Subtasks)
                    {
                        subtask.Status = "done";
                        subtask.CompletedAt = timestamp;
                    }
                    break;

                case "reopen":
                    state.Status = "todo";
                    break;
            }
        }
    }

    private void ParseSubtaskCommand(string args, IssueState state)
    {
        // Parse subtask command formats:
        // subtask add "title" --id st1 --estimate 2h
        // subtask "title" --id st1
        // subtask complete st1
        // subtask remove st1

        if (args.StartsWith("add ", StringComparison.OrdinalIgnoreCase))
        {
            args = args.Substring(4).Trim();
        }

        if (args.StartsWith("complete ", StringComparison.OrdinalIgnoreCase))
        {
            var id = args.Substring(9).Trim();
            var subtask = state.Subtasks.FirstOrDefault(s => s.Id == id);
            if (subtask != null)
            {
                subtask.Status = "done";
                subtask.CompletedAt = DateTime.UtcNow;
            }
            return;
        }

        if (args.StartsWith("remove ", StringComparison.OrdinalIgnoreCase))
        {
            var id = args.Substring(7).Trim();
            state.Subtasks.RemoveAll(s => s.Id == id);
            return;
        }

        // Handle list command
        if (args.Equals("list", StringComparison.OrdinalIgnoreCase))
        {
            // List command doesn't modify state, just return
            return;
        }

        // Parse add format - handle both quoted and unquoted titles
        string title = null;
        string remainingArgs = args;

        // First try to match quoted title
        var titleMatch = Regex.Match(args, @"""([^""]+)""");
        if (titleMatch.Success)
        {
            title = titleMatch.Groups[1].Value;
            remainingArgs = args.Substring(titleMatch.Index + titleMatch.Length).Trim();
        }
        else
        {
            // If no quotes, take everything up to the first option or end of string
            var match = Regex.Match(args, @"^([^-]+)(?:\s+--|\s*$)");
            if (match.Success)
            {
                title = match.Groups[1].Value.Trim();
                remainingArgs = args.Substring(match.Groups[1].Length).Trim();
            }
            else if (!args.StartsWith("--") && !string.IsNullOrWhiteSpace(args))
            {
                // Just a title with no options
                title = args.Trim();
                remainingArgs = "";
            }
        }

        if (!string.IsNullOrEmpty(title))
        {
            var subtask = new Subtask
            {
                Title = title
            };

            // Extract ID
            var idMatch = Regex.Match(remainingArgs, @"--id\s+(\S+)");
            if (idMatch.Success)
            {
                subtask.Id = idMatch.Groups[1].Value;
            }
            else
            {
                subtask.Id = $"st{state.Subtasks.Count + 1}";
            }

            // Extract estimate
            var estimateMatch = Regex.Match(remainingArgs, @"--estimate\s+(\S+)");
            if (estimateMatch.Success)
            {
                subtask.Estimate = estimateMatch.Groups[1].Value;
            }

            // Add or update subtask
            var existing = state.Subtasks.FirstOrDefault(s => s.Id == subtask.Id);
            if (existing != null)
            {
                state.Subtasks.Remove(existing);
            }
            state.Subtasks.Add(subtask);
        }
    }

    private void ParseStartCommand(string args, IssueState state, DateTime timestamp)
    {
        // End any active session first
        if (state.ActiveSession != null)
        {
            state.ActiveSession.EndedAt = timestamp;
            state.ActiveSession.Duration = timestamp - state.ActiveSession.StartedAt;
            state.ActiveSession = null;
        }

        // Extract session data if it's in JSON format (from command execution)
        if (args.Contains("\"SessionId\""))
        {
            try
            {
                // Parse the Data: JSON output
                var dataStart = args.IndexOf('{');
                if (dataStart >= 0)
                {
                    var jsonData = args.Substring(dataStart);
                    var sessionData = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonData);

                    var session = new WorkSession
                    {
                        Id = sessionData.ContainsKey("SessionId") ? sessionData["SessionId"].ToString() : Guid.NewGuid().ToString(),
                        StartedAt = timestamp,
                        Description = sessionData.ContainsKey("Description") ? sessionData["Description"]?.ToString() : null,
                        User = sessionData.ContainsKey("User") ? sessionData["User"]?.ToString() : null
                    };

                    state.Sessions.Add(session);
                    state.ActiveSession = session;
                }
            }
            catch
            {
                // Fallback to simple session
                CreateSimpleSession(state, timestamp, args);
            }
        }
        else
        {
            CreateSimpleSession(state, timestamp, args);
        }
    }

    private void CreateSimpleSession(IssueState state, DateTime timestamp, string args)
    {
        var session = new WorkSession
        {
            Id = Guid.NewGuid().ToString(),
            StartedAt = timestamp
        };

        // Check for description flag
        var descMatch = Regex.Match(args, @"-d\s+""([^""]+)""");
        if (!descMatch.Success)
        {
            descMatch = Regex.Match(args, @"--description\s+""([^""]+)""");
        }

        if (descMatch.Success)
        {
            session.Description = descMatch.Groups[1].Value;
        }

        state.Sessions.Add(session);
        state.ActiveSession = session;
    }

    private void ParseStopCommand(string args, IssueState state, DateTime timestamp)
    {
        if (state.ActiveSession != null)
        {
            state.ActiveSession.EndedAt = timestamp;
            state.ActiveSession.Duration = timestamp - state.ActiveSession.StartedAt;

            // Parse duration from command output if available
            if (args.Contains("Duration"))
            {
                var durationMatch = Regex.Match(args, @"""Duration""\s*:\s*""([^""]+)""");
                if (durationMatch.Success)
                {
                    var durationStr = durationMatch.Groups[1].Value;
                    var parsedDuration = ParseTimeSpan(durationStr);
                    if (parsedDuration.HasValue)
                    {
                        state.ActiveSession.Duration = parsedDuration.Value;
                    }
                }
            }

            state.ActiveSession = null;
        }
    }

    private void ParseTimeCommand(string args, IssueState state, DateTime timestamp)
    {
        // Parse time log format: time 2h30m --description "Fixed bug"
        var duration = ParseTimeSpan(args);
        if (duration.HasValue)
        {
            var session = new WorkSession
            {
                Id = Guid.NewGuid().ToString(),
                StartedAt = timestamp - duration.Value,
                EndedAt = timestamp,
                Duration = duration.Value
            };

            // Check for description
            var descMatch = Regex.Match(args, @"--description\s+""([^""]+)""");
            if (descMatch.Success)
            {
                session.Description = descMatch.Groups[1].Value;
            }

            state.Sessions.Add(session);
        }
    }

    private TimeSpan? ParseTimeSpan(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        var matches = TimeSpanRegex.Matches(input);
        if (matches.Count == 0)
            return null;

        int hours = 0, minutes = 0;

        foreach (Match match in matches)
        {
            var value = int.Parse(match.Groups[1].Value);
            var unit = match.Groups[2].Value.ToLower();

            if (unit == "h")
                hours += value;
            else if (unit == "m")
                minutes += value;
        }

        return new TimeSpan(hours, minutes, 0);
    }
}