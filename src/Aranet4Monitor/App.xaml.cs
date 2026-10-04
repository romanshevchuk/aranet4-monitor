namespace Aranet4Monitor;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        var mainWindow = new MainWindow(new AppServices());
        MainWindow = mainWindow;
        mainWindow.Show();
    }
}
