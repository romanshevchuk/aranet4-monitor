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
            var clockLabels = new List<MetricChartAxisLabel>();
            var tick = start.Date;
            if (tick < start)
            {
                tick = tick.AddHours(6 * Math.Ceiling((start - tick).TotalHours / 6));
            }

            for (; tick < now; tick = tick.AddHours(6))
            {
                var label = tick.TimeOfDay == TimeSpan.Zero
                    ? tick.ToString("ddd HH:mm", CultureInfo.CurrentCulture)
                    : tick.ToString("HH:mm", CultureInfo.CurrentCulture);
                clockLabels.Add(new MetricChartAxisLabel((tick - start).TotalSeconds / range.TotalSeconds, label));
            }

            clockLabels.Add(new MetricChartAxisLabel(1, "Now"));
            return clockLabels;
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
