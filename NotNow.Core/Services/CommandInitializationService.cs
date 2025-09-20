using Microsoft.Extensions.DependencyInjection;
using NotNow.Core.Commands.Framework;
using NotNow.Core.Commands.Registry;

namespace NotNow.Core.Services;

public interface ICommandInitializationService
{
    void Initialize();
}

public class CommandInitializationService : ICommandInitializationService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ICommandRegistry _registry;
    private bool _initialized = false;

    public CommandInitializationService(IServiceProvider serviceProvider, ICommandRegistry registry)
    {
        _serviceProvider = serviceProvider;
        _registry = registry;
    }

    public void Initialize()
    {
        if (_initialized)
            return;

        var modules = _serviceProvider.GetServices<ICommandModule>();
        foreach (var module in modules)
        {
            _registry.RegisterModule(module);
        }

        _initialized = true;
    }
}