using System.Windows;
using System.Linq;

namespace Mapsui48.Host;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Prevent WPF from shutting down immediately since we don't show the window until Attached
        this.ShutdownMode = ShutdownMode.OnExplicitShutdown;

        string pipeName = "Mapsui48_TestPipe"; // default for debugging
        var args = e.Args;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--pipe-name" && i + 1 < args.Length)
            {
                pipeName = args[i + 1];
                break;
            }
        }

        var mainWindow = new MainWindow(pipeName);
        
        // Create the window handle but DO NOT show the window yet,
        // to prevent it from flashing/floating on the desktop before reparenting.
        var helper = new System.Windows.Interop.WindowInteropHelper(mainWindow);
        helper.EnsureHandle();
    }
}
