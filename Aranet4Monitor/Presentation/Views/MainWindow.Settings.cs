using System.Globalization;
using System.Windows;
using Aranet4Monitor.Models;

namespace Aranet4Monitor;

public partial class MainWindow
{
    private void DeviceChip_Click(object sender, RoutedEventArgs e)
    {
        if (DevicePopup.IsOpen) { DevicePopup.IsOpen = false; return; }
        // The same click that dismissed the popup (it closes on any outside press) must not reopen it.
        if ((DateTime.UtcNow - _devicePopupClosedAt).TotalMilliseconds < 250) return;
        DevicePopup.IsOpen = true;
    }

    private void DevicePopup_Closed(object? sender, EventArgs e) => _devicePopupClosedAt = DateTime.UtcNow;

    private void MoreButton_Click(object sender, RoutedEventArgs e)
    {
        MoreMenu.PlacementTarget = MoreButton;
        MoreMenu.IsOpen = true;
    }

    private void TemperatureUnit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.MenuItem { Tag: string tag }
            || !Enum.TryParse<TemperatureUnit>(tag, ignoreCase: true, out var temperatureUnit)) return;

        _preferences.TemperatureDisplayUnit = temperatureUnit;
        _preferences.Save();
        CelsiusUnitMenuItem.IsChecked = temperatureUnit == TemperatureUnit.Celsius;
        FahrenheitUnitMenuItem.IsChecked = temperatureUnit == TemperatureUnit.Fahrenheit;
        HistoryChart.TemperatureDisplayUnit = temperatureUnit;

        foreach (var device in Devices)
        {
            if (device.TemperatureCelsius is { } celsius)
                device.Temperature = Metrics.FormatWithUnit((double)celsius, MetricKind.Temperature, temperatureUnit);
        }

        if (DevicesList.SelectedItem is Aranet4Device selected) ShowDetails(selected);
        else RefreshChart();
    }

    private void RangeButton_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton { Tag: string tag }) return;
        var hours = int.Parse(tag, CultureInfo.InvariantCulture);
        _range = hours == 0 ? null : TimeSpan.FromHours(hours);
        if (HistoryChart is null) return; // Checked fires once while the XAML is still being loaded
        RefreshChart();
    }
}