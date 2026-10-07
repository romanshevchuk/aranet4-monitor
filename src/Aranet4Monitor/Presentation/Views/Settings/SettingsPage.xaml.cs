using System.Windows;
using System.Windows.Controls;
using Aranet4Monitor.Presentation.ViewModels;

namespace Aranet4Monitor.Presentation.Views;

public partial class SettingsPage : System.Windows.Controls.UserControl
{
    public event Action? DevicesForgotten;

    public LiveViewModel? LiveViewModel { get; set; }

    public SettingsPage()
    {
        InitializeComponent();
    }

    public void ScrollToTop() => SettingsScrollViewer.ScrollToTop();

    public void Activate() => ScrollToTop();

    public void ConfirmForgetDetectedDevices()
    {
        var live = LiveViewModel;
        if (live is null || live.Devices.Count == 0)
        {
            return;
        }

        var answer = MessageBox.Show(
            Window.GetWindow(this),
            "Forget all detected sensors? Saved history and settings will be kept.",
            "Forget detected devices",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        live.ForgetDetectedDevices();
        DevicesForgotten?.Invoke();
    }
}
