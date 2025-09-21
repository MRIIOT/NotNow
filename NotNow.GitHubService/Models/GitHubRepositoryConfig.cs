namespace NotNow.GitHubService.Models;

public class GitHubRepositoryConfig
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string Repository { get; set; } = string.Empty;
    public string PersonalAccessToken { get; set; } = string.Empty;
}