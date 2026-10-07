using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Aranet4Monitor.Models;
using Aranet4Monitor.Presentation;
using Aranet4Monitor.Presentation.Controls;
using Aranet4Monitor.Presentation.ViewModels;

namespace Aranet4Monitor.Presentation.Views.Chrome;

public partial class DevicePopupControl : System.Windows.Controls.UserControl
{
    private DateTime popupClosedAt;

    public static readonly DependencyProperty PlacementTargetProperty = DependencyProperty.Register(
        nameof(PlacementTarget),
        typeof(UIElement),
        typeof(DevicePopupControl),
        new PropertyMetadata(null, OnPlacementTargetChanged));

    public event SelectionChangedEventHandler? DevicesSelectionChanged;

    public event RoutedEventHandler? SyncHistoryRequested;

    public event RoutedEventHandler? ScanDevicesRequested;

    public event RoutedEventHandler? ManageDevicesRequested;

    public DevicePopupControl()
    {
        InitializeComponent();
        PopupControl.DataContext = DataContext;
        DataContextChanged += (_, args) => PopupControl.DataContext = args.NewValue;
    }

    public UIElement? PlacementTarget
    {
        get => (UIElement?)GetValue(PlacementTargetProperty);
        set => SetValue(PlacementTargetProperty, value);
    }

    public Popup DevicePopup => PopupControl;

    public void TogglePopup()
    {
        if (DevicePopup.IsOpen)
        {
            DevicePopup.IsOpen = false;
            return;
        }

        // An outside click closes the popup before reaching the chip; don't let that same click reopen it.
        if ((DateTime.UtcNow - popupClosedAt).TotalMilliseconds < 250)
        {
            return;
        }

        DevicePopup.IsOpen = true;
    }

    public void ShowTelemetry(Aranet4Device device)
    {
        BatteryText.Text = device.Battery;
        BatteryBar.Value = device.BatteryValue;
        BatteryBar.Foreground = ThemeService.GetBrush(device.BatteryValue switch
        {
            <= 15 => "Danger",
            <= 35 => "Co2Fair",
            _ => "Co2Good",
        });
        DeviceMeasurementAgeText.Text = device.MeasurementAge;
        DeviceIntervalText.Text = device.MeasurementInterval;
        AdvertisementText.Text = device.LastAdvertisement;
        ScanResponseText.Text = device.LastScanResponse;
    }

    public void ClearTelemetry()
    {
        BatteryText.Text = "—";
        BatteryBar.Value = 0;
        DeviceMeasurementAgeText.Text = "—";
        DeviceIntervalText.Text = "—";
        AdvertisementText.Clear();
        ScanResponseText.Clear();
    }

    public void OpenDiagnostics()
    {
        DiagnosticsExpander.IsExpanded = true;
        DevicePopup.IsOpen = true;
    }

    public TextBlock DevicesTitleText => DevicesTitleTextControl;

    public TextBlock DevicesCountText => DevicesCountTextControl;

    public System.Windows.Controls.ListBox DevicesList => DevicesListControl;

    public StackPanel DeviceDetailsPanel => DeviceDetailsPanelControl;

    public System.Windows.Shapes.Ellipse DeviceStatusDot => DeviceStatusDotControl;

    public TextBlock DeviceStatusText => DeviceStatusTextControl;

    public TextBlock DeviceLastMeasurementText => DeviceLastMeasurementTextControl;

    public TextBlock DeviceMeasurementAgeText => DeviceMeasurementAgeTextControl;

    public TextBlock BatteryText => BatteryTextControl;

    public System.Windows.Controls.ProgressBar BatteryBar => BatteryBarControl;

    public TextBlock DeviceIntervalText => DeviceIntervalTextControl;

    public TextBlock DeviceAddressText => DeviceAddressTextControl;

    public TextBlock DeviceLastSyncedText => DeviceLastSyncedTextControl;

    public TextBlock EmptyHintText => EmptyHintTextControl;

    public Expander DiagnosticsExpander => DiagnosticsExpanderControl;

    public TextBlock PacketCountText => PacketCountTextControl;

    public System.Windows.Controls.TextBox AdvertisementText => AdvertisementTextControl;

    public System.Windows.Controls.TextBox ScanResponseText => ScanResponseTextControl;

