using Microsoft.Extensions.Configuration;
using NotNow.Quake.Views;

#if WINDOWS
using Microsoft.UI.Windowing;
using Windows.Graphics;
using Windows.UI.Core;
using Windows.System;
#endif

namespace NotNow.Quake;

public partial class App : Application
{
	private Window? _mainWindow;
	private bool _isVisible = true;
	private double _heightPercentage = 0.6; // Default value
	private int _animationSpeed = 30; // Default value in milliseconds

	public App()
	{
		try
		{
			Console.WriteLine("[App] Constructor starting...");
			System.Diagnostics.Debug.WriteLine("[App] Constructor starting...");
			
			InitializeComponent();
			
			Console.WriteLine("[App] InitializeComponent completed");
			System.Diagnostics.Debug.WriteLine("[App] InitializeComponent completed");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"[App] Constructor error: {ex}");
			System.Diagnostics.Debug.WriteLine($"[App] Constructor error: {ex}");
			throw;
		}
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		try
		{
			Console.WriteLine("[App] CreateWindow starting...");
			System.Diagnostics.Debug.WriteLine("[App] CreateWindow starting...");

			// Load window settings from appsettings.json
			LoadWindowSettings();

			_mainWindow = new Window(new TerminalPage());
			Console.WriteLine("[App] Main window created with TerminalPage");

#if WINDOWS
			Console.WriteLine("[App] Configuring Windows-specific handlers...");
			Microsoft.Maui.Handlers.WindowHandler.Mapper.AppendToMapping("QuakeTerminal", (handler, view) =>
			{
				Console.WriteLine("[App] WindowHandler mapping executing...");
				try
				{
					var nativeWindow = handler.PlatformView;
					nativeWindow.Activate();
					Console.WriteLine("[App] Native window activated");

					// Set up global hotkey for CTRL+~
					if (handler is Microsoft.Maui.Handlers.WindowHandler windowHandler)
					{
						RegisterHotKey(windowHandler);
						Console.WriteLine("[App] Hotkey registered");
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine($"[App] WindowHandler mapping error: {ex.Message}");
					System.Diagnostics.Debug.WriteLine($"[App] WindowHandler mapping error: {ex}");
				}
			});
#endif

			Console.WriteLine("[App] CreateWindow completed successfully");
			return _mainWindow;
		}
		catch (Exception ex)
		{
			Console.WriteLine($"[App] CreateWindow error: {ex}");
			System.Diagnostics.Debug.WriteLine($"[App] CreateWindow error: {ex}");
			throw;
		}
	}

	private void LoadWindowSettings()
	{
		try
		{
			// Build configuration with environment-specific overrides
			var basePath = AppDomain.CurrentDomain.BaseDirectory;
			var configBuilder = new ConfigurationBuilder()
				.SetBasePath(basePath)
				.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);

			// Add environment-specific configuration
#if DEBUG
			configBuilder.AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false);
			Console.WriteLine("[App] Loading configuration with appsettings.Development.json (DEBUG mode)");
#else
			Console.WriteLine("[App] Loading configuration from appsettings.json (RELEASE mode)");
#endif

			var configuration = configBuilder.Build();

			// Load Window settings
			var windowSection = configuration.GetSection("Window");
			if (windowSection.Exists())
			{
				var heightPercentage = windowSection.GetValue<double?>("HeightPercentage");
				if (heightPercentage.HasValue)
				{
					_heightPercentage = heightPercentage.Value;
					Console.WriteLine($"[App] Loaded HeightPercentage: {_heightPercentage}");
				}

				var animationSpeed = windowSection.GetValue<int?>("AnimationSpeed");
				if (animationSpeed.HasValue)
				{
					_animationSpeed = animationSpeed.Value;
					Console.WriteLine($"[App] Loaded AnimationSpeed: {_animationSpeed}ms");
				}
			}
			else
			{
				Console.WriteLine("[App] Window configuration section not found, using default settings");
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine($"[App] Error loading window settings: {ex.Message}");
			// Continue with default values
		}
	}

#if WINDOWS
	private void RegisterHotKey(Microsoft.Maui.Handlers.WindowHandler handler)
	{
		// Start a background timer to check for CTRL+~ hotkey combination
		// This is a simplified approach - for production use RegisterHotKey Win32 API
		var timer = Application.Current?.Dispatcher.CreateTimer();
		if (timer != null)
		{
			timer.Interval = TimeSpan.FromMilliseconds(100);
			timer.Tick += (s, e) =>
			{
				try
				{
					// Check if CTRL and Tilde (~) keys are pressed
					var ctrlState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);
					var tildeState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread((VirtualKey)192); // OEM3 is tilde key

					if ((ctrlState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down &&
						(tildeState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down)
					{
						// Prevent multiple triggers
						timer.Stop();
						ToggleVisibility();
						Task.Delay(500).ContinueWith(_ => timer.Start());
					}
				}
				catch
				{
					// Silently ignore any errors in hotkey detection
				}
			};
			timer.Start();
		}
	}

	private void ToggleVisibility()
	{
		if (_mainWindow == null) return;

		var handler = _mainWindow.Handler as Microsoft.Maui.Handlers.WindowHandler;
		if (handler == null) return;

		var handle = WinRT.Interop.WindowNative.GetWindowHandle(handler.PlatformView);
		var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(handle);
		var appWindow = AppWindow.GetFromWindowId(windowId);

		// Ensure window stays on top
		if (appWindow.Presenter is OverlappedPresenter presenter)
		{
			presenter.IsAlwaysOnTop = true;
		}

		if (_isVisible)
		{
			// Slide up animation (hide)
			AnimateWindow(appWindow, true);
			_isVisible = false;
		}
		else
		{
			// Slide down animation (show)
			appWindow.Show();
			AnimateWindow(appWindow, false);
			_isVisible = true;
		}
	}

	private async void AnimateWindow(AppWindow appWindow, bool hide)
	{
		var displayArea = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary);
		var workAreaWidth = displayArea.WorkArea.Width;
		var workAreaHeight = displayArea.WorkArea.Height;
		var targetHeight = (int)(workAreaHeight * _heightPercentage);
		int animationSteps = Math.Max(1, _animationSpeed / 2); // Calculate steps based on speed
		int stepDelay = Math.Max(1, _animationSpeed / animationSteps); // Calculate delay per step

		if (hide)
		{
			// Slide up and hide
			int stepSize = targetHeight / animationSteps;
			for (int i = 0; i <= animationSteps; i++)
			{
				int y = -(i * stepSize);
				appWindow.MoveAndResize(new RectInt32 { X = 0, Y = y, Width = workAreaWidth, Height = targetHeight });
				if (i < animationSteps) await Task.Delay(stepDelay);
			}
			appWindow.Hide();
		}
		else
		{
			// Show and slide down
			appWindow.Show();
			int stepSize = targetHeight / animationSteps;
			for (int i = animationSteps; i >= 0; i--)
			{
				int y = -(i * stepSize);
				appWindow.MoveAndResize(new RectInt32 { X = 0, Y = y, Width = workAreaWidth, Height = targetHeight });
				if (i > 0) await Task.Delay(stepDelay);
			}
		}
	}
#endif
}