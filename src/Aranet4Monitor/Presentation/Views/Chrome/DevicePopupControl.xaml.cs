using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Aranet4Monitor.Presentation.Controls;

namespace Aranet4Monitor.Presentation.Views.Chrome;

public partial class DevicePopupControl : System.Windows.Controls.UserControl
{
    public static readonly DependencyProperty PlacementTargetProperty = DependencyProperty.Register(
        nameof(PlacementTarget),
        typeof(UIElement),
        typeof(DevicePopupControl),
        new PropertyMetadata(null, OnPlacementTargetChanged));

    public event EventHandler? PopupClosed;

    public event SelectionChangedEventHandler? DevicesSelectionChanged;

    public event RoutedEventHandler? CopyAddressRequested;

    public event RoutedEventHandler? SyncHistoryRequested;

    public event RoutedEventHandler? CopyPacketRequested;

    public event RoutedEventHandler? ScanDevicesRequested;

    public event RoutedEventHandler? ManageDevicesRequested;

    public event System.Windows.Input.KeyEventHandler? PopupPreviewKeyDown;

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

    private static void OnPlacementTargetChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is DevicePopupControl control)
        {
            control.PopupControl.PlacementTarget = args.NewValue as UIElement;
        }
    }

    private void Popup_Closed(object? sender, EventArgs e) => PopupClosed?.Invoke(this, e);

    private void DevicesList_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        DevicesSelectionChanged?.Invoke(sender, e);

    private void CopyAddress_Click(object sender, RoutedEventArgs e) => CopyAddressRequested?.Invoke(sender, e);

    private void SyncHistory_Click(object sender, RoutedEventArgs e) => SyncHistoryRequested?.Invoke(sender, e);

    private void CopyPacket_Click(object sender, RoutedEventArgs e) => CopyPacketRequested?.Invoke(sender, e);

    private void ScanDevices_Click(object sender, RoutedEventArgs e) => ScanDevicesRequested?.Invoke(sender, e);

    private void ManageDevices_Click(object sender, RoutedEventArgs e) => ManageDevicesRequested?.Invoke(sender, e);

    private void Popover_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e) => PopupPreviewKeyDown?.Invoke(sender, e);
}
