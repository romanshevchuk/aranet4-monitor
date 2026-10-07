using System.ComponentModel;
using System.Windows.Threading;
using Aranet4Monitor.Models;
using Aranet4Monitor.Presentation.ViewModels;
using Aranet4Monitor.Presentation.Views.Chrome;
using Aranet4Monitor.Presentation.Views.History;

namespace Aranet4Monitor.Presentation.Monitoring;

/// <summary>Coordinates history sync progress and completion feedback across the history page and status bar.</summary>
public sealed class HistoryPresentationCoordinator : IDisposable
{
    private readonly HistoryViewModel history;
    private readonly HistoryPage historyPage;
    private readonly StatusBarViewModel statusBar;
    private readonly StatusBarControl statusBarControl;
    private readonly DevicePopupControl devicePopup;
    private readonly LiveViewModel live;
    private readonly DashboardPresentationCoordinator dashboard;
    private readonly DispatcherTimer syncToastTimer = new() { Interval = TimeSpan.FromSeconds(4) };
    private bool disposed;

    public HistoryPresentationCoordinator(
        HistoryViewModel history,
        HistoryPage historyPage,
        StatusBarViewModel statusBar,
        StatusBarControl statusBarControl,
        DevicePopupControl devicePopup,
        LiveViewModel live,
        DashboardPresentationCoordinator dashboard)
    {
        this.history = history;
        this.historyPage = historyPage;
        this.statusBar = statusBar;
        this.statusBarControl = statusBarControl;
        this.devicePopup = devicePopup;
        this.live = live;
        this.dashboard = dashboard;

        history.PropertyChanged += History_PropertyChanged;
        historyPage.SyncStarted += SyncStarted;
        historyPage.SyncFinished += SyncFinished;
        historyPage.SyncToastRequested += ShowSyncToast;
        historyPage.SyncProblemRequested += ShowSyncProblem;
        historyPage.SyncCompleted += SyncCompleted;
        historyPage.CsvExported += CsvExported;
        syncToastTimer.Tick += SyncToastTimer_Tick;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        syncToastTimer.Stop();
        history.PropertyChanged -= History_PropertyChanged;
        historyPage.SyncStarted -= SyncStarted;
        historyPage.SyncFinished -= SyncFinished;
        historyPage.SyncToastRequested -= ShowSyncToast;
        historyPage.SyncProblemRequested -= ShowSyncProblem;
        historyPage.SyncCompleted -= SyncCompleted;
        historyPage.CsvExported -= CsvExported;
        syncToastTimer.Tick -= SyncToastTimer_Tick;
    }

    private void History_PropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(HistoryViewModel.SelectedRange))
        {
            historyPage.RefreshFromLiveDevice();
        }
        else if (args.PropertyName == nameof(HistoryViewModel.SyncProgressText))
        {
            statusBar.SetSyncStatus(history.SyncProgressText, null);
        }
        else if (args.PropertyName == nameof(HistoryViewModel.IsSyncing))
        {
            historyPage.RefreshFromLiveDevice();
            statusBarControl.SetHistorySyncState(history.IsSyncing);
        }
    }

    private void SyncStarted()
    {
        statusBar.HideSyncProblem();
        statusBar.HideSyncToast();
        syncToastTimer.Stop();
        statusBar.SetSyncStatus(string.Empty, null);
    }

    private void SyncFinished()
    {
        statusBar.SetSyncStatus(string.Empty, null);
        devicePopup.RefreshFromViewModels(live, history, DateTime.Now);
    }

    private void SyncCompleted(Aranet4Device device)
    {
        if (live.SelectedDevice == device)
        {
            dashboard.ShowDetails(device);
        }

        devicePopup.RefreshFromViewModels(live, history, DateTime.Now);
    }

    private void CsvExported(string fileName) => ShowSyncToast($"Saved {fileName}");

    private void ShowSyncToast(string text)
    {
        statusBar.ShowSyncToast(text);
        syncToastTimer.Stop();
        syncToastTimer.Start();
    }

    private void ShowSyncProblem(string text, string details) => statusBar.ShowSyncProblem(text, details);

    private void SyncToastTimer_Tick(object? sender, EventArgs args)
    {
        syncToastTimer.Stop();
        statusBar.HideSyncToast();
    }
}
