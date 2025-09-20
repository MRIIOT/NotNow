using System;
using System.Collections.Generic;
using System.Linq;
using NotNow.Core.Models;
using Octokit;

namespace NotNow.Core.Services;

public interface IIssueStateService
{
    IssueState GetOrCreateState(int issueNumber);
    void SetState(int issueNumber, IssueState state);
    void UpdateState(int issueNumber, Action<IssueState> updateAction);
    void ClearState(int issueNumber);
    bool HasState(int issueNumber);
}

public class IssueStateService : IIssueStateService
{
    private readonly Dictionary<int, IssueState> _states = new();

    public IssueState GetOrCreateState(int issueNumber)
    {
        if (!_states.ContainsKey(issueNumber))
        {
            _states[issueNumber] = new IssueState
            {
                IssueNumber = issueNumber,
                Status = "todo",
                Priority = "medium",
                Tags = new List<string>(),
                Subtasks = new List<Subtask>(),
                Sessions = new List<WorkSession>()
            };
        }

        return _states[issueNumber];
    }

    public void SetState(int issueNumber, IssueState state)
    {
        _states[issueNumber] = state;
    }

    public void UpdateState(int issueNumber, Action<IssueState> updateAction)
    {
        var state = GetOrCreateState(issueNumber);
        updateAction(state);
    }

    public void ClearState(int issueNumber)
    {
        _states.Remove(issueNumber);
    }

    public bool HasState(int issueNumber)
    {
        return _states.ContainsKey(issueNumber);
    }
}