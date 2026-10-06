# Air Pulse: target UI structure (WPF)

Audience: an AI coding agent refactoring `src/Aranet4Monitor/Presentation`.
Visual source of truth: `docs/plans/air-pulse-target.html` (replace it with the v13 mockup). Open it in a browser and read its CSS for exact colors, sizes, radii and spacing. This document defines **structure, ownership and behavior**. The mockup defines **looks**.

## 0. Rules for the refactor

1. **Do not touch** `Aranet4Monitor.Core`, `Bluetooth/`, `Storage/` behavior, or `AGENTS.md` constraints (local-only data, incomplete history transfers must not advance the sync cursor, UI updates on the dispatcher).
2. The refactor changes only how the UI is composed and bound. Every existing feature must survive (listed in section 9).
3. One **shell** owns everything that is global (header, banners, status bar, toasts). One **page** owns everything specific to a tab. A global element must never live inside a page, and a page must never reach into the shell.
4. Pages are `UserControl`s with their own ViewModel. `MainWindow.xaml.cs` must shrink to window-chrome concerns only (drag, caption buttons, DPI, tray lifecycle hookup).
5. No `Visibility` toggling of whole pages inside a shared `ScrollViewer`. See section 2.
6. Prefer an in-repo `ObservableObject` and `RelayCommand` over adding a package. If a package is added, update `THIRD-PARTY-NOTICES.md`.

## 1. What is wrong today (diagnosis)

| Symptom | Cause in current code | Fix in this spec |
|---|---|---|
| Footer visible only on Live | `StatusStripGrid` sits inside `DashboardGrid`, which is inside `DashboardScrollViewer`, as a sibling of `HistoryView` | Status bar becomes a direct child of the shell grid, row 3, outside any scroller |
| Footer would scroll away on small windows | Same | Shell rows: Auto, Auto, `*`, Auto. Only the page area scrolls |
| Tabs are fake | `DashboardGrid` and `HistoryView` are collapsed/shown inside one `ScrollViewer` | Each page is its own `UserControl` with its own scroller |
| No Settings page | `SettingsNavigationButton` opens `MoreMenu` (a context menu) plus `AlertSettingsPopup` | Real Settings page (section 5.3). Delete `MoreMenu` and `AlertSettingsPopup` |
| 1,500-line `MainWindow.xaml`, ~1,900 lines in `MainWindow.*.cs` partials | Everything is named elements poked from code-behind | Split into the components below. Bind to ViewModels |
| `DashboardViewModel` is 45 lines | Real state lives in code-behind fields | Move state into the ViewModels in section 6 |
| `DashboardResources.xaml` is one 800-line file | Styles for everything mixed | Split by concern (section 7) |
| Sync progress and sync button are in the header | Global header used for a History action | Sync button moves to History page. Sync progress shows in the status bar (section 4.3) |

## 2. Shell layout

`MainWindow` contains exactly one root `Grid`. Window chrome stays on `WindowChrome`.

```
MainWindow  (default 1180x760, MinWidth 940, MinHeight 640)
└─ Border  (rounded window surface, 1px border)
   └─ Grid  ← ShellGrid
      ├─ Row 0  Auto   HeaderBar
      ├─ Row 1  Auto   BannerHost            (collapsed when empty)
      ├─ Row 2  *      PageHost              (3 pages stacked in one cell)
      ├─ Row 3  Auto   StatusBar             (ALWAYS visible, every tab)
      └─ Overlay: ToastHost                  (Grid.RowSpan=4, bottom-center, above StatusBar)
```

**PageHost** is a single `Grid` (row 2, column 0). It holds `LivePage`, `HistoryPage` and `SettingsPage` in the same cell. Visibility is bound to `Shell.CurrentPage` (enum `AppPage { Live, History, Settings }`) with a converter. Hidden pages are `Collapsed`, so they do not render, but they keep their state (selected metric, range, scroll position). **Each page contains its own `ScrollViewer`** (vertical auto, horizontal disabled, no visible scrollbar chrome beyond the thin themed one) wrapped around its content grid.

Page content padding: 24 top, 26 left/right, 24 bottom. Gap between cards: 16 (use one `CardGap` resource, not literals).

## 3. Component tree

