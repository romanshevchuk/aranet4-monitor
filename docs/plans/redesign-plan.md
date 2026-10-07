# Air Pulse — Light Theme Visual Refresh

## Goal

Restyle the existing WPF Live dashboard to match the approved Air Pulse visual direction:

* quieter, warmer light background
* fewer visually heavy borders
* white elevated cards
* softer red/amber/green state treatment
* CO₂ measurement remains the dominant element
* explicit human-readable CO₂ state
* calmer chart
* cleaner secondary metric cards
* more whitespace
* premium environmental-monitoring feel

**Do not redesign application architecture.**

**Do not add new sensor functionality.**

**Do not change Bluetooth, history, alerts, persistence, or data calculations.**

The existing `LivePage.xaml` already has the correct broad structure: CO₂ hero, chart, and three secondary metrics. The job is primarily to change its visual composition and supporting presentation resources. The existing `MetricChart` should remain the chart implementation. [Current LivePage.xaml](https://github.com/romanshevchuk/aranet4-monitor/blob/main/src/Aranet4Monitor/Presentation/Views/Live/LivePage.xaml)

---

# Phase 0 — Rules for the coding model

Before touching code:

1. Read `AGENTS.md`.
2. Read:

   * `Presentation/Resources/Tokens.xaml`
   * `Presentation/Resources/Theme.Light.xaml`
   * `Presentation/Resources/Theme.Dark.xaml`
   * `Presentation/Resources/Cards.xaml`
   * `Presentation/Resources/Controls.xaml`
   * `Presentation/Resources/Buttons.xaml`
   * `Presentation/Resources/Typography.xaml`
   * `Presentation/Views/Live/LivePage.xaml`
   * `Presentation/Views/Live/LivePage.xaml.cs`
   * `Presentation/ViewModels/LiveViewModel.cs`
   * `Presentation/Monitoring/LivePresentationCoordinator.cs`
   * `Presentation/Controls/MetricChart.cs`
3. Do not modify behavior until it is proven necessary for the visual changes.
4. Preserve all existing bindings, commands, accessibility names, keyboard behavior, and metric-selection behavior.
5. Preserve Dark theme behavior.
6. Do not introduce a UI framework/package.
7. Do not add a new custom chart control.
8. Do not add a "Ventilate room" button. That existed in the interactive concept only to demonstrate interaction. The production app should continue using the existing advice text unless an actual ventilation action is implemented later.

---

# Phase 1 — Establish the new visual tokens

### File

`src/Aranet4Monitor/Presentation/Resources/Theme.Light.xaml`

Replace the existing light palette with this direction.

### Target values

```text
App background       #F7F8F6
Surface              #FFFFFF
Surface secondary    #EEF2F3
Metric card          #FFFFFF

Border               #E4E9EB

Primary text         #17212B
Secondary text       #66727C

Positive             #18A978
Warning              #D7952F
Danger               #C9564E

CO₂ good             #18A978
CO₂ elevated         #D7952F
CO₂ high             #C9564E

Temperature          #D67A25
Humidity             #3779BD
Pressure             #7655BD
```

Do **not** delete existing resource keys.

The Dark theme must continue to compile and use the same resource-key set.

Do not replace all semantic colors with these literals throughout XAML. Define them once in `Theme.Light.xaml`.

---

# Phase 2 — Adjust spacing and typography tokens

### File

`Presentation/Resources/Tokens.xaml`

Change only tokens needed for the Live-page redesign.

Target:

```text
CardRadius       16–18
Card padding     approximately 22
Card gap         16

Hero value       approximately 76–84 px
Metric value     approximately 28–31 px

Body             13 px
Small            12 px
Heading          16 px
```

The existing hero value is currently `118`, which is substantially larger than the visual direction we approved. Reduce it.

Do not globally make every font larger.

The hierarchy should be:

```text
CO₂ number
    ↓
CO₂ state
    ↓
trend
    ↓
advice
    ↓
supporting information
```

---

# Phase 3 — Make cards quieter

### File

`Presentation/Resources/Cards.xaml`

Modify `CardStyle`.

Target:

```text
Background      Surface
CornerRadius    CardRadius
Padding         ~22
BorderBrush     Border
BorderThickness 1
```

But visually, the border should be extremely subtle.

Do **not** add heavy drop shadows to normal cards.

Normal cards should be primarily distinguished by:

> page background → white card

rather than:

> page background → dark outline → white card

Keep shadows only where they already make sense for popovers/toasts.

---

# Phase 4 — Redesign the CO₂ hero visually

### File

`Presentation/Views/Live/LivePage.xaml`

Do not rewrite the data model.

Keep the existing named controls wherever possible because the coordinator/code-behind already updates them.

The hero should visually become:

```text
┌──────────────────────────────────────┐
│                                      │
│ ● HIGH                               │
│                                      │
│ 1,527 ppm                            │
│                                      │
│ ▲ Rising 44 ppm over 62 min          │
│ CO₂ is above the recommended level.  │
│                                      │
│ ━━━━━━━━━━━━━━━●━━━━━━━━━━           │
│ Good       Elevated        High       │
│                                      │
│ ──────────────────────────────────── │
│ 34%          1,666         9:00 AM   │
│ Good ·24h    Peak          Last aired │
└──────────────────────────────────────┘
```

### Quality badge

Keep:

* colored dot
* `Good`
* `Elevated`
* `High`

But remove any unnecessary icon/glyph.

The badge should be:

* small
* outlined
* rounded
* restrained

It should **not** make the entire card look like an error.

---

# Phase 5 — Reduce the red-card effect

This is important.

The current implementation allows the selected CO₂ hero to have a strong state-colored border.

Change this so that:

### Good

Subtle green selection/state indication.

### Elevated

Subtle amber indication.

### High

Subtle red indication.

Do **not** use a saturated 2–3 px red border around the entire card.

Preferred:

```text
white card
very subtle state-tinted surface
1 px state-colored border
```

Approximately 5–8% tint for the background is enough.

The state color should communicate:

> "Pay attention"

rather than:

> "Critical system failure."

---

# Phase 6 — Improve the CO₂ explanation

Use the existing `Co2AdviceTextControl`.

Make sure the presentation produces concise human language:

### Good

```text
Comfortable. Nothing to do.
```

### Elevated

```text
Open a window for about 10 minutes.
```

### High

```text
Open a window or door when you can.
```

Keep the existing actual CO₂ thresholds and classification logic.

**Do not change the thresholds.**

Only change the wording if the existing presentation logic doesn't already provide an equivalent message.

The important UX sequence is:

```text
HIGH
1,527 ppm
↑ Rising 44 ppm over 62 min
CO₂ is above the recommended indoor level.
```

The user should not have to infer what `1,527` means.

---

# Phase 7 — Simplify the CO₂ gauge

The existing gauge already contains:

* green
* amber
* red
* marker
* 1,000
* 1,400
* Good
* Elevated
* High

Keep that concept.

Change the visual treatment:

### Keep

```text
green ━━━ amber ━━━ red
              ●
```

### Remove/de-emphasize

* unnecessary endpoint labels
* excessive numerical labels
* overly thick marker
* excessive spacing around the gauge

Only emphasize:

```text
1,000
1,400
```

The zone names should sit underneath:

```text
Good        Elevated        High
```

The marker should be a white circle with a dark outline, approximately 14–16 px.

---

# Phase 8 — Make the hero footer quieter

Keep the three existing facts.

Do not change their data source.

Visually:

```text
──────────────────────────────────────

34%             1,666             9:00 AM
Good · 24 h     Peak · 11:36      Last aired
```

Primary value:

* ~16 px
* semibold

Secondary label:

* 11–12 px
* secondary text color

Use a single subtle hairline.

Avoid vertical separator lines between the three columns.

---

# Phase 9 — Calm down the chart

### Files

```text
Presentation/Views/Live/LivePage.xaml
Presentation/Controls/MetricChart.cs
```

Do **not** rewrite chart behavior.

The existing `MetricChart` already owns chart rendering and theme colors. Keep that architecture.

[Current MetricChart.cs](https://github.com/romanshevchuk/aranet4-monitor/blob/main/src/Aranet4Monitor/Presentation/Controls/MetricChart.cs?utm_source=chatgpt.com)

### Visual changes

The chart should prioritize the data line.

Reduce:

* gridline contrast
* background-zone saturation
* threshold visual weight
* decorative chart elements

Use approximately:

```text
Gridlines       #E7EBED / very subtle
Threshold       muted dashed line
CO₂ line        state color
Latest point    white center + state-colored outline
```

The chart's background should remain mostly neutral.

Don't make the entire chart look like a giant red/green traffic-light visualization.

---

# Phase 10 — Keep chart header compact

Current structure is already good.

Preserve:

```text
CO₂
Low X · average Y · high Z              [1h][6h][24h]
```

Make the range selector feel like a small segmented control:

```text
┌─────────────────┐
│ 1h  │ 6h │ 24h │
└─────────────────┘
```

Selected option:

* white background
* dark text
* subtle shadow

Unselected:

* transparent
* muted text

Do not introduce a new command or range model.

The current `SelectHistoryRangeCommand` already exists in `LiveViewModel`.

---

# Phase 11 — Make the context note quieter

Keep the existing outdoor reference and informational disclaimer.

Target:

```text
────────────────────────────────────────

● Outdoor reference · about 420 ppm     ⓘ
  Fresh outdoor air is the lowest level
  a room can reach.
```

No box around this section.

No second card.

Only:

* top hairline
* small dot
* title
* explanation
* info affordance

The existing info tooltip can remain.

---

# Phase 12 — Redesign secondary metric cards

The existing `Temperature`, `Humidity`, and `Pressure` controls should become visually simpler.

Target:

```text
Temperature

22.1 °C

Steady this hour
```

```text
Humidity

50 %

Comfortable · ideal 30–50%
```

```text
Pressure

982.2 hPa

Steady this hour
```

Use:

* white surface
* subtle border
* 16 px radius
* 18–20 px padding

The colored dot is useful.

Keep it.

Do not make the entire card colored.

---

# Phase 13 — Make selected metric obvious

The secondary metric controls are currently `RadioButton`s.

Keep them as radio buttons.

When selected:

```text
subtle accent/state border
```

When not selected:

```text
neutral border
```

Do not use:

* large colored backgrounds
* large glow
* thick outlines

This is particularly important because CO₂ selection currently visually dominates.

---

# Phase 14 — Header polish

### File

`Presentation/Views/Chrome/HeaderBar.xaml`

The current structure is already very close to the desired design.

Keep:

```text
Air Pulse    Live   History   Settings          Device ▾
```

Selected navigation should be a **soft pill**, not an underline.

Target:

```text
Live
████
```

rather than:

```text
Live
────
```

The current implementation already has `HeaderNavigationSelectedButton`, so modify that style rather than creating a new navigation system.

The device chip should remain neutral.

Do not add status colors to the device chip.

---

# Phase 15 — Status bar

### File

`Presentation/Views/Chrome/StatusBarControl.xaml`

Keep the existing functionality.

Visually make it lighter:

```text
● Live   ▂▅▇                         Battery ▰ 65%
```

Signal bars remain.

Battery remains.

Sync progress remains.

Don't change the behavior.

The footer should feel like a quiet system-status area rather than another dashboard card.

---

# Phase 16 — Typography

### File

`Presentation/Resources/Typography.xaml`

Use:

```text
Font family:
Segoe UI Variable Text, Segoe UI
```

Keep that existing choice.

For large numerical values:

* light/normal weight
* tabular-looking numbers if already supported
* no excessive boldness

For labels:

* semibold where necessary
* otherwise regular

The main CO₂ number should visually resemble:

```text
1,527
```

rather than:

```text
1,527
^^^^^^
```

In other words: **large and elegant, not bold and heavy.**

---

# Phase 17 — Do not change these files

Unless compilation requires a tiny presentation-only change, do **not** modify:

```text
Bluetooth/*
Storage/*
Models/*
Alerts/*
AppServices.cs
Aranet4HistorySync.cs
Aranet4SensorSource.cs
```

Also do not change:

* CO₂ thresholds
* history calculations
* alert behavior
* BLE behavior
* synchronization behavior
* persistence format
* tray behavior

---

# Phase 18 — Dark theme safety

This is mandatory.

After changing the shared styles:

1. Launch Light theme.
2. Verify visual changes.
3. Switch to Dark theme.
4. Verify:

   * no white-on-white controls
   * no missing brushes
   * no invisible borders
   * no hard-coded light colors appearing in Dark theme

**Never put `#FFFFFF`, `#F7F8F6`, etc. directly into LivePage.xaml.**

Use theme resources.

---

# Phase 19 — Build verification

The coding model must run:

```powershell
dotnet format Aranet4Monitor.sln
dotnet test Aranet4Monitor.sln
dotnet build src/Aranet4Monitor/Aranet4Monitor.csproj
```

Then run:

```powershell
dotnet run --project src/Aranet4Monitor/Aranet4Monitor.csproj
```

The repository itself specifies `dotnet test` and the .NET 10 project structure, so these commands fit the existing project rather than introducing a new workflow. ([GitHub][1])
