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
		try
		{
			Console.WriteLine("[MauiProgram] Starting CreateMauiApp...");
			System.Diagnostics.Debug.WriteLine("[MauiProgram] Starting CreateMauiApp...");
			
			var builder = MauiApp.CreateBuilder();
			Console.WriteLine("[MauiProgram] MauiApp.CreateBuilder() completed");
			
			builder
				.UseMauiApp<App>()
				.ConfigureFonts(fonts =>
				{
					Console.WriteLine("[MauiProgram] Configuring fonts...");
					fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
					fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
					fonts.AddFont("CascadiaMono.ttf", "CascadiaMono");
					fonts.AddFont("MaterialIcons-Regular.ttf", "MaterialIcons");
					Console.WriteLine("[MauiProgram] Fonts configured");
				})
				.ConfigureLifecycleEvents(events =>
				{
					Console.WriteLine("[MauiProgram] Configuring lifecycle events...");
#if WINDOWS
					events.AddWindows(windows => windows
						.OnWindowCreated(window =>
						{
							Console.WriteLine("[MauiProgram] Window created, configuring window properties...");
							try
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
									Console.WriteLine("[MauiProgram] Window presenter configured");
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
								Console.WriteLine($"[MauiProgram] Window positioned: {workAreaWidth}x{(int)(workAreaHeight * 0.6)}");
							}
							catch (Exception ex)
							{
								Console.WriteLine($"[MauiProgram] Error configuring window: {ex.Message}");
								System.Diagnostics.Debug.WriteLine($"[MauiProgram] Error configuring window: {ex}");
							}
						}));
#endif
				});

			// Add configuration
			Console.WriteLine("[MauiProgram] Loading configuration...");
			try
			{
				var configBuilder = new ConfigurationBuilder()
					.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);

#if DEBUG
				// In Debug mode, also load Development configuration which will override base settings
				configBuilder.AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false);
				Console.WriteLine("[MauiProgram] Adding appsettings.Development.json (DEBUG mode)");
#endif

				var config = configBuilder.Build();
				builder.Configuration.AddConfiguration(config);
				Console.WriteLine("[MauiProgram] Configuration loaded");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[MauiProgram] Error loading configuration: {ex.Message}");
				System.Diagnostics.Debug.WriteLine($"[MauiProgram] Error loading configuration: {ex}");
			}

			// Add services
			Console.WriteLine("[MauiProgram] Adding services...");
			try
			{
				builder.Services.AddNotNowCore();
				Console.WriteLine("[MauiProgram] NotNowCore added");

				builder.Services.AddGitHubService(builder.Configuration);
				Console.WriteLine("[MauiProgram] GitHubService added");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[MauiProgram] Error adding services: {ex.Message}");
				System.Diagnostics.Debug.WriteLine($"[MauiProgram] Error adding services: {ex}");
				throw;
			}

#if DEBUG
			builder.Logging.AddDebug();
			builder.Logging.SetMinimumLevel(LogLevel.Trace);
			Console.WriteLine("[MauiProgram] Debug logging configured");
#endif

			Console.WriteLine("[MauiProgram] Building MauiApp...");
			var app = builder.Build();
			Console.WriteLine("[MauiProgram] MauiApp built successfully");
			return app;
		}
		catch (Exception ex)
		{
			Console.WriteLine($"[MauiProgram] FATAL ERROR: {ex}");
			System.Diagnostics.Debug.WriteLine($"[MauiProgram] FATAL ERROR: {ex}");
			throw;
		}
	}
}
