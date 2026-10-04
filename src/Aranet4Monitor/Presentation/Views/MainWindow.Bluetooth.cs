using System.Globalization;
using System.Windows;
using Aranet4Monitor.Abstractions;
using Aranet4Monitor.Application.Monitoring;

namespace Aranet4Monitor;

public partial class MainWindow
{
    private readonly Dictionary<ulong, Aranet4Device> devicesByAddress = new();

    private void StartListening()
    {
        if (sensorSource.IsActive)
        {
            return;
        }

        listenerHasBeenStarted = true;
        try
        {
            sensorSource.Start();
            ListenMenuItem.IsChecked = true;
            SetStatus("Listening for Aranet4 beacon packets…", StatusKind.Listening);
            EmptyHintText.Text = "Looking for your Aranet4… Make sure Smart Home Integration is enabled in the Aranet Home app.";
        }
        catch (Exception ex)
        {
            sensorSource.Stop();
            ListenMenuItem.IsChecked = false;
            if (ex is UnauthorizedAccessException
                || ex.Message.Contains("Bluetooth", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("radio", StringComparison.OrdinalIgnoreCase))
            {
                SetStatus("Bluetooth unavailable", StatusKind.BluetoothUnavailable, ex.ToString());
            }
            else
            {
                SetStatus($"Could not start: {ex.Message}", StatusKind.Error, ex.ToString());
            }
        }
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        if (Dashboard.Devices.Count == 0)
        {
            return;
        }

        var answer = MessageBox.Show(
            this,
            "Forget all detected sensors? Saved history and settings will be kept.",
            "Forget detected devices",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        devicesByAddress.Clear();
        sensorSource.ClearKnownAddresses();
        sensorMonitor.ClearSensorTracking();
        Dashboard.Devices.Clear();
        Dashboard.SelectedDevice = null;
        ClearDetails();
    }

    private void StopWatching()
    {
        if (!sensorSource.IsActive)
        {
            return;
        }

        sensorSource.Stop();
        ListenMenuItem.IsChecked = false;
        SetStatus("Stopped", StatusKind.Idle);
    }

    private void SensorSource_Stopped(object? sender, SensorSourceStoppedEventArgs args) => Dispatcher.InvokeAsync(() =>
    {
        ListenMenuItem.IsChecked = false;
        if (args.RadioUnavailable)
        {
            SetStatus("Bluetooth radio unavailable", StatusKind.BluetoothUnavailable, args.Error);
        }
        else if (args.Successful)
        {
            SetStatus("Listener stopped", StatusKind.Idle);
        }
        else
        {
            SetStatus($"Listener stopped: {args.Error}", StatusKind.Error, args.Error);
        }
    });

    private void SensorSource_AdvertisementReceived(object? sender, SensorAdvertisementReceivedEventArgs args) =>
        Dispatcher.InvokeAsync(() => UpdateDevice(args));

    private void UpdateDevice(SensorAdvertisementReceivedEventArgs args)
    {
        if (!devicesByAddress.TryGetValue(args.BluetoothAddress, out var device))
        {
            device = new Aranet4Device { Address = args.Address };
            device.LoadHistory(sensorMonitor.LoadHistory(device.Address));
            devicesByAddress.Add(args.BluetoothAddress, device);
            sensorSource.RegisterKnownAddress(args.BluetoothAddress);
            Dashboard.Devices.Add(device);

            // The dashboard is designed around the primary sensor; keep the first one in focus.
            if (Dashboard.SelectedDevice is null)
            {
                Dashboard.SelectedDevice = device;
                DevicesList.ScrollIntoView(device);
            }
        }

        device.LastSeen = args.Timestamp;
        device.Rssi = args.Rssi;
        device.Packets++;
        if (!string.IsNullOrWhiteSpace(args.LocalName))
        {
            device.Name = args.LocalName;
        }

        if (args.IsScanResponse)
        {
            device.LastScanResponse = args.Packet;
        }
        else
        {
            device.LastAdvertisement = args.Packet;
        }

        if (args.Measurement is { } measurement)
        {
            device.Firmware = measurement.Firmware;
            device.Co2Ppm = measurement.Co2;
            device.TemperatureCelsius = measurement.TemperatureCelsius;
            device.Temperature = Metrics.FormatWithUnit((double)measurement.TemperatureCelsius, MetricKind.Temperature, preferences.TemperatureDisplayUnit);
            device.Pressure = $"{measurement.PressureHpa:0.0} hPa";
            device.Humidity = $"{measurement.HumidityPercent}%";
            device.HumidityValue = measurement.HumidityPercent;
            device.Battery = measurement.BatteryPercent is null ? "—" : $"{measurement.BatteryPercent}%";
            device.BatteryValue = measurement.BatteryPercent ?? 0;
            device.MeasurementInterval = measurement.IntervalSeconds is null ? "—" : $"{measurement.IntervalSeconds} s";
            device.MeasurementAge = FormatMeasurementAge(measurement.AgeSeconds);
            device.IntegrationState = "Live Smart Home beacon decoded";

            var now = args.Timestamp;
            var observation = sensorMonitor.ProcessMeasurement(
                device.Address,
                measurement,
                now,
                AlertThreshold,
                TimeSpan.FromMinutes(AlertDurationMinutes),
                AlertsPaused);
            if (observation.SampleAdded)
            {
                device.ReplaceHistory(observation.History);
                var monitoring = observation.Monitoring!;
                if (monitoring.NotificationReady)
                {
                    preferences.LastCo2AlertAt = now;
                    preferences.LastCo2AlertPpm = measurement.Co2;
                    preferencesStore.Save(preferences);
                    SetAlertStatus($"Alert sent: {measurement.Co2:N0} ppm at {now:t}", alertStatusDanger);
                    notifications.NotifyHighCo2(measurement.Co2);
                }
                else if (monitoring.NotificationDeferred)
                {
                    SetAlertStatus(
                        $"Above your alert level, but notifications are paused until {alertsPausedUntil:t}.",
                        alertStatusWarning);
                }
                else if (measurement.Co2 > AlertThreshold)
                {
                    SetAlertStatus(
                        $"Above your alert level; waiting for {AlertDurationMinutes} minutes of sustained readings.",
                        alertStatusWarning);
                }
                else if (measurement.Co2 <= monitoring.ResetThresholdPpm)
                {
                    // Air is fine again. If we had raised the alarm, celebrate with a short all-clear.
                    if (monitoring.RecoveryNotificationDue)
                    {
                        notifications.NotifyRecovered(measurement.Co2);
                    }

                    if (preferences.LastCo2AlertAt is { } previousAlert)
                    {
                        SetAlertStatus(
                            $"Recovered below {monitoring.ResetThresholdPpm:N0} ppm. Last alert {previousAlert:t}.",
                            alertStatusSuccess);
                    }
                    else
                    {
                        SetAlertStatus("No active high-CO₂ alert.", alertStatusNeutral);
                    }
                }
            }
        }
        else if (args.DecodeMessage != "Waiting for an Aranet manufacturer beacon.")
        {
            device.IntegrationState = args.DecodeMessage;
        }

        // Don't overwrite a sync progress/result message the user is still reading.
        if (syncCancellation is null && sensorSource.IsActive)
        {
            SetStatus("Listening", StatusKind.Listening);
        }

        if (Dashboard.SelectedDevice == device)
        {
            ShowDetails(device);
            UpdateTray();
        }
    }

    private void CopyPacket_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string which })
        {
            return;
        }

        var text = which == "adv" ? AdvertisementText.Text : ScanResponseText.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        try
        {
            Clipboard.SetText(text);
        }
        catch (System.Runtime.InteropServices.COMException) { /* clipboard busy */ }
    }

    private static string FormatMeasurementAge(ushort? ageSeconds)
    {
        if (ageSeconds is not { } seconds)
        {
            return "—";
        }

        if (seconds < 5)
        {
            return "just now";
        }

        if (seconds < 60)
        {
            return $"{seconds}s ago";
        }

        if (seconds < 3_600)
        {
            return $"{seconds / 60}m {seconds % 60}s ago";
        }

        return $"{seconds / 3_600}h {seconds % 3_600 / 60}m ago";
    }

}
