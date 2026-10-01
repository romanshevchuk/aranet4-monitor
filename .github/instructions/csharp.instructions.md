# C# Style Guide for This Repository

## Goals

Prefer C# that is:

- easy to scan
- visually quiet
- consistent
- idiomatic
- explicit where it improves readability
- not artificially compressed

The main preference is **less visual clutter**. In particular, do not use `_fieldName`
for private fields. Use `fieldName`.

## Naming

Use normal C# casing without underscore prefixes:

```csharp
private readonly Dictionary<ulong, Aranet4Device> devicesByAddress = new();
private BluetoothLEAdvertisementWatcher? watcher;
private readonly Co2AlertService co2Alerts = new();
private readonly HashSet<string> alertedDevices = new(StringComparer.OrdinalIgnoreCase);
```

Use:

- `PascalCase` for types, methods, properties, events, constants, and public members.
- `camelCase` for private fields, parameters, locals, and private implementation details.
- `IThing` for interfaces.
- Avoid abbreviations unless they are established domain terms (`Co2`, `Rssi`, etc.).

## Braces and control flow

Always use braces for `if`, `else`, `for`, `foreach`, `while`, and similar statements.

Prefer:

```csharp
if (watcher is null)
{
    return;
}
```

over:

```csharp
if (watcher is null) return;
```

This keeps small changes from silently changing control flow and makes nested logic easier to scan.

## One logical operation per line

Avoid dense one-line blocks when they contain meaningful behavior.

Prefer:

```csharp
if (watcher is not null)
{
    return;
}
```

and:

```csharp
try
{
    Clipboard.SetText(text);
}
catch (COMException)
{
    // Clipboard can be temporarily busy.
}
```

Expression-bodied members are fine when the entire operation is genuinely simple:

```csharp
private void StartButton_Click(object sender, RoutedEventArgs e) => StartListening();
```

Don't force large methods, conditionals, or lambdas onto one line.

## Blank lines

Use blank lines to separate logical phases:

```csharp
var advertisement = args.Advertisement;

var manufacturerBlocks = advertisement.ManufacturerData
    .Select(...)
    .ToArray();

var isAranet = Aranet4AdvertisementFilter.IsCandidate(...);

if (!isAranet)
{
    return;
}
```

Avoid blank lines between every statement. A blank line should communicate a change of thought.

## Conditionals

Prefer guard clauses:

```csharp
if (watcher is null)
{
    return;
}
```

instead of wrapping the whole method in another level of indentation.

Prefer pattern matching and null checks:

```csharp
if (sender is not Button { Tag: string which })
{
    return;
}
```

but don't use clever syntax merely to save a line.

## Method size

If a method is doing several unrelated jobs, split it.

For example, a method that:

1. updates device state,
2. records history,
3. evaluates alerts,
4. updates the UI,
5. updates the tray

is a good candidate for several focused private methods.

The goal is not "short methods at all costs". The goal is that a reader can understand the
method without mentally reconstructing several independent workflows.

## LINQ

Use fluent LINQ when it improves readability:

```csharp
var manufacturerBlocks = advertisement.ManufacturerData
    .Select(block => new ManufacturerBlock(
        block.CompanyId,
        ToBytes(block.Data)))
    .ToArray();
```

Avoid LINQ when a normal loop is substantially easier to understand, especially when the
loop has side effects or complicated branching.

## Object initializers

Prefer compact initializers for simple objects:

```csharp
var watcher = new BluetoothLEAdvertisementWatcher
{
    ScanningMode = BluetoothLEScanningMode.Active
};
```

For several properties with meaningful logic, expand the code rather than trying to make
the initializer clever.

## Comments

Comments should explain **why**, not repeat **what** the code says.

Good:

```csharp
// The dashboard is designed around the primary sensor; keep the first one in focus.
```

Bad:

```csharp
// Set selected device to device.
Dashboard.SelectedDevice = device;
```

Keep comments short and close to the code they explain.

## Fields

Prefer fields that read naturally in code:

```csharp
private readonly Dictionary<ulong, Aranet4Device> devicesByAddress = new();
private BluetoothLEAdvertisementWatcher? watcher;
```

Avoid:

```csharp
private readonly Dictionary<ulong, Aranet4Device> _devicesByAddress = new();
private BluetoothLEAdvertisementWatcher? _watcher;
```

If removing underscores causes ambiguity with properties, prefer clearer names rather than
reintroducing prefixes:

```csharp
private readonly Co2AlertService co2Alerts = new();
private readonly HashSet<string> alertedDevices = new();
```

## Formatting tool

The repository contains an `.editorconfig` with the project's formatting and naming rules.

Run:

```bash
dotnet format
```

to format the solution.

To see what would change without modifying files:

```bash
dotnet format --verify-no-changes
```

For a specific solution or project:

```bash
dotnet format MySolution.sln
dotnet format MyProject.csproj
```

For a CI check:

```bash
dotnet format MySolution.sln --verify-no-changes
```

If the SDK reports analyzer/style diagnostics separately, use:

```bash
dotnet format MySolution.sln analyzers
```

## Agent instructions

When creating or editing C#:

1. Follow `.editorconfig`.
2. Do not add `_` prefixes to private fields.
3. Use braces for control-flow statements.
4. Prefer guard clauses.
5. Keep methods visually structured with logical blank lines.
6. Avoid dense one-line statements when they contain real logic.
7. Prefer simple, idiomatic C# over clever code-golf syntax.
8. Use expression-bodied members only for genuinely trivial members.
9. Keep comments focused on intent and non-obvious constraints.
10. Run `dotnet format` after substantial C# changes.
11. Do not reformat unrelated files just because formatting changed elsewhere.

## Important

Formatting is not architecture.

If a method is still difficult to read after formatting, refactor the method rather than
adding more formatting rules.
