using System;
using System.Text.Json;
using System.Text.RegularExpressions;
using NotNow.Core.Models;
using Octokit;

namespace NotNow.Core.Services
{
    public interface IIssueStateManager
    {
        IssueStateVersion? ExtractStateFromBody(string? issueBody);
        string EmbedStateInBody(string? originalBody, IssueStateVersion state);
        IssueStateVersion CreateNewVersion(IssueState currentState, string command, string clientId);
        IssueStateVersion IncrementVersion(IssueStateVersion current, IssueState newState, string command, string clientId);
        bool IsStateStale(IssueStateVersion? state, TimeSpan maxAge);
    }

    public class IssueStateManager : IIssueStateManager
    {
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public IssueStateVersion? ExtractStateFromBody(string? issueBody)
        {
            if (string.IsNullOrWhiteSpace(issueBody))
                return null;

            var pattern = $@"{Regex.Escape(IssueStateVersion.StateBeginMarker)}(.*?){Regex.Escape(IssueStateVersion.StateEndMarker)}";
            var match = Regex.Match(issueBody, pattern, RegexOptions.Singleline);

            if (!match.Success)
                return null;

            try
            {
                var jsonContent = match.Groups[1].Value.Trim();

                // Remove HTML comment markers
                jsonContent = Regex.Replace(jsonContent, @"^\s*<!--\s*", "", RegexOptions.Multiline);
                jsonContent = Regex.Replace(jsonContent, @"\s*-->\s*$", "", RegexOptions.Multiline);

                return JsonSerializer.Deserialize<IssueStateVersion>(jsonContent, _jsonOptions);
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Failed to deserialize state from body: {ex.Message}");
                return null;
            }
        }

        public string EmbedStateInBody(string? originalBody, IssueStateVersion state)
        {
            var json = JsonSerializer.Serialize(state, _jsonOptions);
            var stateSection = $@"
{IssueStateVersion.StateBeginMarker}
<!--
{json}
-->
{IssueStateVersion.StateEndMarker}";

            if (string.IsNullOrWhiteSpace(originalBody))
            {
                return stateSection.TrimStart();
            }

            // Remove existing state section if present
            var pattern = $@"{Regex.Escape(IssueStateVersion.StateBeginMarker)}.*?{Regex.Escape(IssueStateVersion.StateEndMarker)}";
            var cleanedBody = Regex.Replace(originalBody, pattern, "", RegexOptions.Singleline).TrimEnd();

            return $"{cleanedBody}\n{stateSection}";
        }

        public IssueStateVersion CreateNewVersion(IssueState currentState, string command, string clientId)
        {
            return new IssueStateVersion
            {
                SchemaVersion = IssueStateVersion.CurrentSchemaVersion,
                StateVersion = 1,
                LastUpdated = DateTime.UtcNow,
                LastUpdatedBy = clientId,
                LastCommand = command,
                Data = ConvertToVersionedData(currentState)
            };
        }

        public IssueStateVersion IncrementVersion(IssueStateVersion current, IssueState newState, string command, string clientId)
        {
            return new IssueStateVersion
            {
                SchemaVersion = IssueStateVersion.CurrentSchemaVersion,
                StateVersion = current.StateVersion + 1,
                LastUpdated = DateTime.UtcNow,
                LastUpdatedBy = clientId,
                LastCommand = command,
                Data = ConvertToVersionedData(newState)
            };
        }

        public bool IsStateStale(IssueStateVersion? state, TimeSpan maxAge)
        {
            if (state == null)
                return true;

            return DateTime.UtcNow - state.LastUpdated > maxAge;
        }

        private IssueStateData ConvertToVersionedData(IssueState state)
        {
            return new IssueStateData
            {
                IssueNumber = state.IssueNumber,
                Title = state.Title,
                Status = state.Status,
                Priority = state.Priority,
                Type = state.Type,
                Assignee = state.Assignee,
                Estimate = state.Estimate,
                DueDate = state.DueDate,
                Tags = new List<string>(state.Tags),
                Subtasks = new List<Subtask>(state.Subtasks),
                Sessions = new List<WorkSession>(state.Sessions),
                TotalTimeSpent = state.TotalTimeSpent,
                IsInitialized = state.IsInitialized
            };
        }
    }
}