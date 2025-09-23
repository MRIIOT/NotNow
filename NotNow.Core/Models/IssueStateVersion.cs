using System;

namespace NotNow.Core.Models
{
    public class IssueStateVersion
    {
        public const string CurrentSchemaVersion = "2.0";
        public const string StateBeginMarker = "<!-- NOTNOW-STATE-BEGIN -->";
        public const string StateEndMarker = "<!-- NOTNOW-STATE-END -->";

        public string SchemaVersion { get; set; } = CurrentSchemaVersion;
        public int StateVersion { get; set; } = 1;
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
        public string LastUpdatedBy { get; set; } = string.Empty;
        public string LastCommand { get; set; } = string.Empty;

        public IssueStateData Data { get; set; } = new();
    }

    public class IssueStateData
    {
        public int IssueNumber { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Status { get; set; } = "todo";
        public string Priority { get; set; } = "medium";
        public string? Type { get; set; }
        public string? Assignee { get; set; }
        public string? Estimate { get; set; }
        public DateTime? DueDate { get; set; }
        public List<string> Tags { get; set; } = new();
        public List<Subtask> Subtasks { get; set; } = new();
        public List<WorkSession> Sessions { get; set; } = new();
        public TimeSpan TotalTimeSpent { get; set; }
        public bool IsInitialized { get; set; }

        public TaskCounts GetTaskCounts()
        {
            return new TaskCounts
            {
                Open = Subtasks.Count(s => s.Status != "done"),
                Total = Subtasks.Count
            };
        }
    }

    public class TaskCounts
    {
        public int Open { get; set; }
        public int Total { get; set; }

        public string Display => Total > 0 ? $"[{Open}/{Total}]" : string.Empty;
    }
}