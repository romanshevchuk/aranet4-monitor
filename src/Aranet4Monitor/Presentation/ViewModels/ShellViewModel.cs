using Aranet4Monitor.Abstractions;
using Aranet4Monitor.Application.History;
using Aranet4Monitor.Application.Monitoring;
using Aranet4Monitor.Presentation;

namespace Aranet4Monitor.Presentation.ViewModels;

public sealed class ShellViewModel : ObservableObject
{
    private AppPage currentPage = AppPage.Live;

    public ShellViewModel(
        HistorySyncService? historySyncService = null,
        SensorMonitor? sensorMonitor = null,
        LiveViewModel? live = null,
        ISensorSource? sensorSource = null)
    {
        Live = live ?? new LiveViewModel(sensorSource, sensorMonitor);
        History = new HistoryViewModel(historySyncService, sensorMonitor);
        NavigateCommand = new RelayCommand(
            parameter =>
            {
                if (parameter is AppPage page)
                {
                    CurrentPage = page;
                }
            },
            parameter => parameter is AppPage);
    }

    public AppPage CurrentPage
    {
        get => currentPage;
        private set => SetProperty(ref currentPage, value);
    }

    public LiveViewModel Live { get; }

    public HistoryViewModel History { get; }

    public RelayCommand NavigateCommand { get; }
}
