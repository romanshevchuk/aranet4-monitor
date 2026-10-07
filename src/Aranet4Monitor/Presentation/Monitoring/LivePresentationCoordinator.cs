using System.Collections.Specialized;
using System.Windows.Threading;
using Aranet4Monitor.Presentation.ViewModels;
using Aranet4Monitor.Presentation.Views.Chrome;

namespace Aranet4Monitor.Presentation.Monitoring;

/// <summary>Owns periodic live-reading freshness and dashboard refresh work.</summary>
public sealed class LivePresentationCoordinator : IDisposable
{
    private readonly LiveViewModel live;
    private readonly HistoryViewModel history;
    private readonly SettingsViewModel settings;
    private readonly DevicePopupControl devicePopup;
    private readonly DashboardPresentationCoordinator dashboard;
    private readonly DispatcherTimer tickTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private int tickCount;
    private bool disposed;

    public LivePresentationCoordinator(
        LiveViewModel live,
        HistoryViewModel history,
        SettingsViewModel settings,
        DevicePopupControl devicePopup,
        DashboardPresentationCoordinator dashboard)
    {
        this.live = live;
        this.history = history;
        this.settings = settings;
        this.devicePopup = devicePopup;
        this.dashboard = dashboard;

        live.Devices.CollectionChanged += Devices_CollectionChanged;
        tickTimer.Tick += TickTimer_Tick;
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        tickTimer.Start();
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        tickTimer.Stop();
        tickTimer.Tick -= TickTimer_Tick;
        live.Devices.CollectionChanged -= Devices_CollectionChanged;
    }

    private void Devices_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        devicePopup.RefreshFromViewModels(live, history, DateTime.Now);
    }

    private void TickTimer_Tick(object? sender, EventArgs args)
    {
        foreach (var device in live.Devices)
        {
            device.Tick();
        }

        if (devicePopup.DevicePopup.IsOpen)
        {
            devicePopup.RefreshFromViewModels(live, history, DateTime.Now);
        }

        if (live.SelectedDevice is { } selected)
        {
            dashboard.RefreshLastSeen(selected);
        }

        settings.ExpireAlertPause(DateTime.Now);

        if (++tickCount % 10 == 0)
        {
            dashboard.RefreshChart();
            dashboard.UpdateTray();
        }
    }
}
