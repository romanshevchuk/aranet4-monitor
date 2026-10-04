---
applyTo: "src/Aranet4Monitor/Bluetooth/**/*.cs"
---

# BLE Protocol Guidance

- Keep packet decoding bounded and explicit; preserve the supported legacy and current history-transfer paths.
- Use the existing protocol fixtures and Bluetooth tests when changing packet layouts, byte order, record indexing, or transfer behavior.
- Honor cancellation and keep transfers bounded when the sensor stalls or stops responding.
- Advance the saved history cursor only after a complete transfer; missing records must remain eligible for a later sync.
- Keep platform BLE access in the Bluetooth layer and avoid requiring physical hardware in automated tests.