using System.Windows;

namespace Aranet4Monitor.Presentation.Views.Chrome;

public partial class BannerHost : System.Windows.Controls.UserControl
{
    public event EventHandler<RoutedEventArgs>? StartListeningRequested;

    public event EventHandler<RoutedEventArgs>? BluetoothSettingsRequested;

    public event EventHandler<RoutedEventArgs>? RetrySyncRequested;

    public event EventHandler<RoutedEventArgs>? DismissSyncProblemRequested;

    public BannerHost()
    {
        InitializeComponent();
    }

    private void NoticeAction_Click(object sender, RoutedEventArgs e) =>
        StartListeningRequested?.Invoke(this, e);

    private void BluetoothSettings_Click(object sender, RoutedEventArgs e) =>
        BluetoothSettingsRequested?.Invoke(this, e);

    private void RetrySync_Click(object sender, RoutedEventArgs e) =>
        RetrySyncRequested?.Invoke(this, e);

    private void DismissSyncProblem_Click(object sender, RoutedEventArgs e) =>
        DismissSyncProblemRequested?.Invoke(this, e);
}
