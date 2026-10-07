using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Aranet4Monitor.Presentation.ViewModels;
using Aranet4Monitor.Presentation.Views;
using Aranet4Monitor.Presentation.Views.Chrome;
using Aranet4Monitor.Presentation.Views.History;
using Aranet4Monitor.Presentation.Views.Live;

namespace Aranet4Monitor.Presentation.Monitoring;

/// <summary>Connects shell navigation and window size changes to the active page controls.</summary>
public sealed class ShellPresentationCoordinator : IDisposable
{
    private const double NarrowLayoutBreakpoint = 1060;

    private readonly Window window;
    private readonly ShellViewModel shell;
    private readonly HeaderBar headerBar;
    private readonly LivePage livePage;
    private readonly HistoryPage historyPage;
    private readonly SettingsPage settingsPage;
    private bool isNarrowLayout;
    private bool disposed;

    public ShellPresentationCoordinator(
        Window window,
        ShellViewModel shell,
        HeaderBar headerBar,
        LivePage livePage,
        HistoryPage historyPage,
        SettingsPage settingsPage)
    {
        this.window = window;
        this.shell = shell;
        this.headerBar = headerBar;
        this.livePage = livePage;
        this.historyPage = historyPage;
        this.settingsPage = settingsPage;

        shell.PropertyChanged += Shell_PropertyChanged;
        window.SizeChanged += Window_SizeChanged;
        SelectPage(shell.CurrentPage);
        UpdateResponsiveLayout(window.ActualWidth);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        shell.PropertyChanged -= Shell_PropertyChanged;
        window.SizeChanged -= Window_SizeChanged;
    }

    private void Shell_PropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(ShellViewModel.CurrentPage))
        {
            SelectPage(shell.CurrentPage);
        }
    }

    private void SelectPage(AppPage page)
    {
        headerBar.SelectPage(page);
        switch (page)
        {
            case AppPage.Live:
                livePage.Activate();
                break;
            case AppPage.History:
                historyPage.Activate();
                break;
            case AppPage.Settings:
                settingsPage.Activate();
                break;
        }
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs args) =>
        UpdateResponsiveLayout(args.NewSize.Width);

    private void UpdateResponsiveLayout(double width)
    {
        var useNarrowLayout = width < NarrowLayoutBreakpoint;
        if (useNarrowLayout == isNarrowLayout)
        {
            return;
        }

        isNarrowLayout = useNarrowLayout;
        livePage.ApplyNarrowLayout(useNarrowLayout);
        historyPage.ApplyNarrowLayout(useNarrowLayout);
    }
}