    public void RefreshDevices(
        IReadOnlyCollection<Aranet4Device> devices,
        Aranet4Device? selectedDevice,
        DateTime lastReading,
        bool isStale,
        DateTime? lastSync)
    {
        var count = devices.Count;
        DevicesCountText.Text = count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        DevicesTitleText.Text = count > 1 ? "YOUR SENSORS" : "SENSORS";
        DevicesList.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyHintText.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
        PacketCountText.Text = selectedDevice is null
            ? "0 packets received"
            : $"{selectedDevice.Packets:N0} packets received";

        if (selectedDevice is null)
        {
            DeviceDetailsPanel.Visibility = Visibility.Collapsed;
            return;
        }

        DeviceDetailsPanel.Visibility = Visibility.Visible;
        DeviceStatusText.Text = lastReading == default
            ? "Waiting for live readings"
            : isStale
                ? "No recent readings"
                : "Receiving live data";
        DeviceStatusText.Foreground = lastReading == default
            ? ThemeService.GetBrush("TextSecondary")
            : isStale
                ? ThemeService.GetBrush("Warning")
                : ThemeService.GetBrush("Positive");
        DeviceStatusDot.Fill = lastReading == default
            ? ThemeService.GetBrush("Disabled")
            : isStale
                ? ThemeService.GetBrush("Co2Fair")
                : ThemeService.GetBrush("Co2Good");
        DeviceLastMeasurementText.Text = lastReading == default
            ? "No measurement yet"
            : lastReading.ToLocalTime().ToString("g", System.Globalization.CultureInfo.CurrentCulture);
        DeviceAddressText.Text = selectedDevice.Address;
        DeviceLastSyncedText.Text = lastSync is { } syncedThrough
            ? syncedThrough.ToLocalTime().ToString("g", System.Globalization.CultureInfo.CurrentCulture)
            : "Not synced yet";
        BatteryText.Foreground = selectedDevice.BatteryValue switch
        {
            <= 15 => ThemeService.GetBrush("Danger"),
            <= 35 => ThemeService.GetBrush("Warning"),
            _ => ThemeService.GetBrush("TextPrimary"),
        };
    }

    public void RefreshFromViewModels(LiveViewModel live, HistoryViewModel history, DateTime now)
    {
        var device = live.SelectedDevice;
        var lastReading = device is null ? default : live.GetLastReadingTime(device);
        var isStale = device is not null && live.IsStale(device, now);
        RefreshDevices(live.Devices, device, lastReading, isStale, history.GetSyncCursor(device));
    }

    private static void OnPlacementTargetChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is DevicePopupControl control)
        {
            control.PopupControl.PlacementTarget = args.NewValue as UIElement;
        }
    }

    private void Popup_Closed(object? sender, EventArgs e)
    {
        popupClosedAt = DateTime.UtcNow;
        if (PlacementTarget is Control target)
        {
            target.Focus();
        }
    }

    private void DevicesList_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        DevicesSelectionChanged?.Invoke(sender, e);

    private void CopyAddress_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(DeviceAddressText.Text))
        {
            Clipboard.SetText(DeviceAddressText.Text);
        }
    }

    private void SyncHistory_Click(object sender, RoutedEventArgs e) => SyncHistoryRequested?.Invoke(sender, e);

    private void CopyPacket_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string packetKind })
        {
            return;
        }

        var packet = packetKind == "adv" ? AdvertisementText.Text : ScanResponseText.Text;
        if (string.IsNullOrWhiteSpace(packet))
        {
            return;
        }

        try
        {
            Clipboard.SetText(packet);
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            // The clipboard can be temporarily busy.
        }
    }

    private void ScanDevices_Click(object sender, RoutedEventArgs e)
    {
        DevicePopup.IsOpen = false;
        ScanDevicesRequested?.Invoke(this, e);
    }

    private void ManageDevices_Click(object sender, RoutedEventArgs e)
    {
        DevicePopup.IsOpen = false;
        ManageDevicesRequested?.Invoke(this, e);
    }

    private void Popover_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        DevicePopup.IsOpen = false;
        if (PlacementTarget is Control target)
        {
            target.Focus();
        }

        e.Handled = true;
    }
}
