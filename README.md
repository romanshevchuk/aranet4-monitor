# Aranet4 Home Listener

Windows desktop app for passively receiving Aranet4 Bluetooth Low Energy beacons. It does **not** pair or connect to the device.

It recognizes Aranet4s by their name, `FCE0` advertised service, or Aranet manufacturer identifier `0x0702`. The app keeps separate BLE advertisement and scan-response captures, then decodes the Smart Home Integrations beacon into CO₂, temperature, pressure, humidity, battery, firmware version, measurement interval, and measurement age.

## Run

```powershell
dotnet run
```

Bluetooth must be enabled and the PC needs an adapter capable of BLE scanning.

## If the device appears but has no reading

Your current capture is a valid Aranet4 **ScanResponse**: it contains the `FCE0` service and local name, but no measurement payload. Keep the listener open to receive its paired `Advertisement` packet. If it remains on “Waiting for sensor advertisement”, enable **Smart Home Integrations** for the device in the Aranet Home app. The readable beacon uses manufacturer company ID `0x0702`; without it, this listener intentionally does not attempt a connection or decrypt data.

## CO₂ history

Every decoded measurement is stored once (the sensor repeats each reading in many packets) and drawn in the **CO₂ history** chart. Use the 1h / 6h / 24h / All selector, hover the chart for exact values, and **Export CSV** to save the readings.

History is kept for 7 days per device in `%LocalAppData%\AranetHome\history\`, so the chart survives restarts. **Clear** only resets the live view; delete that folder to wipe saved history. The listener starts automatically when the app opens.
