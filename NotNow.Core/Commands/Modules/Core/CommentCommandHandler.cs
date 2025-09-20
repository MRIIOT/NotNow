using Microsoft.Extensions.DependencyInjection;
using NotNow.Core.Commands.Framework;
using NotNow.GitHubService.Interfaces;

namespace NotNow.Core.Commands.Modules.Core;

public class CommentArgs : CommandArgs { }

public class CommentCommandHandler : CommandHandler<CommentArgs>
{
    private readonly IServiceProvider _serviceProvider;

    public CommentCommandHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public override async Task<CommandResult> ExecuteAsync(CommandExecutionContext context, CommentArgs args)
    {
        var message = args.GetParameter<string>("message");
        var body = args.GetOption<string>("body");
        var markdown = args.GetOption<bool>("markdown");

        // Use body if provided, otherwise use message parameter
        var commentText = !string.IsNullOrEmpty(body) ? body : message;

        if (string.IsNullOrEmpty(commentText))
        {
            return CommandResult.Failure("Comment text is required. Use message parameter or --body option.");
        }

        try
        {
            // Get the GitHub service
            using var scope = _serviceProvider.CreateScope();
            var gitHubService = scope.ServiceProvider.GetService<IGitHubService>();

            if (gitHubService == null)
            {
                return CommandResult.Failure("GitHub service not available. Cannot post comment.");
            }

            // Format the comment if markdown is requested
            if (markdown)
            {
                commentText = FormatAsMarkdown(commentText);
            }

            // Post the comment to GitHub
            var comment = await gitHubService.AddCommentToIssueAsync(context.IssueNumber, commentText);

            var result = new
            {
                CommentId = comment.Id,
                Author = comment.User.Login,
                CreatedAt = comment.CreatedAt,
                Url = comment.HtmlUrl,
                BodyLength = comment.Body.Length
            };

            return CommandResult.Ok($"Comment posted successfully by {comment.User.Login}", result);
        }
        catch (Exception ex)
        {
            return CommandResult.Failure($"Failed to post comment: {ex.Message}");
        }
    }

    private string FormatAsMarkdown(string text)
    {
        // Basic markdown formatting - can be extended
        if (!text.Contains("```") && !text.Contains("#") && !text.Contains("**"))
        {
            // If no markdown detected, wrap in a quote block
            return $"> {text.Replace("\n", "\n> ")}";
        }
        return text;
    }
}

public class NoteArgs : CommandArgs { }

public class NoteCommandHandler : CommandHandler<NoteArgs>
{
    private readonly IServiceProvider _serviceProvider;

    public NoteCommandHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public override async Task<CommandResult> ExecuteAsync(CommandExecutionContext context, NoteArgs args)
    {
        var text = args.GetParameter<string>("text");
        var title = args.GetOption<string>("title");
        var category = args.GetOption<string>("category", "note");

        if (string.IsNullOrEmpty(text))
        {
            return CommandResult.Failure("Note text is required.");
        }

        try
        {
            // Get the GitHub service
            using var scope = _serviceProvider.CreateScope();
            var gitHubService = scope.ServiceProvider.GetService<IGitHubService>();

            if (gitHubService == null)
            {
                return CommandResult.Failure("GitHub service not available. Cannot post note.");
            }

            // Format the note with metadata
            var formattedNote = FormatNote(text, title, category, context.User);

            // Post the note as a comment to GitHub
            var comment = await gitHubService.AddCommentToIssueAsync(context.IssueNumber, formattedNote);

            var result = new
            {
                CommentId = comment.Id,
                Author = comment.User.Login,
                CreatedAt = comment.CreatedAt,
                Category = category,
                Title = title
            };

            return CommandResult.Ok($"Note added to issue #{context.IssueNumber}", result);
        }
        catch (Exception ex)
        {
            return CommandResult.Failure($"Failed to post note: {ex.Message}");
        }
    }

    private string FormatNote(string text, string? title, string category, string user)
    {
        var formatted = "### 📝 Note";

        if (!string.IsNullOrEmpty(title))
        {
            formatted = $"### 📝 {title}";
        }

        formatted += $"\n\n**Category:** {category}";
        formatted += $"\n**Author:** @{user}";
        formatted += $"\n**Date:** {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC";
        formatted += "\n\n---\n\n";
        formatted += text;

        return formatted;
    }
}

public class UpdateArgs : CommandArgs { }

public class UpdateCommandHandler : CommandHandler<UpdateArgs>
{
    private readonly IServiceProvider _serviceProvider;

    public UpdateCommandHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public override async Task<CommandResult> ExecuteAsync(CommandExecutionContext context, UpdateArgs args)
    {
        var message = args.GetParameter<string>("message");
        var progress = args.GetOption<int>("progress");
        var blockers = args.GetOption<string>("blockers");
        var nextSteps = args.GetOption<string>("next");

        if (string.IsNullOrEmpty(message) && !progress.HasValue && string.IsNullOrEmpty(blockers) && string.IsNullOrEmpty(nextSteps))
        {
            return CommandResult.Failure("Update requires at least a message, progress percentage, blockers, or next steps.");
        }

        try
        {
            // Get the GitHub service
            using var scope = _serviceProvider.CreateScope();
            var gitHubService = scope.ServiceProvider.GetService<IGitHubService>();

            if (gitHubService == null)
            {
                return CommandResult.Failure("GitHub service not available. Cannot post update.");
            }

            // Format the update
            var formattedUpdate = FormatUpdate(message, progress, blockers, nextSteps, context.User);

            // Post the update as a comment to GitHub
            var comment = await gitHubService.AddCommentToIssueAsync(context.IssueNumber, formattedUpdate);

            var result = new
            {
                CommentId = comment.Id,
                Author = comment.User.Login,
                CreatedAt = comment.CreatedAt,
                Progress = progress,
                HasBlockers = !string.IsNullOrEmpty(blockers)
            };

            return CommandResult.Ok("Status update posted successfully", result);
        }
        catch (Exception ex)
        {
            return CommandResult.Failure($"Failed to post update: {ex.Message}");
        }
    }

    private string FormatUpdate(string? message, int? progress, string? blockers, string? nextSteps, string user)
    {
        var formatted = "## 📊 Status Update\n\n";
        formatted += $"**From:** @{user}\n";
        formatted += $"**Date:** {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC\n\n";

        if (progress.HasValue)
        {
            formatted += $"### Progress: {progress}%\n";
            formatted += GenerateProgressBar(progress.Value) + "\n\n";
        }

        if (!string.IsNullOrEmpty(message))
        {
            formatted += "### Update\n";
            formatted += message + "\n\n";
        }

        if (!string.IsNullOrEmpty(blockers))
        {
            formatted += "### 🚧 Blockers\n";
            var blockerList = blockers.Split(',', StringSplitOptions.RemoveEmptyEntries);
            foreach (var blocker in blockerList)
            {
                formatted += $"- {blocker.Trim()}\n";
            }
            formatted += "\n";
        }

        if (!string.IsNullOrEmpty(nextSteps))
        {
            formatted += "### ➡️ Next Steps\n";
            var stepsList = nextSteps.Split(',', StringSplitOptions.RemoveEmptyEntries);
            foreach (var step in stepsList)
            {
                formatted += $"- [ ] {step.Trim()}\n";
            }
        }

        return formatted;
    }

    private string GenerateProgressBar(int percentage)
    {
        percentage = Math.Max(0, Math.Min(100, percentage)); // Clamp to 0-100
        int filled = percentage / 5; // 20 segments total
        int empty = 20 - filled;

        return $"[{'█' * filled}{('░' * empty)}]";
    }
}