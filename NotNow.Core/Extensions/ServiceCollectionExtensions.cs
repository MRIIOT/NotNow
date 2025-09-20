using Microsoft.Extensions.DependencyInjection;
using NotNow.Core.Commands.Execution;
using NotNow.Core.Commands.Framework;
using NotNow.Core.Commands.Modules;
using NotNow.Core.Commands.Modules.Core;
using NotNow.Core.Commands.Modules.Subtasks;
using NotNow.Core.Commands.Modules.TimeTracking;
using NotNow.Core.Commands.Parser;
using NotNow.Core.Commands.Registry;
using NotNow.Core.Services;
using System.Reflection;

namespace NotNow.Core.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNotNowCore(
        this IServiceCollection services,
        Action<NotNowCoreOptions>? configure = null)
    {
        var options = new NotNowCoreOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);

        // Register command infrastructure
        services.AddSingleton<ICommandRegistry, CommandRegistry>();
        services.AddScoped<ICommandParser, ModularCommandParser>();
        services.AddScoped<ICommandExecutor, CommandExecutor>();
        services.AddSingleton<ICommandInitializationService, CommandInitializationService>();
        services.AddScoped<ICommandPostingService, CommandPostingService>();

        // Register core modules
        if (options.RegisterCoreModules)
        {
            services.AddCommandModule<CoreModule>();
            services.AddCommandModule<TimeTrackingModule>();
            services.AddCommandModule<SubtaskModule>();
        }

        // Register custom modules
        foreach (var moduleType in options.CustomModules)
        {
            services.AddCommandModule(moduleType);
        }

        // Register all command handlers
        RegisterCommandHandlers(services, options);

        // Module registration will happen at runtime

        return services;
    }

    public static IServiceCollection AddCommandModule<TModule>(this IServiceCollection services)
        where TModule : class, ICommandModule, new()
    {
        services.AddSingleton<ICommandModule, TModule>();
        return services;
    }

    public static IServiceCollection AddCommandModule(this IServiceCollection services, Type moduleType)
    {
        if (!typeof(ICommandModule).IsAssignableFrom(moduleType))
            throw new ArgumentException($"Type {moduleType.Name} does not implement ICommandModule");

        services.AddSingleton(typeof(ICommandModule), moduleType);
        return services;
    }

    private static void RegisterCommandHandlers(IServiceCollection services, NotNowCoreOptions options)
    {
        var assemblies = new List<Assembly>
        {
            typeof(ServiceCollectionExtensions).Assembly // NotNow.Core assembly
        };

        assemblies.AddRange(options.ScanAssemblies);

        foreach (var assembly in assemblies.Distinct())
        {
            var handlerTypes = assembly.GetTypes()
                .Where(t => !t.IsAbstract &&
                           !t.IsInterface &&
                           typeof(ICommandHandler).IsAssignableFrom(t));

            foreach (var handlerType in handlerTypes)
            {
                services.AddScoped(handlerType);
            }
        }
    }
}

public class NotNowCoreOptions
{
    public bool RegisterCoreModules { get; set; } = true;
    public List<Type> CustomModules { get; set; } = new();
    public List<Assembly> ScanAssemblies { get; set; } = new();
    public bool EnableValidation { get; set; } = true;
    public bool StrictMode { get; set; } = false;
}