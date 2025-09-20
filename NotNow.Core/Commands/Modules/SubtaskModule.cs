using NotNow.Core.Commands.Framework;
using NotNow.Core.Commands.Modules.Subtasks;

namespace NotNow.Core.Commands.Modules;

public class SubtaskModule : ICommandModule
{
    public string ModuleName => "Subtasks";
    public string Version => "1.0.0";

    public List<CommandRegistration> GetCommands()
    {
        return new List<CommandRegistration>
        {
            new CommandRegistration
            {
                Name = "subtask",
                Aliases = new[] { "task" },
                Description = "Manage subtasks",
                Context = CommandContext.Both,
                HandlerType = typeof(SubtaskHandler),
                Schema = new CommandSchema
                {
                    Parameters = new List<CommandParameter>
                    {
                        new() { Name = "action", Type = typeof(string), Required = false, DefaultValue = "add", Description = "Action (add/complete/remove/list)" },
                        new() { Name = "title", Type = typeof(string), Required = false, Description = "Subtask title" }
                    },
                    Options = new List<CommandOption>
                    {
                        new() { LongName = "id", Type = typeof(string), Description = "Subtask ID" },
                        new() { LongName = "estimate", Type = typeof(string), Description = "Time estimate" },
                        new() { LongName = "depends", Type = typeof(string), Description = "Dependencies (comma-separated IDs)" },
                        new() { LongName = "assignee", Type = typeof(string), Description = "Assignee username" }
                    }
                }
            },

            new CommandRegistration
            {
                Name = "complete",
                Aliases = new[] { "done", "finish" },
                Description = "Mark subtask as complete",
                Context = CommandContext.Comment,
                HandlerType = typeof(CompleteSubtaskHandler),
                Schema = new CommandSchema
                {
                    Parameters = new List<CommandParameter>
                    {
                        new() { Name = "id", Type = typeof(string), Required = true, Description = "Subtask ID to complete" }
                    },
                    Options = new List<CommandOption>
                    {
                        new() { LongName = "time", Type = typeof(string), Description = "Time spent on subtask" },
                        new() { LongName = "notes", Type = typeof(string), Description = "Completion notes" }
                    }
                }
            },

            new CommandRegistration
            {
                Name = "reopen",
                Description = "Reopen completed subtask",
                Context = CommandContext.Comment,
                HandlerType = typeof(ReopenSubtaskHandler),
                Schema = new CommandSchema
                {
                    Parameters = new List<CommandParameter>
                    {
                        new() { Name = "id", Type = typeof(string), Required = true, Description = "Subtask ID to reopen" }
                    },
                    Options = new List<CommandOption>
                    {
                        new() { LongName = "reason", Type = typeof(string), Description = "Reason for reopening" }
                    }
                }
            }
        };
    }

    public Task OnModuleInitialize(IServiceProvider services)
    {
        return Task.CompletedTask;
    }
}