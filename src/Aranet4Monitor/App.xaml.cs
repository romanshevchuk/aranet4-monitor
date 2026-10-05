using Aranet4Monitor.Presentation;

namespace Aranet4Monitor;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new AppServices();
        ThemeService.Initialize(services.Preferences.Theme);

        var mainWindow = new MainWindow(services);
        MainWindow = mainWindow;
        mainWindow.Show();
    }
}
