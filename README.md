# Aranet4 Home

Windows desktop monitor for Aranet4 sensors. It listens for Smart Home Integration BLE beacons and can pair/connect to retrieve measurements and history stored on the sensor.

## Run

Requires Windows and the .NET 8 SDK. Enable Bluetooth and keep the Aranet4 nearby.

```powershell
dotnet run
```

## Sensor history

Select a detected sensor and choose **Sync sensor** to pair and import readings. The first sync downloads the full sensor log, even if live beacon samples are already saved. Later syncs use a separate per-sensor cursor from the last successfully completed sync, with a one-record overlap to safely merge by timestamp. **Cancel sync** leaves the cursor unchanged; retrying re-fetches the unfinished range. The app stores CO₂, temperature, humidity, and pressure history, and exports those fields to CSV. The dashboard has one chart at a time: click the CO₂, Temperature, Humidity or Pressure card to chart that metric. Every card also shows the min–max for the selected time range. **Sync sensor** imports all four metrics at once.

History and sync cursors are saved per sensor under `%LocalAppData%\AranetHome\history\`; up to 10,000 samples per device are retained. The alert threshold, alert duration, and last-alert details are stored in `%LocalAppData%\AranetHome\settings.json`. Clearing the device list does not delete saved history.

## Ventilation alerts

By default, the app notifies when CO₂ stays above 1,500 ppm for 10 minutes and rearms at or below 1,400 ppm. Both the threshold and 1–60 minute persistence duration can be adjusted in the history panel. The persistence duration and reset margin are app choices to suppress brief spikes, not HSE-prescribed timing. The UK Health and Safety Executive says consistently higher than 1,500 ppm in an occupied room indicates poor ventilation, and cautions that CO₂ readings are a broad ventilation guide, not proof of a safe level. See [HSE guidance on using CO₂ monitors](https://www.hse.gov.uk/ventilation/using-co2-monitors.htm).

Notifications are short and playful, and their tone escalates with the reading. By default they appear as the app's own large pop-up (bottom-right, click to open the dashboard); right-click the tray icon → **Large pop-up notifications** to switch back to the standard Windows notification, whose text size is controlled by Windows (Settings → Accessibility → Text size). Use **Send test** (next to the threshold) to preview one, or **Pause 1 h** to snooze alerts. When the air clears after an alert you get a quick all-clear.

## Dashboard layout

The window is designed to show everything at once, with no scrolling: four metric cards across the top (they double as chart tabs), one chart below, and a bottom strip with the CO₂ alert settings and sensor details (battery, signal, reading age, interval). The single-sensor device list lives behind the small device chip in the header (click it for the device list, firmware, packet count and Bluetooth diagnostics). Start/stop listening and "Forget detected devices" are in the ⋯ menu.

## System tray

Closing or minimizing the window keeps the listener running in the tray.

- The tray icon **is the live CO₂ number**, coloured by air quality (green / amber / red). The tile fills the whole icon and the digits are stretched to use every pixel. Below 1,000 ppm it shows the exact value ("820"); above that it shows thousands with one decimal ("1.3" = 1,300 ppm). Hover for the exact value. It turns grey if the sensor hasn't been heard from for 5 minutes. Right-click → **Show number on tray icon** switches to a plain coloured disc instead.
- Click the icon to open the dashboard. Right-click for the menu: current reading, **Pause alerts for 1 hour**, **Start with Windows**, and **Exit**.
- With **Start with Windows** enabled, the app launches hidden in the tray (`--tray`).

## Tests and protocol reference

Run fixture-based history protocol, incremental indexing, metric merging, and alert tests with:

```powershell
dotnet test BleListener.sln
```

The [Aranet4-Python project](https://github.com/Anrijs/Aranet4-Python) (MIT) documents the GATT history protocol and is a useful reference for future firmware compatibility work.

## Project layout

```
App.xaml, GlobalUsings.cs     Application entry point
Views/                        MainWindow (XAML + code-behind)
Controls/                     MetricChart, SignalBars
Bluetooth/                    Beacon parser, GATT history sync, history protocol, HistoryTransfer
Models/                       Aranet4Device, Co2Sample, Co2Stats, Co2Quality, Metrics
Alerts/                       Co2AlertService (when to alert), AlertMessages (what to say)
Tray/                         TrayIconService (icon, menu, notifications), TrayIconRenderer (number icon), ToastWindow (large pop-up)
Storage/                      HistoryStore, AppPreferences, StartupRegistration
Assets/                       App icon
BleListener.Tests/            xUnit tests, mirroring the folders above
```

All code shares the `BleListener` namespace; folders only organise the files.
