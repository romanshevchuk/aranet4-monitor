using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Aranet4Monitor.Models;

namespace Aranet4Monitor.Presentation.ViewModels;

public sealed class DashboardViewModel : INotifyPropertyChanged
{
    private Aranet4Device? _selectedDevice;
    private MetricKind _selectedMetric = MetricKind.Co2;
    private TimeSpan? _historyRange = TimeSpan.FromHours(6);

    public ObservableCollection<Aranet4Device> Devices { get; } = [];

    public Aranet4Device? SelectedDevice
    {
        get => _selectedDevice;
        set => SetField(ref _selectedDevice, value);
    }

    public MetricKind SelectedMetric
    {
        get => _selectedMetric;
        set => SetField(ref _selectedMetric, value);
    }

    public TimeSpan? HistoryRange
    {
        get => _historyRange;
        set => SetField(ref _historyRange, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}