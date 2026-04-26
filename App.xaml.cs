using System.Windows;

namespace Agent1;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        var targetWindow = NativeMethods.GetForegroundWindow();
        base.OnStartup(e);

        var window = new MainWindow(targetWindow);
        MainWindow = window;
        window.Show();
    }
}
