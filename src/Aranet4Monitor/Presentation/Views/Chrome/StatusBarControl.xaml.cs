using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Aranet4Monitor.Models;
using Aranet4Monitor.Presentation;
using Aranet4Monitor.Presentation.ViewModels;

namespace Aranet4Monitor.Presentation.Views.Chrome;

public partial class StatusBarControl : System.Windows.Controls.UserControl
{
    public event RoutedEventHandler? DeviceChipRequested;

    public HistoryViewModel? HistoryViewModel { get; set; }

    public StatusBarControl()
    {
        InitializeComponent();
    }

    public System.Windows.Controls.Button CancelHistorySyncButton => CancelHistorySyncControl;

    public System.Windows.Controls.Button FooterBatteryButton => FooterBatteryControl;

    public System.Windows.Controls.ProgressBar FooterBatteryBar => FooterBatteryProgress;

    public TextBlock FooterBatteryText => FooterBatteryLabel;

    public TextBlock RssiText => RssiLabel;

    public Aranet4Monitor.Presentation.Controls.SignalBars DetailSignal => DetailSignalControl;

    public void ShowDeviceTelemetry(Aranet4Device device)
    {
        FooterBatteryText.Text = device.Battery;
        FooterBatteryText.Foreground = ThemeService.GetBrush(device.BatteryValue switch
        {
            <= 15 => "Danger",
            <= 35 => "Warning",
            _ => "TextPrimary",
        });
        FooterBatteryBar.Value = device.BatteryValue;
        FooterBatteryBar.Foreground = ThemeService.GetBrush(device.BatteryValue switch
        {
            <= 15 => "Danger",
            <= 35 => "Co2Fair",
            _ => "Co2Good",
        });
        RssiText.Text = device.Packets == 0 ? "—" : $"{device.Rssi} dBm";
        DetailSignal.Bars = device.SignalBars;
    }

    public void ClearDeviceTelemetry()
    {
        FooterBatteryText.Text = "—";
        FooterBatteryBar.Value = 0;
        RssiText.Text = "—";
        DetailSignal.Bars = 0;
    }

    public void SetHistorySyncState(bool isSyncing)
    {
        CancelHistorySyncButton.Content = "Cancel";
        CancelHistorySyncButton.IsEnabled = isSyncing;
        CancelHistorySyncButton.Visibility = isSyncing ? Visibility.Visible : Visibility.Collapsed;
    }

    private void CancelHistorySync_Click(object sender, RoutedEventArgs e)
    {
        CancelHistorySyncButton.IsEnabled = false;
        CancelHistorySyncButton.Content = "Cancelling…";
        HistoryViewModel?.CancelSync();
    }

    private void DeviceChip_Click(object sender, RoutedEventArgs e) =>
        DeviceChipRequested?.Invoke(this, e);
}
