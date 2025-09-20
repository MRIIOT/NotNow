using NotNow.Core.Commands.Framework;
using NotNow.Core.Commands.Modules.Core;

namespace NotNow.Core.Commands.Modules;

public class CoreModule : ICommandModule
{
    public string ModuleName => "Core";
    public string Version => "1.0.0";

    public List<CommandRegistration> GetCommands()
    {
        return new List<CommandRegistration>
        {
            new CommandRegistration
            {
                Name = "init",
                Description = "Initialize NotNow tracking for an issue",
                Context = CommandContext.IssueBody,
                HandlerType = typeof(InitCommandHandler),
                Schema = new CommandSchema
                {
                    Options = new List<CommandOption>
                    {
                        new() { LongName = "type", Type = typeof(string), Description = "Issue type (bug, feature, task)" },
                        new() { LongName = "priority", Type = typeof(string), Description = "Priority level (low, medium, high, critical)" },
                        new() { LongName = "workflow", Type = typeof(string), Description = "Workflow to use" }
                    }
                }
            },

            new CommandRegistration
            {
                Name = "status",
                Description = "Change issue status",
                Context = CommandContext.Comment,
                HandlerType = typeof(StatusCommandHandler),
                Schema = new CommandSchema
                {
                    Parameters = new List<CommandParameter>
                    {
                        new() { Name = "status", Type = typeof(string), Required = true, Description = "New status" }
                    },
                    Options = new List<CommandOption>
                    {
                        new() { LongName = "reason", ShortName = "r", Type = typeof(string), Description = "Reason for status change" }
                    }
                }
            },

            new CommandRegistration
            {
                Name = "assign",
                Aliases = new[] { "assignee" },
                Description = "Assign issue to user",
                Context = CommandContext.Both,
                HandlerType = typeof(AssignCommandHandler),
                Schema = new CommandSchema
                {
                    Parameters = new List<CommandParameter>
                    {
                        new() { Name = "user", Type = typeof(string), Required = true, Description = "Username to assign" }
                    }
                }
            },

            new CommandRegistration
            {
                Name = "due",
                Aliases = new[] { "deadline" },
                Description = "Set or update due date",
                Context = CommandContext.Both,
                HandlerType = typeof(DueCommandHandler),
                Schema = new CommandSchema
                {
                    Parameters = new List<CommandParameter>
                    {
                        new() { Name = "date", Type = typeof(DateTime), Required = true, Description = "Due date (YYYY-MM-DD)" }
                    }
                }
            },

            new CommandRegistration
            {
                Name = "estimate",
                Description = "Set time estimate",
                Context = CommandContext.Both,
                HandlerType = typeof(EstimateCommandHandler),
                Schema = new CommandSchema
                {
                    Parameters = new List<CommandParameter>
                    {
                        new()
                        {
                            Name = "duration",
                            Type = typeof(string),
                            Required = true,
                            Description = "Duration (e.g., 2h30m)",
                            Validator = (v) => v is string s && System.Text.RegularExpressions.Regex.IsMatch(s, @"^\d+[hms]")
                        }
                    },
                    Options = new List<CommandOption>
                    {
                        new() { LongName = "update", Type = typeof(bool), Description = "Update existing estimate" }
                    }
                }
            },

            new CommandRegistration
            {
                Name = "tags",
                Aliases = new[] { "tag", "labels" },
                Description = "Manage tags",
                Context = CommandContext.Both,
                HandlerType = typeof(TagsCommandHandler),
                Schema = new CommandSchema
                {
                    Parameters = new List<CommandParameter>
                    {
                        new() { Name = "action", Type = typeof(string), Required = false, DefaultValue = "add", Description = "Action (add/remove/set)" },
                        new() { Name = "tags", Type = typeof(string), Required = false, Description = "Comma-separated tags" }
                    }
                }
            },

            new CommandRegistration
            {
                Name = "priority",
                Description = "Set priority level",
                Context = CommandContext.Both,
                HandlerType = typeof(PriorityCommandHandler),
                Schema = new CommandSchema
                {
                    Parameters = new List<CommandParameter>
                    {
                        new() { Name = "level", Type = typeof(string), Required = true, Description = "Priority (low, medium, high, critical)" }
                    }
                }
            },

            new CommandRegistration
            {
                Name = "comment",
                Aliases = new[] { "c", "msg" },
                Description = "Post a comment to the issue",
                Context = CommandContext.Comment,
                HandlerType = typeof(CommentCommandHandler),
                Schema = new CommandSchema
                {
                    Parameters = new List<CommandParameter>
                    {
                        new() { Name = "message", Type = typeof(string), Required = false, Description = "Comment text" }
                    },
                    Options = new List<CommandOption>
                    {
                        new() { LongName = "body", ShortName = "b", Type = typeof(string), Description = "Comment body (alternative to message parameter)" },
                        new() { LongName = "markdown", ShortName = "m", Type = typeof(bool), Description = "Format as markdown quote" }
                    }
                }
            },

            new CommandRegistration
            {
                Name = "note",
                Aliases = new[] { "n" },
                Description = "Add a formatted note to the issue",
                Context = CommandContext.Comment,
                HandlerType = typeof(NoteCommandHandler),
                Schema = new CommandSchema
                {
                    Parameters = new List<CommandParameter>
                    {
                        new() { Name = "text", Type = typeof(string), Required = true, Description = "Note content" }
                    },
                    Options = new List<CommandOption>
                    {
                        new() { LongName = "title", ShortName = "t", Type = typeof(string), Description = "Note title" },
                        new() { LongName = "category", ShortName = "c", Type = typeof(string), Description = "Note category (default: note)" }
                    }
                }
            },

            new CommandRegistration
            {
                Name = "update",
                Aliases = new[] { "u", "progress" },
                Description = "Post a status update with progress",
                Context = CommandContext.Comment,
                HandlerType = typeof(UpdateCommandHandler),
                Schema = new CommandSchema
                {
                    Parameters = new List<CommandParameter>
                    {
                        new() { Name = "message", Type = typeof(string), Required = false, Description = "Update message" }
                    },
                    Options = new List<CommandOption>
                    {
                        new() { LongName = "progress", ShortName = "p", Type = typeof(int), Description = "Progress percentage (0-100)" },
                        new() { LongName = "blockers", ShortName = "b", Type = typeof(string), Description = "Comma-separated list of blockers" },
                        new() { LongName = "next", ShortName = "n", Type = typeof(string), Description = "Comma-separated list of next steps" }
                    }
                }
            }
        };
    }

    public Task OnModuleInitialize(IServiceProvider services)
    {
        // Module initialization if needed
        return Task.CompletedTask;
    }
}