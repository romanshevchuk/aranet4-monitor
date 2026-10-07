using Aranet4Monitor.Presentation;
using Aranet4Monitor.Presentation.ViewModels;

namespace Aranet4Monitor.Presentation.Monitoring;

/// <summary>Refreshes theme-dependent presentation after the active palette changes.</summary>
public sealed class ThemePresentationCoordinator : IDisposable
{
    private readonly StatusBarViewModel statusBar;
    private readonly DashboardPresentationCoordinator dashboard;
    private readonly LiveViewModel live;
    private bool disposed;

    public ThemePresentationCoordinator(
        StatusBarViewModel statusBar,
        DashboardPresentationCoordinator dashboard,
        LiveViewModel live)
    {
        this.statusBar = statusBar;
        this.dashboard = dashboard;
        this.live = live;

        ThemeService.Changed += ThemeService_Changed;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        ThemeService.Changed -= ThemeService_Changed;
    }

    private void ThemeService_Changed(object? sender, EventArgs args)
    {
        statusBar.RefreshTheme();
        dashboard.UpdateHeaderSensorState();
        if (live.SelectedDevice is { } device)
        {
            dashboard.ShowDetails(device);
        }
        else
        {
            dashboard.RefreshChart();
        }
    }
}
