using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;
using NotNow.Core.Extensions;
using NotNow.GitHubService.Extensions;
using Microsoft.Extensions.Configuration;

#if WINDOWS
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Windows.Graphics;
#endif

namespace NotNow.Quake;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
				fonts.AddFont("CascadiaMono.ttf", "CascadiaMono");
				fonts.AddFont("MaterialIcons-Regular.ttf", "MaterialIcons");
			})
			.ConfigureLifecycleEvents(events =>
			{
#if WINDOWS
				events.AddWindows(windows => windows
					.OnWindowCreated(window =>
					{
						var handle = WinRT.Interop.WindowNative.GetWindowHandle(window);
						var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(handle);
						var appWindow = AppWindow.GetFromWindowId(windowId);

						// Remove title bar and set always on top
						if (appWindow.Presenter is OverlappedPresenter presenter)
						{
							presenter.SetBorderAndTitleBar(false, false);
							presenter.IsResizable = true;
							presenter.IsAlwaysOnTop = true; // Keep window always on top
						}

						// Position at top of screen like Quake console
						var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
						var workAreaWidth = displayArea.WorkArea.Width;
						var workAreaHeight = displayArea.WorkArea.Height;

						// Take up top 60% of screen
						appWindow.MoveAndResize(new RectInt32
						{
							X = 0,
							Y = 0, // Start at top of screen
							Width = workAreaWidth,
							Height = (int)(workAreaHeight * 0.6)
						});
					}));
#endif
			});

		// Add configuration
		var config = new ConfigurationBuilder()
			.AddJsonFile("appsettings.json", optional: true)
			.Build();
		builder.Configuration.AddConfiguration(config);

		// Add services
		builder.Services.AddNotNowCore();
		builder.Services.AddGitHubService(config);

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
