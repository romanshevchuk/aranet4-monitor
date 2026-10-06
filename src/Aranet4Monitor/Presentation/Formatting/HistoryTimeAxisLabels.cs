using System;
using System.Collections.Generic;
using System.Globalization;
using Aranet4Monitor.Presentation.Controls;

namespace Aranet4Monitor.Presentation.Formatting;

public static class HistoryTimeAxisLabels
{
    public static IReadOnlyList<MetricChartAxisLabel> ForRange(DateTime now, TimeSpan range)
    {
        var start = now - range;
        if (range == TimeSpan.FromDays(1))
        {
            return
            [
                new(0, "Yesterday " + start.ToString("HH:mm", CultureInfo.CurrentCulture)),
                new(0.25, (start + TimeSpan.FromHours(6)).ToString("HH:mm", CultureInfo.CurrentCulture)),
                new(0.5, (start + TimeSpan.FromHours(12)).ToString("HH:mm", CultureInfo.CurrentCulture)),
                new(0.75, (start + TimeSpan.FromHours(18)).ToString("HH:mm", CultureInfo.CurrentCulture)),
                new(1, "Now"),
            ];
        }

        var labels = new List<MetricChartAxisLabel>
        {
            new(0, start.ToString("ddd", CultureInfo.CurrentCulture)),
        };
        for (var day = start.Date.AddDays(1); day < now; day = day.AddDays(1))
        {
            var position = (day - start).TotalSeconds / range.TotalSeconds;
            labels.Add(new MetricChartAxisLabel(position, day.ToString("ddd", CultureInfo.CurrentCulture)));
        }

        labels.Add(new MetricChartAxisLabel(1, now.ToString("ddd", CultureInfo.CurrentCulture)));
        return labels;
    }
}
