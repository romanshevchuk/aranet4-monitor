using System.Windows;
using System.Windows.Controls;

namespace Aranet4Monitor.Presentation.Views.Chrome;

public partial class HeaderBar : System.Windows.Controls.UserControl
{
    public event EventHandler<RoutedEventArgs>? LiveNavigationRequested;

    public event EventHandler<RoutedEventArgs>? HistoryNavigationRequested;

    public event EventHandler<RoutedEventArgs>? SettingsNavigationRequested;

    public event EventHandler<RoutedEventArgs>? DeviceChipRequested;

    public event EventHandler<RoutedEventArgs>? MinimizeRequested;

    public event EventHandler<RoutedEventArgs>? ToggleWindowStateRequested;

    public event EventHandler<RoutedEventArgs>? CloseRequested;

    public HeaderBar()
    {
        InitializeComponent();
    }

    public Button DeviceChipButton => DeviceChipControl;

    public TextBlock ChipNameText => ChipNameControl;

    public Button LiveNavigationButton => LiveNavigationControl;

    public Button HistoryNavigationButton => HistoryNavigationControl;

    public Button SettingsNavigationButton => SettingsNavigationControl;

    private void LiveNavigation_Click(object sender, RoutedEventArgs e) =>
        LiveNavigationRequested?.Invoke(this, e);

    private void HistoryNavigation_Click(object sender, RoutedEventArgs e) =>
        HistoryNavigationRequested?.Invoke(this, e);

    private void SettingsNavigation_Click(object sender, RoutedEventArgs e) =>
        SettingsNavigationRequested?.Invoke(this, e);

    private void DeviceChip_Click(object sender, RoutedEventArgs e) =>
        DeviceChipRequested?.Invoke(this, e);

    private void MinimizeWindow_Click(object sender, RoutedEventArgs e) =>
        MinimizeRequested?.Invoke(this, e);

    private void ToggleWindowState_Click(object sender, RoutedEventArgs e) =>
        ToggleWindowStateRequested?.Invoke(this, e);

    private void CloseWindow_Click(object sender, RoutedEventArgs e) =>
        CloseRequested?.Invoke(this, e);
}
