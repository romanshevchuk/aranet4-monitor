using System.Globalization;
using System.Text;
using Aranet4Monitor.Application.History;
using Aranet4Monitor.Application.Monitoring;
using Aranet4Monitor.Domain.Measurements;
using Aranet4Monitor.Models;

namespace Aranet4Monitor.Presentation.ViewModels;

public sealed class HistoryViewModel : ObservableObject
{
    private readonly HistorySyncService? historySyncService;
    private readonly SensorMonitor? sensorMonitor;
    private TimeSpan selectedRange = TimeSpan.FromDays(1);
    private CancellationTokenSource? syncCancellation;
    private bool isSyncing;
    private string syncProgressText = string.Empty;
    private Aranet4Device? selectedDevice;

    public HistoryViewModel(HistorySyncService? historySyncService = null, SensorMonitor? sensorMonitor = null)
    {
        this.historySyncService = historySyncService;
        this.sensorMonitor = sensorMonitor;
        SelectRangeCommand = new RelayCommand(SelectRange);
    }

    public RelayCommand SelectRangeCommand { get; }

    private void SelectRange(object? parameter)
    {
        if (parameter is int hours)
        {
            SelectedRange = TimeSpan.FromHours(hours);
        }
        else if (parameter is string text
            && int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out hours))
        {
            SelectedRange = TimeSpan.FromHours(hours);
        }
    }

    public TimeSpan SelectedRange
    {
        get => selectedRange;
        set => SetProperty(ref selectedRange, value);
    }

    public Aranet4Device? SelectedDevice
    {
        get => selectedDevice;
        set => SetProperty(ref selectedDevice, value);
    }

    public DateTime? GetSyncCursor(Aranet4Device? device) =>
        device is null ? null : historySyncService?.LoadSyncCursor(device.Address);

    public HistoryPageState CreatePageState() => new(
        SelectedDevice,
        SelectedRange,
        GetSyncCursor(SelectedDevice),
        IsSyncing);

    public bool IsSyncing
    {
        get => isSyncing;
        private set => SetProperty(ref isSyncing, value);
    }

    public string SyncProgressText
    {
        get => syncProgressText;
        private set => SetProperty(ref syncProgressText, value);
    }

    public async Task<HistorySyncOutcome?> SyncAsync(Aranet4Device device, ulong bluetoothAddress)
    {
        if (historySyncService is null || sensorMonitor is null)
        {
            throw new InvalidOperationException("History sync is not configured.");
        }

        if (syncCancellation is not null)
        {
            return null;
        }

        var cancellation = new CancellationTokenSource();
        syncCancellation = cancellation;
        IsSyncing = true;
        SyncProgressText = string.Empty;
        try
        {
            var progress = new Progress<string>(message => SyncProgressText = message);
            var result = await historySyncService.SyncAsync(
                device.Address,
                bluetoothAddress,
                progress,
                cancellation.Token);
            sensorMonitor.ReplaceHistory(device.Address, result.Samples);
            device.ReplaceHistory(result.Samples);
            return result;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            return null;
        }
        finally
        {
            syncCancellation = null;
            cancellation.Dispose();
            IsSyncing = false;
            SyncProgressText = string.Empty;
        }
    }

    public void CancelSync() => syncCancellation?.Cancel();

    public HistorySyncFailure ClassifySyncFailure(Exception exception)
    {
        if (exception.Message.Contains("nearby", StringComparison.OrdinalIgnoreCase)
            || exception.Message.Contains("took too long", StringComparison.OrdinalIgnoreCase)
            || exception.Message.Contains("not connected", StringComparison.OrdinalIgnoreCase)
            || exception.Message.Contains("DeviceNotConnected", StringComparison.OrdinalIgnoreCase))
        {
            return HistorySyncFailure.SensorUnavailable;
        }

        return exception is IOException
            ? HistorySyncFailure.LocalStorage
            : HistorySyncFailure.Transfer;
    }

    public HistorySyncMessage DescribeSyncOutcome(HistorySyncOutcome outcome) => outcome.MissingRecords > 0
        ? new HistorySyncMessage(
            HistorySyncMessageKind.Incomplete,
            "Incomplete – retry needed",
            $"History sync was incomplete: {outcome.AddedSamples:N0} new readings saved, {outcome.MissingRecords:N0} records weren't received.")
        : outcome.AddedSamples > 0
            ? new HistorySyncMessage(HistorySyncMessageKind.Completed, $"Synced {outcome.AddedSamples:N0} new readings")
            : new HistorySyncMessage(HistorySyncMessageKind.Completed, "Already up to date");

    public HistoryCsvExport? CreateCsvExport(TimeSpan? visibleRange, DateTime now)
    {
        if (SelectedDevice is not { } device)
        {
            return null;
        }

        IEnumerable<Co2Sample> samples = device.History;
        if (visibleRange is { } range)
        {
            var earliest = now - range;
            samples = samples.Where(sample => sample.Time >= earliest);
        }

        var exportSamples = samples.ToArray();
        if (visibleRange is not null && exportSamples.Length == 0)
        {
            return null;
        }

        var csv = new StringBuilder("time,co2_ppm,temperature_c,humidity_percent,pressure_hpa\n");
        foreach (var sample in exportSamples)
        {
            csv.Append(sample.Time.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)).Append(',')
                .Append(sample.Ppm > 0 ? sample.Ppm.ToString(CultureInfo.InvariantCulture) : string.Empty).Append(',')
                .Append(sample.TemperatureCelsius?.ToString("0.0", CultureInfo.InvariantCulture) ?? string.Empty).Append(',')
                .Append(sample.HumidityPercent?.ToString(CultureInfo.InvariantCulture) ?? string.Empty).Append(',')
                .Append(sample.PressureHpa?.ToString("0.0", CultureInfo.InvariantCulture) ?? string.Empty).Append('\n');
        }

        var deviceLabel = device.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? device.Address;
        var rangeLabel = visibleRange switch
        {
            null => "all",
            { TotalHours: < 48 } shortRange => $"{(int)shortRange.TotalHours}h",
            { } longRange => $"{(int)longRange.TotalDays}d",
        };

        return new HistoryCsvExport($"aranet4-{deviceLabel}-{rangeLabel}.csv", csv.ToString());
    }
}

public sealed record HistoryCsvExport(string FileName, string Contents);

public sealed record HistoryPageState(
    Aranet4Device? Device,
    TimeSpan SelectedRange,
    DateTime? SyncCursor,
    bool IsSyncing);

public enum HistorySyncFailure
{
    SensorUnavailable,
    LocalStorage,
    Transfer,
}

public enum HistorySyncMessageKind
{
    Completed,
    Incomplete,
}

public sealed record HistorySyncMessage(HistorySyncMessageKind Kind, string Title, string? Details = null);
