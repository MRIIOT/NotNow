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
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		_mainWindow = new Window(new TerminalPage());

#if WINDOWS
		Microsoft.Maui.Handlers.WindowHandler.Mapper.AppendToMapping("QuakeTerminal", (handler, view) =>
		{
			var nativeWindow = handler.PlatformView;
			nativeWindow.Activate();

			// Set up global hotkey for CTRL+~
			if (handler is Microsoft.Maui.Handlers.WindowHandler windowHandler)
			{
				RegisterHotKey(windowHandler);
			}
		});
#endif

		return _mainWindow;
	}

#if WINDOWS
	private void RegisterHotKey(Microsoft.Maui.Handlers.WindowHandler handler)
	{
		// Simple timer-based approach to check for hotkey
		// For production, you'd want to use proper Windows global hotkey registration
		var timer = new System.Threading.Timer(_ =>
		{
			// Simple check for CTRL key state (tilde detection simplified for now)
			var ctrlState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);
			if ((ctrlState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down)
			{
				MainThread.BeginInvokeOnMainThread(() => ToggleVisibility());
			}
		}, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(100));
	}

	private void ToggleVisibility()
	{
		if (_mainWindow == null) return;

		var handler = _mainWindow.Handler as Microsoft.Maui.Handlers.WindowHandler;
		if (handler == null) return;

		var handle = WinRT.Interop.WindowNative.GetWindowHandle(handler.PlatformView);
		var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(handle);
		var appWindow = AppWindow.GetFromWindowId(windowId);

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

		if (hide)
		{
			// Slide up
			for (int y = 0; y >= -targetHeight; y -= 20)
			{
				appWindow.MoveAndResize(new RectInt32 { X = 0, Y = y, Width = workAreaWidth, Height = targetHeight });
				await Task.Delay(10);
			}
			appWindow.Hide();
		}
		else
		{
			// Slide down
			for (int y = -targetHeight; y <= 0; y += 20)
			{
				appWindow.MoveAndResize(new RectInt32 { X = 0, Y = y, Width = workAreaWidth, Height = targetHeight });
				await Task.Delay(10);
			}
		}
	}
#endif
}