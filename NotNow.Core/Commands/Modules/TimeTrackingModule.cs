using NotNow.Core.Commands.Framework;
using NotNow.Core.Commands.Modules.TimeTracking;

namespace NotNow.Core.Commands.Modules;

public class TimeTrackingModule : ICommandModule
{
    public string ModuleName => "TimeTracking";
    public string Version => "1.0.0";

    public List<CommandRegistration> GetCommands()
    {
        return new List<CommandRegistration>
        {
            new CommandRegistration
            {
                Name = "time",
                Aliases = new[] { "log", "track" },
                Description = "Log time spent",
                Context = CommandContext.Comment,
                HandlerType = typeof(TimeCommandHandler),
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
                            Validator = (v) => v is string s && System.Text.RegularExpressions.Regex.IsMatch(s, @"^\d+[hm]")
                        }
                    },
                    Options = new List<CommandOption>
                    {
                        new() { LongName = "description", ShortName = "d", Type = typeof(string), Description = "Work description" },
                        new() { LongName = "date", Type = typeof(DateTime), Description = "Date of work (default: today)" }
                    }
                }
            },

            new CommandRegistration
            {
                Name = "start",
                Aliases = new[] { "begin" },
                Description = "Start work session",
                Context = CommandContext.Comment,
                HandlerType = typeof(StartWorkHandler),
                Schema = new CommandSchema
                {
                    Options = new List<CommandOption>
                    {
                        new() { LongName = "description", ShortName = "d", Type = typeof(string), Description = "Session description" }
                    }
                }
            },

            new CommandRegistration
            {
                Name = "stop",
                Aliases = new[] { "pause", "end" },
                Description = "Stop work session",
                Context = CommandContext.Comment,
                HandlerType = typeof(StopWorkHandler),
                Schema = new CommandSchema
                {
                    Options = new List<CommandOption>
                    {
                        new() { LongName = "description", ShortName = "d", Type = typeof(string), Description = "Session summary" }
                    }
                }
            },

            new CommandRegistration
            {
                Name = "session",
                Description = "View current session info",
                Context = CommandContext.Comment,
                HandlerType = typeof(SessionInfoHandler),
                Schema = new CommandSchema()
            },

            new CommandRegistration
            {
                Name = "timespent",
                Aliases = new[] { "total" },
                Description = "View total time spent",
                Context = CommandContext.Comment,
                HandlerType = typeof(TimeSpentHandler),
                Schema = new CommandSchema
                {
                    Options = new List<CommandOption>
                    {
                        new() { LongName = "user", ShortName = "u", Type = typeof(string), Description = "Filter by user" },
                        new() { LongName = "from", Type = typeof(DateTime), Description = "Start date" },
                        new() { LongName = "to", Type = typeof(DateTime), Description = "End date" }
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