Everything below is a `UserControl` under `Presentation/Views/` unless stated otherwise. Indentation = containment.

```
MainWindow
├─ HeaderBar
│  ├─ BrandMark                      (logo ring + "Air Pulse")
│  ├─ NavTabs                        (Live | History | Settings)
│  ├─ [flex spacer / window drag region]
│  ├─ DeviceChip                     (+ DevicePopup, section 4.2)
│  └─ CaptionButtons                 (min / max / close, only if custom chrome)
├─ BannerHost
│  └─ StatusBanner                   (0..2 visible, section 4.1)
├─ PageHost
│  ├─ LivePage
│  │  ├─ HeroCard
│  │  │  ├─ QualityBadge
│  │  │  ├─ StaleCaption             (hidden while live)
│  │  │  ├─ ValueBlock               (big number + unit)
│  │  │  ├─ TrendLine
│  │  │  ├─ AdviceLine               (one line)
│  │  │  ├─ ZoneScale                (bar + marker + labels)
│  │  │  └─ HeroFooter               (3 facts)
│  │  ├─ ChartCard
│  │  │  ├─ ChartHeader              (title, stats caption, RangeSegment)
│  │  │  ├─ MetricChart              (existing control)
│  │  │  └─ ContextNote
│  │  └─ MetricCardsRow
│  │     └─ MetricCard ×3            (Temperature, Humidity, Pressure)
│  ├─ HistoryPage
│  │  ├─ PageHeader                  (title, verdict, sync status, SyncHistoryButton)
│  │  ├─ HistoryChartCard
│  │  │  ├─ ChartHeader              (reuse)
│  │  │  ├─ MetricChart              (reuse, taller)
│  │  │  └─ DayStripSection
│  │  │     ├─ DayStrip              (72 stripes)
│  │  │     └─ TimeAxis
│  │  └─ HistorySidebar
│  │     ├─ ZoneShareSection
│  │     ├─ InsightsSection
│  │     └─ ExportSection
│  └─ SettingsPage
│     ├─ PageHeader                  (reuse)
│     └─ SettingsCard
│        └─ SettingRow ×N            (grouped, section 5.3)
├─ StatusBar
│  ├─ ConnectionStatus               (dot + text + SignalBars)
│  ├─ SyncProgress                   (visible only while syncing)
│  └─ BatteryButton                  (+ BatteryFlyout)
└─ ToastHost
   └─ ToastCard
```

Separate windows (not part of the shell tree): the tray flyout (`ToastWindow` and `TrayIconService`). They reuse `QualityBadge`, `AdviceLine` text and zone colors but are laid out independently.

## 4. Global components

### 4.1 BannerHost / StatusBanner
- Replaces `NoticeBar` and `SyncProblemBanner`. One reusable `StatusBanner` control: left accent bar (3px, color by severity), bold title, muted detail line, one optional action button on the right.
- Binds to `Shell.Banners` (ordered list, max 2 shown, highest priority first). Priority: BluetoothOff, SensorNotFound, SensorStale, SyncProblem.
- Text and actions (match mockup):
  - **SensorStale**: "No new reading for N min", detail "Showing the last reading from HH:mm. This usually clears when you move closer to the sensor." Action: *Try again*.
  - **SensorNotFound**: "{Device} not found", detail "Last seen at HH:mm. Check that Bluetooth is on and the sensor has battery. Your history is safe on the sensor." Action: *Search again*.
  - **BluetoothOff**: action *Open Bluetooth settings* (existing `BluetoothSettings_Click` behavior).
  - **SyncProblem**: existing message, action *Retry*, dismissible.
- Lives in the shell so it shows on every tab.

