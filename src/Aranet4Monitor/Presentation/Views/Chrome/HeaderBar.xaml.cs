using System.Windows;
using System.Windows.Controls;
using Aranet4Monitor.Presentation;

namespace Aranet4Monitor.Presentation.Views.Chrome;

public partial class HeaderBar : System.Windows.Controls.UserControl
{
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

    public void SetDeviceName(string name) => ChipNameText.Text = name;

    public Button LiveNavigationButton => LiveNavigationControl;

    public Button HistoryNavigationButton => HistoryNavigationControl;

    public Button SettingsNavigationButton => SettingsNavigationControl;

    public void SelectPage(AppPage page)
    {
        LiveNavigationButton.Style = (Style)FindResource(page == AppPage.Live
            ? "HeaderNavigationSelectedButton"
            : "HeaderNavigationButton");
        HistoryNavigationButton.Style = (Style)FindResource(page == AppPage.History
            ? "HeaderNavigationSelectedButton"
            : "HeaderNavigationButton");
        SettingsNavigationButton.Style = (Style)FindResource(page == AppPage.Settings
            ? "HeaderNavigationSelectedButton"
            : "HeaderNavigationButton");
    }

    private void DeviceChip_Click(object sender, RoutedEventArgs e) =>
        DeviceChipRequested?.Invoke(this, e);

    private void MinimizeWindow_Click(object sender, RoutedEventArgs e) =>
        MinimizeRequested?.Invoke(this, e);

    private void ToggleWindowState_Click(object sender, RoutedEventArgs e) =>
        ToggleWindowStateRequested?.Invoke(this, e);

    private void CloseWindow_Click(object sender, RoutedEventArgs e) =>
        CloseRequested?.Invoke(this, e);
}
