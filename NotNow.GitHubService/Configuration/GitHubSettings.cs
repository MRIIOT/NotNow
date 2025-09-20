namespace NotNow.GitHubService.Configuration;

public class GitHubSettings
{
    public string PersonalAccessToken { get; set; } = string.Empty;
    public string RepositoryUrl { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string Repository { get; set; } = string.Empty;
}