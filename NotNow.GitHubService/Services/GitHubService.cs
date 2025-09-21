using Microsoft.Extensions.Options;
using NotNow.GitHubService.Configuration;
using NotNow.GitHubService.Interfaces;
using Octokit;

namespace NotNow.GitHubService.Services;

public class GitHubService : IGitHubService
{
    private readonly GitHubClient _client;
    private readonly GitHubSettings _settings;

    public GitHubService(IOptions<GitHubSettings> options)
    {
        _settings = options.Value;

        if (string.IsNullOrEmpty(_settings.PersonalAccessToken))
            throw new ArgumentException("Personal Access Token is required");

        if (string.IsNullOrEmpty(_settings.Owner))
            throw new ArgumentException("Repository Owner is required");

        if (string.IsNullOrEmpty(_settings.Repository))
            throw new ArgumentException("Repository name is required");

        _client = new GitHubClient(new ProductHeaderValue("NotNow.GitHubService"))
        {
            Credentials = new Credentials(_settings.PersonalAccessToken)
        };
    }

    public async Task<IReadOnlyList<Issue>> GetIssuesAsync(ItemStateFilter state = ItemStateFilter.Open)
    {
        try
        {
            var issueRequest = new RepositoryIssueRequest
            {
                State = state
            };

            var issues = await _client.Issue.GetAllForRepository(
                _settings.Owner,
                _settings.Repository,
                issueRequest);

            return issues;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error retrieving issues: {ex.Message}", ex);
        }
    }

    public async Task<IReadOnlyList<IssueComment>> GetIssueCommentsAsync(int issueNumber)
    {
        try
        {
            var comments = await _client.Issue.Comment.GetAllForIssue(
                _settings.Owner,
                _settings.Repository,
                issueNumber);

            return comments;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error retrieving comments for issue #{issueNumber}: {ex.Message}", ex);
        }
    }

    public async Task<Issue> CreateIssueAsync(string title, string body, string[]? labels = null, string[]? assignees = null)
    {
        try
        {
            var newIssue = new NewIssue(title)
            {
                Body = body
            };

            if (labels != null && labels.Length > 0)
            {
                foreach (var label in labels)
                {
                    newIssue.Labels.Add(label);
                }
            }

            if (assignees != null && assignees.Length > 0)
            {
                foreach (var assignee in assignees)
                {
                    newIssue.Assignees.Add(assignee);
                }
            }

            var issue = await _client.Issue.Create(
                _settings.Owner,
                _settings.Repository,
                newIssue);

            return issue;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error creating issue: {ex.Message}", ex);
        }
    }

    public async Task<IssueComment> AddCommentToIssueAsync(int issueNumber, string comment)
    {
        try
        {
            var issueComment = await _client.Issue.Comment.Create(
                _settings.Owner,
                _settings.Repository,
                issueNumber,
                comment);

            return issueComment;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error adding comment to issue #{issueNumber}: {ex.Message}", ex);
        }
    }

    public async Task<Issue> CloseIssueAsync(int issueNumber)
    {
        try
        {
            var issueUpdate = new IssueUpdate
            {
                State = ItemState.Closed
            };

            var issue = await _client.Issue.Update(
                _settings.Owner,
                _settings.Repository,
                issueNumber,
                issueUpdate);

            return issue;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error closing issue #{issueNumber}: {ex.Message}", ex);
        }
    }

    public async Task<Issue> ReopenIssueAsync(int issueNumber)
    {
        try
        {
            var issueUpdate = new IssueUpdate
            {
                State = ItemState.Open
            };

            var issue = await _client.Issue.Update(
                _settings.Owner,
                _settings.Repository,
                issueNumber,
                issueUpdate);

            return issue;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error reopening issue #{issueNumber}: {ex.Message}", ex);
        }
    }
}