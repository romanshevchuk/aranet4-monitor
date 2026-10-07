using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Aranet4Monitor.Presentation.Views.Chrome;

public partial class StatusBarControl : System.Windows.Controls.UserControl
{
    public event RoutedEventHandler? CancelHistorySyncRequested;

    public event RoutedEventHandler? DeviceChipRequested;

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

    private void CancelHistorySync_Click(object sender, RoutedEventArgs e) =>
        CancelHistorySyncRequested?.Invoke(this, e);

    private void DeviceChip_Click(object sender, RoutedEventArgs e) =>
        DeviceChipRequested?.Invoke(this, e);
}
