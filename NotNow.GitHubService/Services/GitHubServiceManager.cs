using NotNow.GitHubService.Models;
using NotNow.GitHubService.Configuration;
using NotNow.GitHubService.Interfaces;
using Microsoft.Extensions.Options;
using Octokit;

namespace NotNow.GitHubService.Services;

public interface IGitHubServiceManager
{
    void Initialize(List<GitHubRepositoryConfig> configs);
    IGitHubService GetService(string repositoryId);
    List<GitHubRepositoryConfig> GetRepositories();
    GitHubRepositoryConfig? GetRepository(string repositoryId);
    void UpdateServices(List<GitHubRepositoryConfig> configs);
    string? CurrentRepositoryId { get; set; }
    IGitHubService? GetCurrentService();
}

public class GitHubServiceManager : IGitHubServiceManager
{
    private Dictionary<string, IGitHubService> _services = new();
    private List<GitHubRepositoryConfig> _configs = new();
    public string? CurrentRepositoryId { get; set; }

    public void Initialize(List<GitHubRepositoryConfig> configs)
    {
        _configs = configs ?? new List<GitHubRepositoryConfig>();
        _services.Clear();

        foreach (var config in _configs)
        {
            CreateService(config);
        }
    }

    public void UpdateServices(List<GitHubRepositoryConfig> configs)
    {
        // Dispose old services if needed
        _services.Clear();

        // Reinitialize with new configs
        Initialize(configs);
    }

    private void CreateService(GitHubRepositoryConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.Id) ||
            string.IsNullOrWhiteSpace(config.PersonalAccessToken) ||
            string.IsNullOrWhiteSpace(config.Owner) ||
            string.IsNullOrWhiteSpace(config.Repository))
        {
            // Skip invalid configs
            return;
        }

        // Create GitHubSettings from config
        var settings = new GitHubSettings
        {
            PersonalAccessToken = config.PersonalAccessToken,
            Owner = config.Owner,
            Repository = config.Repository
        };

        var optionsWrapper = Options.Create(settings);
        var service = new GitHubService(optionsWrapper);

        _services[config.Id] = service;
    }

    public IGitHubService GetService(string repositoryId)
    {
        if (string.IsNullOrEmpty(repositoryId))
        {
            throw new ArgumentException("Repository ID cannot be null or empty", nameof(repositoryId));
        }

        if (_services.TryGetValue(repositoryId, out var service))
        {
            return service;
        }

        throw new InvalidOperationException($"Repository '{repositoryId}' not configured or has invalid configuration");
    }

    public List<GitHubRepositoryConfig> GetRepositories()
    {
        return _configs.Where(c => _services.ContainsKey(c.Id)).ToList();
    }

    public GitHubRepositoryConfig? GetRepository(string repositoryId)
    {
        return _configs.FirstOrDefault(c => c.Id == repositoryId);
    }

    public IGitHubService? GetCurrentService()
    {
        if (string.IsNullOrEmpty(CurrentRepositoryId))
        {
            // Return the first available service if no current repository is set
            var firstRepo = _configs.FirstOrDefault(c => _services.ContainsKey(c.Id));
            if (firstRepo != null)
            {
                CurrentRepositoryId = firstRepo.Id;
                return _services[firstRepo.Id];
            }
            return null;
        }

        if (_services.TryGetValue(CurrentRepositoryId, out var service))
        {
            return service;
        }

        return null;
    }
}