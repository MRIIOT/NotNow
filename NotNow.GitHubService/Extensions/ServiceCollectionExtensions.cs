using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NotNow.GitHubService.Models;
using NotNow.GitHubService.Configuration;
using NotNow.GitHubService.Interfaces;
using NotNow.GitHubService.Services;

namespace NotNow.GitHubService.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGitHubService(this IServiceCollection services, IConfiguration configuration)
    {
        // Support both old single-repo and new multi-repo formats
        var multiRepoConfig = configuration.GetSection("GitHubRepositories").Get<List<GitHubRepositoryConfig>>();

        if (multiRepoConfig != null && multiRepoConfig.Any())
        {
            // New multi-repo configuration
            services.AddSingleton<IGitHubServiceManager>(provider =>
            {
                var manager = new GitHubServiceManager();
                manager.Initialize(multiRepoConfig);
                return manager;
            });
        }
        else
        {
            // Fallback to old single-repo configuration for backward compatibility
            var singleRepoConfig = configuration.GetSection("GitHub").Get<GitHubSettings>();
            if (singleRepoConfig != null)
            {
                // Convert single repo to multi-repo format
                var repoConfig = new GitHubRepositoryConfig
                {
                    Id = "default",
                    DisplayName = $"{singleRepoConfig.Owner}/{singleRepoConfig.Repository}",
                    Owner = singleRepoConfig.Owner,
                    Repository = singleRepoConfig.Repository,
                    PersonalAccessToken = singleRepoConfig.PersonalAccessToken
                };

                services.AddSingleton<IGitHubServiceManager>(provider =>
                {
                    var manager = new GitHubServiceManager();
                    manager.Initialize(new List<GitHubRepositoryConfig> { repoConfig });
                    return manager;
                });
            }
            else
            {
                // No configuration found, add empty manager
                services.AddSingleton<IGitHubServiceManager>(provider =>
                {
                    var manager = new GitHubServiceManager();
                    manager.Initialize(new List<GitHubRepositoryConfig>());
                    return manager;
                });
            }
        }

        // Register IGitHubService as a factory that gets the current service from the manager
        // This maintains backward compatibility for code that still uses IGitHubService directly
        services.AddScoped<IGitHubService>(provider =>
        {
            var manager = provider.GetService<IGitHubServiceManager>();
            if (manager != null)
            {
                var currentService = manager.GetCurrentService();
                if (currentService != null)
                {
                    return currentService;
                }
            }

            // If no repositories configured, return a dummy service that throws meaningful errors
            var dummySettings = Options.Create(new GitHubSettings
            {
                PersonalAccessToken = "dummy",
                Owner = "not-configured",
                Repository = "not-configured"
            });
            return new Services.GitHubService(dummySettings);
        });

        return services;
    }

    public static IServiceCollection AddGitHubService(this IServiceCollection services, Action<GitHubSettings> configureOptions)
    {
        services.Configure(configureOptions);
        services.AddScoped<IGitHubService, Services.GitHubService>();

        return services;
    }
}