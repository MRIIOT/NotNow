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
		var targetHeight = (int)(workAreaHeight * 0.6);
		int animationSteps = 15;
		int stepDelay = 15; // milliseconds

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