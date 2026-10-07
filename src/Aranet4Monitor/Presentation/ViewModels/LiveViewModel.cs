using System.Collections.ObjectModel;
namespace Aranet4Monitor.Presentation.ViewModels;

public sealed class LiveViewModel : ObservableObject
{
    private Aranet4Device? selectedDevice;
    private MetricKind selectedMetric = MetricKind.Co2;
    private TimeSpan? historyRange = TimeSpan.FromHours(1);

    public ObservableCollection<Aranet4Device> Devices { get; } = [];

    public Aranet4Device? SelectedDevice
    {
        get => selectedDevice;
        set => SetProperty(ref selectedDevice, value);
    }

    public MetricKind SelectedMetric
    {
        get => selectedMetric;
        set => SetProperty(ref selectedMetric, value);
    }

    public TimeSpan? HistoryRange
    {
        get => historyRange;
        set => SetProperty(ref historyRange, value);
    }
}
