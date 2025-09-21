using Microsoft.UI.Xaml;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace NotNow.Quake.WinUI;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : MauiWinUIApplication
{
	/// <summary>
	/// Initializes the singleton application object.  This is the first line of authored code
	/// executed, and as such is the logical equivalent of main() or WinMain().
	/// </summary>
	public App()
	{
		try
		{
			Console.WriteLine("[Windows.App] Constructor starting...");
			System.Diagnostics.Debug.WriteLine("[Windows.App] Constructor starting...");
			
			this.InitializeComponent();
			
			Console.WriteLine("[Windows.App] InitializeComponent completed");
			System.Diagnostics.Debug.WriteLine("[Windows.App] InitializeComponent completed");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"[Windows.App] Constructor error: {ex}");
			System.Diagnostics.Debug.WriteLine($"[Windows.App] Constructor error: {ex}");
			throw;
		}
	}

	protected override MauiApp CreateMauiApp()
	{
		try
		{
			Console.WriteLine("[Windows.App] CreateMauiApp starting...");
			System.Diagnostics.Debug.WriteLine("[Windows.App] CreateMauiApp starting...");
			
			var app = MauiProgram.CreateMauiApp();
			
			Console.WriteLine("[Windows.App] CreateMauiApp completed");
			System.Diagnostics.Debug.WriteLine("[Windows.App] CreateMauiApp completed");
			
			return app;
		}
		catch (Exception ex)
		{
			Console.WriteLine($"[Windows.App] CreateMauiApp error: {ex}");
			System.Diagnostics.Debug.WriteLine($"[Windows.App] CreateMauiApp error: {ex}");
			throw;
		}
	}
}

