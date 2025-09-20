using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NotNow.GitHubService.Configuration;
using NotNow.GitHubService.Interfaces;
using NotNow.GitHubService.Services;

namespace NotNow.GitHubService.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGitHubService(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<GitHubSettings>(configuration.GetSection("GitHubSettings"));
        services.AddScoped<IGitHubService, Services.GitHubService>();

        return services;
    }

    public static IServiceCollection AddGitHubService(this IServiceCollection services, Action<GitHubSettings> configureOptions)
    {
        services.Configure(configureOptions);
        services.AddScoped<IGitHubService, Services.GitHubService>();

        return services;
    }
}