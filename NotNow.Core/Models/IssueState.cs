using System;
using System.Collections.Generic;

namespace NotNow.Core.Models;

public class IssueState
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
    public DateTime? LastUpdated { get; set; }
    public bool IsInitialized { get; set; }

    // Current session if active
    public WorkSession? ActiveSession { get; set; }
}

public class Subtask
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public string? Estimate { get; set; }
    public string? Assignee { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class WorkSession
{
    public string Id { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public TimeSpan Duration { get; set; }
    public string? Description { get; set; }
    public string? User { get; set; }
}