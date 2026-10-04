using Aranet4Monitor.Presentation.ViewModels;
using Xunit;

namespace Aranet4Monitor.Tests.Presentation;

public sealed class DashboardViewModelTests
{
    [Fact]
    public void StartsWithDashboardDefaults()
    {
        var viewModel = new DashboardViewModel();

        Assert.Empty(viewModel.Devices);
        Assert.Null(viewModel.SelectedDevice);
        Assert.Equal(MetricKind.Co2, viewModel.SelectedMetric);
        Assert.Equal(TimeSpan.FromHours(6), viewModel.HistoryRange);
    }

    [Fact]
    public void NotifiesWhenPresentationStateChanges()
    {
        var viewModel = new DashboardViewModel();
        var changedProperties = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        viewModel.SelectedMetric = MetricKind.Temperature;
        viewModel.HistoryRange = null;

        Assert.Equal([nameof(viewModel.SelectedMetric), nameof(viewModel.HistoryRange)], changedProperties);
    }
}
