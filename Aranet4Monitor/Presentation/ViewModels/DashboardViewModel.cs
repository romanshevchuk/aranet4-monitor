using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Aranet4Monitor.Models;

namespace Aranet4Monitor.Presentation.ViewModels;

public sealed class DashboardViewModel : INotifyPropertyChanged
{
    private Aranet4Device? selectedDevice;
    private MetricKind selectedMetric = MetricKind.Co2;
    private TimeSpan? historyRange = TimeSpan.FromHours(6);

    public ObservableCollection<Aranet4Device> Devices { get; } = [];

    public Aranet4Device? SelectedDevice
    {
        get => selectedDevice;
        set => SetField(ref selectedDevice, value);
    }

    public MetricKind SelectedMetric
    {
        get => selectedMetric;
        set => SetField(ref selectedMetric, value);
    }

    public TimeSpan? HistoryRange
    {
        get => historyRange;
        set => SetField(ref historyRange, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
