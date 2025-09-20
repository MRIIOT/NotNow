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

						// Remove title bar
						if (appWindow.Presenter is OverlappedPresenter presenter)
						{
							presenter.SetBorderAndTitleBar(false, false);
							presenter.IsResizable = true;
						}

						// Position at top of screen like Quake console
						var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
						var workAreaWidth = displayArea.WorkArea.Width;
						var workAreaHeight = displayArea.WorkArea.Height;

						// Take up top 60% of screen
						appWindow.MoveAndResize(new RectInt32
						{
							X = 0,
							Y = -1, // Start just off screen for slide animation
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
