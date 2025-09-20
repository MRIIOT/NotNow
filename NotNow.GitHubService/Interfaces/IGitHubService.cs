using Octokit;

namespace NotNow.GitHubService.Interfaces;

public interface IGitHubService
{
    Task<IReadOnlyList<Issue>> GetIssuesAsync(ItemStateFilter state = ItemStateFilter.Open);
    Task<IReadOnlyList<IssueComment>> GetIssueCommentsAsync(int issueNumber);
    Task<Issue> CreateIssueAsync(string title, string body, string[]? labels = null, string[]? assignees = null);
    Task<IssueComment> AddCommentToIssueAsync(int issueNumber, string comment);
}