### 4.2 HeaderBar
Grid columns: `Auto` (BrandMark), `Auto` (NavTabs, margin-left 24), `*` (drag region), `Auto` (DeviceChip), `Auto` (CaptionButtons).
- **NavTabs**: three `RadioButton`-style tab buttons (style `NavTab`). Selected state: soft filled pill. **No underline** (one indicator only). Bound to `Shell.CurrentPage`. Keyboard: Left/Right arrows, Ctrl+1/2/3.
- **DeviceChip**: button showing device name + short address. **No status dot, no alert badge** inside the chip (removed on purpose, because a colored dot beside the name was read as that room's status). Opens `DevicePopup` (existing popup content moves into `DevicePopup.xaml`: device list when more than one sensor, details panel, diagnostics expander, "Add sensor", "Manage"). Keep the 250 ms re-open guard. Per-device zone dots remain inside the popup list.
- Header contains **no** Sync button, **no** theme control, **no** unit switch, **no** progress UI.

### 4.3 StatusBar (global footer)
Border, top hairline, secondary surface, height ~44. Grid columns: `Auto`, `*`, `Auto`.
- **ConnectionStatus** (col 0, one inline group): `● Live` + `SignalBars`. The dot color and text follow the connection state table (section 8). Show text "Weak signal" next to the bars **only** when weak. Tooltip on the whole group: "Last reading N s ago. The sensor sends a new reading about every 2 min." Signal bars have their own tooltip with the dBm value. Never show raw dBm as visible text.
- **SyncProgress** (col 1, centered, collapsed unless syncing): small progress text ("Downloading history 42%") and a *Cancel* link. Migrates `SyncProgressPanel`, `SyncStatusText`, `CancelHistorySyncButton`.
- **BatteryButton** (col 2, right): `Battery [bar] 66%`. Opens `BatteryFlyout` (existing detail). This is the only interactive element in the bar.
- The bar is global: it shows data for the selected device on every tab.

### 4.4 ToastHost
Overlay across all rows, bottom center, margin-bottom = StatusBar height + 12. `ToastCard` style from existing `ToastCard`. Auto-dismiss 4 s, one at a time. Replaces `SyncToast`/`SyncToastText`.

## 5. Pages

### 5.1 LivePage
Root: `ScrollViewer` → `Grid` with rows `*` (MinHeight 430) and `Auto`; columns `4*` and `7*` (HeroCard MinWidth 340).

```
┌ HeroCard (r0,c0) ─────────┐ ┌ ChartCard (r0,c1) ──────────────────────┐
│ badge            [stale]  │ │ CO₂                          [1h 6h 24h]│
│ 721  ppm CO₂              │ │ Low · average · high                    │
│ ▲ Rising 109 ppm / 32 min │ │ ┌ MetricChart ────────────────────────┐ │
│ Comfortable. Nothing...   │ │ │                                     │ │
│ ZoneScale                 │ │ └─────────────────────────────────────┘ │
│ ───────────────────────── │ │ ─────────────────────────────────────── │
│ 90%     1,059 ppm  18:40  │ │ ● Outdoor reference · about 420 ppm  ⓘ  │
│ Good·24h Peak·05:56 Aired │ │   Fresh outdoor air is the lowest...    │
└───────────────────────────┘ └─────────────────────────────────────────┘
┌ MetricCard ┐ ┌ MetricCard ┐ ┌ MetricCard ┐     (r1, spans both columns)
```

**HeroCard** (Grid rows: Auto, Auto, Auto, Auto, Auto, `*`, Auto)
1. Row 0: `QualityBadge` (left) and `StaleCaption` (right). Badge = outlined pill, colored dot + zone word (Good / Elevated / High). **No glyph or icon in the badge.** `StaleCaption` ("Last reading 14 min ago") is collapsed while live. Do not show "Updated N s ago" while live.
2. Row 1: `ValueBlock`: very large light-weight number (tabular numerals) and a smaller muted "ppm CO₂" baseline-aligned.
3. Row 2: `TrendLine`: "▲ Rising" (primary text) + "109 ppm over 32 min" (muted). When not live: "Not live" + "trend paused".
4. Row 3: `AdviceLine`: **single line**, `TextTrimming=CharacterEllipsis`, fixed line height so the card never changes height. Text: Good "Comfortable. Nothing to do." / Elevated "Open a window for about 10 minutes." / High "Open a window or door when you can." Tooltip holds the longer explanation. Empty when not live (keep the height).
5. Row 4: `ZoneScale`: 3-segment bar (green/amber/red, gaps 4), white ring marker positioned by value, threshold labels **only 1,000 and 1,400** at the segment boundaries, zone names (Good, Elevated, High) centered under each segment.
6. Row 5: flexible spacer.
7. Row 6: `HeroFooter`, fixed height `CardFooterHeight` (76), top hairline, 3 equal columns, each = value (semibold) over a one-line label (muted, no wrap, ellipsis): `90%` / "Good · 24 h"; `1,059 ppm` / "Peak · 05:56"; `18:40` / "Last aired". Tooltips explain each. "Last aired" comes from `AiringDetector` (latest event start time). Values are always derived from stored samples, never hard-coded.

Hero background and border:
- Background: raised neutral surface. When zone is Elevated or High, tint with the zone color at ~7%. No tint when not live.
- Border: when CO₂ is the selected metric, border = zone color (green/amber/red). When another metric is selected, the hero border is neutral and the selected `MetricCard` shows the selection border. Keep this behavior.
- No glows, gradients, pulsing or animated decoration. Marker position may ease over 300 ms.

**ChartCard** (Grid rows: Auto, `*`, Auto)
- `ChartHeader`: title (metric name) and stats caption "Low X · average Y · high Z unit" on the left; `RangeSegment` (1h / 6h / 24h) on the right.
- `MetricChart`: existing control. It is chart-only: it must not draw titles or notes.
- `ContextNote` (row 2): top hairline, fixed height `CardFooterHeight` (76), bottom-anchored so its hairline aligns **exactly** with the HeroFooter hairline. Layout: 3 columns (dot, text, info icon). Text = bold title line over one muted explanation line (ellipsis). The info icon opens a styled tooltip popover (same style as `BatteryFlyout`), not a native tooltip. Content comes from `LiveViewModel.ContextNote` by selected metric (CO₂: outdoor reference, with the disclaimer that CO₂ shows ventilation, not overall air quality; humidity: comfort range; temperature, pressure: simple context).
- The note has **no box, no background, no border except the top hairline.**

**MetricCardsRow**: `UniformGrid` 3 columns, gap 16, each `MetricCard`: label with a small colored dot, value + muted unit, one sub line ("Steady this hour", "Rising this hour", humidity "Comfortable · ideal 30–50%"). No numeric ranges on the card. The card is a toggle: it sets `SelectedMetric`, and the selected card gets the selection border.

### 5.2 HistoryPage
Root: `ScrollViewer` → `Grid` rows `Auto`, `*`; columns `*` and `280`.

- **PageHeader** (row 0, spans both): left: title "How the room breathed" (22px) and a one-line verdict ("Mostly good: 90% of the last 24 h stayed below 1,000 ppm."), computed from stats. Right (aligned center): muted "Last synced 2 h ago · 1,204 readings" followed by `SyncHistoryButton` (ghost style, neutral, **not** a colored primary). Button disabled while syncing. No paragraph explaining the mechanism, put that in the button tooltip.
- **HistoryChartCard** (row 1, col 0): `ChartHeader` (reuse; range options 24h and 7d), `MetricChart` (MinHeight 320), then `DayStripSection`.
  - `DayStripSection`: heading "Day at a glance" + muted "· one stripe every 20 minutes · ● airing detected". `DayStrip`: 72 equal rounded stripes colored by zone, height 40, gap 3. A 5px dot sits above each stripe where an airing event starts. Each stripe has a tooltip "HH:mm · N ppm". `TimeAxis` below: "Yesterday 22:10 | 04:00 | 10:00 | 16:00 | Now". The first label must not equal the last label.
  - Chart x-axis for 24h uses the same labels ("Yesterday 22:10" ... "Now").
- **HistorySidebar** (row 1, col 1, card): three sections separated by hairlines.
  1. `ZoneShareSection`: "Time in each zone", a segmented bar, and "90% good · 10% elevated · 0% high".
  2. `InsightsSection` "Worth knowing" (bulleted, bold lead word): **Overnight** (23:00–07:00 average and peak), **Airing** (count, latest time, drop, duration, from `AiringDetector`), **Highest** (value and time). All text is generated from data. If no airing was found, say so plainly. Never hard-code example values.
  3. `ExportSection`: short text and an *Export CSV* button.

### 5.3 SettingsPage
Root: `ScrollViewer` → centered `SettingsCard` (MaxWidth 860, left-aligned). `PageHeader`: "Settings" with one line: "Notifications are off until you want them, and fire once per event. Everything stays on this PC: no account, no cloud."

`SettingRow` is a reusable control: `Grid` with column 0 (title semibold, description muted, wraps) and column 1 (the control, right-aligned), hairline between rows. Vertical padding 14.

Rows, in order (map each to the existing preference):

| Group | Row | Control | Existing source |
|---|---|---|---|
| Notifications | Notifications (master) | Switch, **default off** | new master flag in `AppPreferences` (or derive from existing alert enabled) |
| | Elevated CO₂ | Slider 700–1800 step 50 + value text (also duration) | `AlertThresholdTextBox`, `AlertDurationTextBox` |
| | High CO₂ | Switch | existing second threshold, if any |
| | Quiet hours | Two time pickers | new |
| | Try it | Button "Send test notification" | existing test toast |
| | Snooze | Button / menu (1 h, until tomorrow) | `PauseAlertsButton` |
| Display | Temperature unit | Segment °C / °F | `Celsius/FahrenheitUnitMenuItem` |
| | Appearance | Segment Auto / Light / Dark | `Auto/Light/DarkThemeMenuItem`, `ThemeService` |
| | Large popups | Switch | `LargePopupsMenuItem` |
| Tray and startup | Show CO₂ number in tray icon | Switch | `ShowNumberMenuItem` |
| | Keep listening when the window is closed | Switch | `ListenMenuItem` |
| | Start with Windows | Switch | `StartWithWindowsMenuItem`, `StartupRegistration` |
| Data | Your data | Text "N readings stored on this PC (about X MB)" + *Delete…* (confirm dialog) | `HistoryStore` |

Rules: while the master switch is off, every row under "Notifications" except the master is dimmed to 45% and **disabled** (`IsEnabled=false`, not just visually). Group titles are small muted uppercase labels. Settings apply immediately. There is no Save button.

## 6. ViewModels (Presentation/ViewModels)

| ViewModel | Owns | Used by |
|---|---|---|
| `ShellViewModel` | `CurrentPage`, `Banners`, `Toast`, `SelectedDevice`, `Devices`, `Connection` (state enum), commands: `Navigate`, `RetryConnection`, `OpenBluetoothSettings` | MainWindow, HeaderBar, BannerHost, ToastHost, StatusBar |
| `StatusBarViewModel` | connection text/color key, signal level (0–4) and dBm, battery %, last reading age, sync progress (text, percent, cancel command) | StatusBar |
| `DeviceSwitcherViewModel` | devices with zone per device, selection, details, diagnostics | DeviceChip, DevicePopup |
| `LiveViewModel` | latest reading, zone, trend text, advice text, scale marker position, hero footer facts, `SelectedMetric`, `ChartRange`, chart samples, `ContextNote`, metric card values and sub lines | LivePage |
| `HistoryViewModel` | range, samples, verdict, zone shares, day strip cells, airing events, insights, sync status text, `SyncHistoryCommand`, `ExportCommand` | HistoryPage |
| `SettingsViewModel` | all preference-backed properties, `TestNotificationCommand`, `SnoozeCommand`, `DeleteDataCommand` | SettingsPage |

Guidelines:
- ViewModels talk to the existing services (`SensorMonitor`, `HistorySyncService`, `Co2AlertService`, `IHistoryStore`, `IPreferencesStore`) through `AppServices`. Do not duplicate their logic. Zone classification, stats and airing detection stay in Core (`Co2Quality`, `Co2Stats`, `AiringDetector`).
- Display strings (advice, trend, insights) are produced in ViewModels or a small `Presentation/Formatting` helper, with unit tests where there is logic.
- All marshalling to the UI thread happens in one place (the ViewModel base or the service adapter), not in each view.
- Code-behind is allowed only for: popup open/close and focus guards, `WindowChrome` hit-testing, DPI/size-class detection that sets `ShellViewModel.IsCompact`, tray window lifecycle.

## 7. Resources and styles (Presentation/Resources)

Split `DashboardResources.xaml`; keep `Theme.Light.xaml` / `Theme.Dark.xaml` as the only color definitions.

```
Resources/
  Theme.Light.xaml, Theme.Dark.xaml      colors only (keep names; add any missing mockup tokens)
  Tokens.xaml                            CardGap, CardRadius, CardPadding, CardFooterHeight, StatusBarHeight, font sizes, spacing
  Typography.xaml                        AppFontFamily, HeroNumber, CardTitle, SectionLabel, SubText, TabularNumbers
  Buttons.xaml                           GhostButton, IconButton, LinkButton, NavTab, SegmentButton
  Cards.xaml                             CardStyle, PopoverCard, ToastCard, StatusBanner
  Controls.xaml                          Switch, Slider, ZoneScale, MetricCard, SettingRow, scrollbar
```

Rules: no hex colors or magic numbers in views; zone colors via `MetricColors` and theme resources; every interactive control has a visible keyboard focus style (existing `KeyboardFocusVisual`); text uses tabular numerals for all live numbers; set `AutomationProperties.Name` on icon-only controls, the signal bars, the zone scale and the day strip.

Quiet-by-default visual rules (from the audience brief): no pulsing, no glow, no decorative gradients, no persistent bright accent fills. The only saturated colors are zone colors (badge dot, scale, chart line, strips) and the thin selection border.

## 8. State matrix

Connection state drives several components at once. `Shell.Connection` is `Live | Stale | NotFound | BluetoothOff`.

| Component | Live | Stale (no new reading) | NotFound | BluetoothOff |
|---|---|---|---|---|
| StatusBar text and dot | "Live", green | "Waiting for sensor", amber | "Not connected", gray | "Bluetooth off", gray |
| SignalBars | shown, level from RSSI | hidden | hidden | hidden |
| Banner | none | SensorStale | SensorNotFound | BluetoothOff |
| HeroCard number | normal | 45% opacity | 45% opacity | 45% opacity |
| QualityBadge | zone word | "Last reading" (neutral dot) | same | same |
| StaleCaption | hidden | "Last reading N min ago" | "Last reading N h ago" | same |
| TrendLine / AdviceLine | normal | "Not live · trend paused" / empty | same | same |
| Hero tint and border | zone-based | neutral | neutral | neutral |
| History page | fully usable (stored data) | usable | usable | usable, Sync disabled |

Zone states: Good (<1000 ppm), Elevated (1000–1399), High (≥1400). Zone words are exactly "Good", "Elevated", "High". Never use "poor", "stuffy" or alarming copy.

## 9. Feature checklist (must still work after refactor)

Device chip and popup (multi-sensor list, details, diagnostics), live CO₂/temperature/humidity/pressure, metric selection with chart switching, 1h/6h/24h and 24h/7d ranges, chart hover/keyboard readout, history sync with progress and cancel, sync problem handling, Bluetooth-off handling, battery flyout, alerts (threshold, duration, snooze, test), tray icon (with number option) and tray flyout, toast messages, light/dark/auto theme, °C/°F, start with Windows, large popups, local history export to CSV, empty state when no sensor is paired.

## 10. Old name → new home (for searching the codebase)

| Old `x:Name` | New component |
|---|---|
| `NoticeBar`, `SyncProblemBanner` | `BannerHost` / `StatusBanner` |
| `StatusStripGrid`, `FooterStatusDot/Text`, `DetailSignal`, `RssiText` | `StatusBar` / `ConnectionStatus` |
| `FooterBatteryButton/Bar/Text` | `StatusBar` / `BatteryButton` |
| `SyncProgressPanel`, `SyncStatusText`, `CancelHistorySyncButton` | `StatusBar` / `SyncProgress` |
| `SyncToast`, `SyncToastText` | `ToastHost` |
| `Co2HeroPanel`, `Co2Tab` | `HeroCard` |
| `QualityBadge/Icon/Text` | `QualityBadge` (remove the icon) |
| `Co2Text`, `Co2UnitText` | `ValueBlock` |
| `TrendText`, `Co2AdviceText`, `Co2CaptionText` | `TrendLine`, `AdviceLine` (drop the caption if redundant) |
| `Gauge*`, `GoodZoneText`, `FairZoneText`, `PoorZoneText` | `ZoneScale` |
| `Co2DayShare*`, `Co2DayPeak*`, `Co2Airing*` | `HeroFooter` |
| `HistoryChartCard`, `ChartTitleText`, `ChartStatsText`, `HistoryChart` | `ChartCard` / `ChartHeader` / `MetricChart` |
| `ChartContext*` | `ContextNote` |
| `SecondaryMetricsGrid`, `TemperatureTab`, `HumidityTab`, `PressureTab` | `MetricCardsRow` / `MetricCard` |
| `HistoryView`, `HistoryContentGrid`, `HistoryMainCard` | `HistoryPage` / `HistoryChartCard` |
| `HistoryHeaderSummaryText`, `HistoryLastSyncText`, `SyncHistoryButton` | `HistoryPage` / `PageHeader` |
| `LongHistoryChart`, `HistoryDayStrip`, `History*TimeText` | `DayStripSection`, `TimeAxis` |
| `HistorySummaryCard`, `HistoryZoneShare*`, `HistoryOvernightText`, `HistoryAiringText`, `HistoryPeakText`, `HistoryExportButton` | `HistorySidebar` sections |
| `MoreButton`, `MoreMenu`, `*MenuItem` | `SettingsPage` rows |
| `AlertSettingsPopup`, `AlertThreshold/DurationTextBox`, `PauseAlertsButton`, `AlertStatusText` | `SettingsPage` Notifications rows |
| `DevicePopup` and children | `DevicePopup` (own file) |
| `DashboardScrollViewer`, `DashboardGrid` | removed (each page owns its scroller) |

## 11. Suggested file layout

```
Presentation/
  Views/
    MainWindow.xaml(.cs)            shell grid only
    Shell/    HeaderBar, NavTabs, DeviceChip, DevicePopup, BannerHost, StatusBanner,
              StatusBar, BatteryFlyout, ToastHost
    Live/     LivePage, HeroCard, ZoneScale, ChartCard, ContextNote, MetricCardsRow, MetricCard
    History/  HistoryPage, HistoryChartCard, DayStrip, HistorySidebar
    Settings/ SettingsPage, SettingRow
    Shared/   PageHeader, RangeSegment, QualityBadge
  Controls/   MetricChart, SignalBars (existing), DayStrip drawing if custom
  ViewModels/ Shell, StatusBar, DeviceSwitcher, Live, History, Settings, ObservableObject, RelayCommand
  Resources/  see section 7
```

## 12. Migration order (each step must build and run)

1. Add `ObservableObject`, `RelayCommand`, `AppPage`, `ShellViewModel`. Introduce the new shell grid with the four rows. Move the **status bar** out of the dashboard into row 3. Verify it appears on all three views. This fixes the visible bug first.
2. Extract `LivePage` (keep its internals as they are), give it its own `ScrollViewer`. Extract `HistoryPage` the same way. Replace `Visibility` swapping in code-behind with the `CurrentPage` binding.
3. Move banners into `BannerHost`, toast into `ToastHost`, sync progress into the status bar, Sync button into the History header.
4. Build `SettingsPage`; migrate every `MoreMenu` item and the alert popup; delete both.
5. Split Live internals into `HeroCard`, `ChartCard`, `ContextNote`, `MetricCard` with ViewModel bindings. Apply the visual rules from section 5.1 (single-line advice, aligned footers, no badge icon, hero tint).
6. Split History internals (`DayStrip`, sidebar sections); generate verdict and insights from data.
7. Split `DashboardResources.xaml`; remove dead names and code-behind.
8. Run `dotnet format Aranet4Monitor.sln` and `dotnet test Aranet4Monitor.sln`.

## 13. Acceptance criteria

- StatusBar is visible on Live, History and Settings, stays pinned at the bottom, and never scrolls.
- Resizing to MinWidth/MinHeight causes no clipping of the header, status bar or banners. Below ~900 px width: Live stacks Hero above Chart and metric cards go to one column; History moves the sidebar under the chart (drive this from `IsCompact`).
- HeroCard height does not change between Good, Elevated, High or not-live states. HeroFooter and ContextNote hairlines align on the same y position on every metric.
- Switching tabs preserves each page's selected metric, range and scroll position.
- No page contains global UI; the shell contains no page-specific UI.
- `MainWindow.xaml` is under ~150 lines. No file under `Views/` exceeds ~300 lines of XAML.
- Keyboard: Tab order follows visual order, tabs switch with arrow keys, popups close on Esc and return focus to their trigger.
- Visual check: with the target mockup open beside the running app in light and dark themes, all three tabs match the mockup's layout, spacing and copy.
