using Octokit;

namespace NotNow.GitHubService.Interfaces;

public interface IGitHubService
{
    Task<IReadOnlyList<Issue>> GetIssuesAsync(ItemStateFilter state = ItemStateFilter.Open);
    Task<IReadOnlyList<IssueComment>> GetIssueCommentsAsync(int issueNumber);
    Task<Issue> CreateIssueAsync(string title, string body, string[]? labels = null, string[]? assignees = null);
    Task<IssueComment> AddCommentToIssueAsync(int issueNumber, string comment);
    Task<Issue> CloseIssueAsync(int issueNumber);
    Task<Issue> ReopenIssueAsync(int issueNumber);
    Task<Issue> UpdateIssueAsync(int issueNumber, string? title = null, string? body = null, ItemState? state = null);
    Task<Issue> GetIssueAsync(int issueNumber);
    Task<User> GetCurrentUserAsync();
}