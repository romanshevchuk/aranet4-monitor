using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Aranet4Monitor.Presentation.Controls;

public readonly record struct MetricChartAxisLabel(double Position, string Text);

/// <summary>
/// Lightweight line chart for one metric at a time (CO₂, temperature, humidity or pressure).
/// CO₂ gets air-quality bands and a level-coloured line; the others use their accent colour.
/// Drawn directly with DrawingContext, so it needs no charting package.
/// </summary>
public sealed class MetricChart : FrameworkElement
{
    private static readonly FontFamily Font = new("Segoe UI Variable Text, Segoe UI");
    private static readonly Typeface Regular = new(Font, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
    private static readonly Typeface Bold = new(Font, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

    // Rebuilt lazily whenever the theme changes.
    private static Palette? palette;

    private static Palette ThemePalette => palette is { } current && current.Version == ThemeService.Version
        ? current
        : palette = Palette.Create();

    private static Color Good => ThemePalette.Good;
    private static Color Warn => ThemePalette.Warn;
    private static Color Bad => ThemePalette.Bad;

    private static Brush AxisText => ThemePalette.Muted;
    private static Brush InkText => ThemePalette.Ink;
    private static Brush MutedText => ThemePalette.Muted;
    private static Brush SurfaceFill => ThemePalette.Surface;
    private static Pen GridPen => ThemePalette.Grid;
    private static Pen TooltipPen => ThemePalette.Grid;
    private static Pen ThresholdPen => ThemePalette.Threshold;
    private static Brush LabelPlate => Solid(ThemeService.GetColor("SurfaceColor"), 224);
    private readonly List<(FormattedText Text, Point Origin)> pendingLabels = [];
    private static Pen CursorPen => ThemePalette.Threshold;
    private static Pen FocusPen => ThemePalette.Focus;

    /// <summary>A longer silence than this breaks the line instead of drawing a misleading straight segment.</summary>
    private static readonly TimeSpan GapThreshold = TimeSpan.FromMinutes(30);

    private static readonly TimeSpan[] TickSteps =
    [
        TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(1), TimeSpan.FromHours(2), TimeSpan.FromHours(3), TimeSpan.FromHours(6),
        TimeSpan.FromHours(12), TimeSpan.FromDays(1), TimeSpan.FromDays(2), TimeSpan.FromDays(7),
    ];

    private static readonly double[] ValueSteps = [0.1, 0.2, 0.25, 0.5, 1, 2, 2.5, 5, 10, 20, 25, 50, 100, 200, 250, 500, 1000, 2000];

    public static readonly DependencyProperty SamplesProperty = DependencyProperty.Register(
        nameof(Samples), typeof(IReadOnlyList<Co2Sample>), typeof(MetricChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Visible time window ending "now"; null shows everything recorded.</summary>
    public static readonly DependencyProperty RangeProperty = DependencyProperty.Register(
        nameof(Range), typeof(TimeSpan?), typeof(MetricChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TimeAxisLabelsProperty = DependencyProperty.Register(
        nameof(TimeAxisLabels), typeof(IReadOnlyList<MetricChartAxisLabel>), typeof(MetricChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MetricProperty = DependencyProperty.Register(
        nameof(Metric), typeof(MetricKind), typeof(MetricChart),
        new FrameworkPropertyMetadata(MetricKind.Co2, FrameworkPropertyMetadataOptions.AffectsRender, (d, _) => ((MetricChart)d).hover = -1));

    public static readonly DependencyProperty TemperatureDisplayUnitProperty = DependencyProperty.Register(
        nameof(TemperatureDisplayUnit), typeof(TemperatureUnit), typeof(MetricChart),
        new FrameworkPropertyMetadata(TemperatureUnit.Celsius, FrameworkPropertyMetadataOptions.AffectsRender, (d, _) => ((MetricChart)d).hover = -1));

    public IReadOnlyList<Co2Sample>? Samples
    {
        get => (IReadOnlyList<Co2Sample>?)GetValue(SamplesProperty); set => SetValue(SamplesProperty, value);
    }
    public TimeSpan? Range
    {
        get => (TimeSpan?)GetValue(RangeProperty); set => SetValue(RangeProperty, value);
    }
    public IReadOnlyList<MetricChartAxisLabel>? TimeAxisLabels
    {
        get => (IReadOnlyList<MetricChartAxisLabel>?)GetValue(TimeAxisLabelsProperty);
        set => SetValue(TimeAxisLabelsProperty, value);
    }
    public MetricKind Metric
    {
        get => (MetricKind)GetValue(MetricProperty); set => SetValue(MetricProperty, value);
    }
    public TemperatureUnit TemperatureDisplayUnit
    {
        get => (TemperatureUnit)GetValue(TemperatureDisplayUnitProperty); set => SetValue(TemperatureDisplayUnitProperty, value);
    }

    private readonly record struct Pt(DateTime Time, double Value);

    // State captured during the last render so mouse hit-testing matches what is on screen.
    private Rect plot;
    private DateTime t0, t1;
    private Pt[] points = [];
    private int first, end;
    private int hover = -1;

    public MetricChart()
    {
        Loaded += (_, _) => ThemeService.Changed += OnThemeChanged;
        Unloaded += (_, _) => ThemeService.Changed -= OnThemeChanged;
    }

    private void OnThemeChanged(object? sender, EventArgs e) => InvalidateVisual();
    protected override Size MeasureOverride(Size availableSize) =>
        new(double.IsInfinity(availableSize.Width) ? 400 : availableSize.Width,
            double.IsInfinity(availableSize.Height) ? 200 : availableSize.Height);

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (end <= first || plot.Width <= 0)
        {
            return;
        }

        var x = e.GetPosition(this).X;
        var time = t0 + TimeSpan.FromSeconds((x - plot.Left) / plot.Width * (t1 - t0).TotalSeconds);

        // Nearest point by time (points are ordered, so binary search).
        int lo = first, hi = end - 1;
        while (lo < hi)
        {
            var mid = (lo + hi) / 2;
            if (points[mid].Time < time)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }
        if (lo > first && (time - points[lo - 1].Time) < (points[lo].Time - time))
        {
            lo--;
        }

        if (lo != hover)
        {
            hover = lo;
            InvalidateVisual();
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        if (hover != -1)
        {
            hover = -1;
            InvalidateVisual();
        }
    }

    protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (end <= first)
        {
            return;
        }

        var index = hover < first || hover >= end ? end - 1 : hover;
        switch (e.Key)
        {
            case System.Windows.Input.Key.Left:
                index = Math.Max(first, index - 1);
                break;
            case System.Windows.Input.Key.Right:
                index = Math.Min(end - 1, index + 1);
                break;
            case System.Windows.Input.Key.Home:
                index = first;
                break;
            case System.Windows.Input.Key.End:
                index = end - 1;
                break;
            default:
                return;
        }

        if (index != hover)
        {
            hover = index;
            InvalidateVisual();
        }

        e.Handled = true;
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, w, h)); // makes the whole area hit-testable
        if (IsKeyboardFocused && w >= 4 && h >= 4)
        {
            dc.DrawRoundedRectangle(null, FocusPen, new Rect(1, 1, w - 2, h - 2), 4, 4);
        }
        if (w < 120 || h < 80)
        {
            return;
        }

        var kind = Metric;
        var accent = MetricColors.Accent(kind);
        plot = new Rect(52, 8, w - 52 - 16, h - 8 - 28);
        pendingLabels.Clear();
        var pts = (Samples ?? Array.Empty<Co2Sample>())
            .Select(sample => (sample.Time, Value: Metrics.Value(sample, kind, TemperatureDisplayUnit)))
            .Where(p => p.Value.HasValue)
            .Select(p => new Pt(p.Time, p.Value!.Value))
            .ToArray();
        points = pts;
        var now = DateTime.Now;

        // ---- time domain ----
        DateTime t0, t1;
        if (Range is { } range)
        {
            t1 = now;
            t0 = now - range;
        }
        else if (pts.Length >= 2)
        {
            t0 = pts[0].Time;
            t1 = pts[^1].Time;
        }
        else
        {
            t1 = now;
            t0 = now - TimeSpan.FromHours(1);
        }
        if (t1 - t0 < TimeSpan.FromMinutes(1))
        {
            t1 = t0 + TimeSpan.FromMinutes(1);
        }

        // First/last visible point (points are ordered by time).
        var first = LowerBound(pts, t0);
        var end = LowerBound(pts, t1.AddTicks(1));
        this.t0 = t0;
        this.t1 = t1;
        this.first = first;
        this.end = end;
        if (hover < first || hover >= end)
        {
            hover = -1;
        }

        // ---- value domain ----
        double min, max;
        if (end > first)
        {
            min = double.MaxValue;
            max = double.MinValue;
            for (var i = first; i < end; i++)
            {
                min = Math.Min(min, pts[i].Value);
                max = Math.Max(max, pts[i].Value);
            }
        }
        else
        {
            (min, max) = kind switch
            {
                MetricKind.Co2 => (400.0, 1600.0),
                MetricKind.Temperature => TemperatureDisplayUnit == TemperatureUnit.Fahrenheit ? (64.4, 78.8) : (18.0, 26.0),
                MetricKind.Humidity => (30.0, 70.0),
                _ => (990.0, 1030.0),
            };
        }

        var minSpan = Metrics.MinSpan(kind, TemperatureDisplayUnit);
        if (max - min < minSpan)
        {
            // Flat data: centre it instead of pinning it to an edge.
            var mid = (min + max) / 2;
            min = mid - minSpan / 2;
            max = mid + minSpan / 2;
        }

        var step = NiceStep((max - min) / 4);
        var lo = Math.Floor((min - step * 0.25) / step) * step;
        var hi = Math.Ceiling((max + step * 0.25) / step) * step;
        if (kind != MetricKind.Temperature)
        {
            lo = Math.Max(0, lo);
        }

        if (kind == MetricKind.Humidity)
        {
            hi = Math.Min(100, hi);
            if (hi - lo < step * 3)
            {
                lo = Math.Max(0, hi - step * 3);
            }
        }
        else if (hi - lo < step * 3)
        {
            hi = lo + step * 3;
        }

        if (kind == MetricKind.Humidity)
        {
            // Always show the whole 30-50 comfort band with some margin (axis at least 20-60).
            lo = Math.Min(lo, 20);
            hi = Math.Max(hi, 60);
            step = Math.Max(step, NiceStep((hi - lo) / 6)); // keep the grid from getting dense
            lo = Math.Floor(lo / step) * step;
            hi = Math.Min(100, Math.Ceiling(hi / step) * step);
        }

        if (kind == MetricKind.Co2)
        {
            lo = Math.Max(400, Math.Floor(Math.Min(min, 400) / 400) * 400);
            hi = Math.Ceiling(Math.Max(max, 1600) / 400) * 400;
            step = hi <= 3200 ? 400 : NiceStep((hi - lo) / 5);
        }

        double X(DateTime t) => plot.Left + (t - t0).TotalSeconds / (t1 - t0).TotalSeconds * plot.Width;
        double Y(double v) => plot.Bottom - (v - lo) / (hi - lo) * plot.Height;

        // ---- background bands ----
        if (kind == MetricKind.Co2)
        {
            DrawBand(dc, Y, lo, hi, double.NegativeInfinity, Co2Quality.FairFromPpm, Good, 0);
            DrawBand(dc, Y, lo, hi, Co2Quality.FairFromPpm, Co2Quality.PoorFromPpm, Warn, 16);
            DrawBand(dc, Y, lo, hi, Co2Quality.PoorFromPpm, double.PositiveInfinity, Bad, 22);
        }
        else if (kind == MetricKind.Humidity)
        {
            DrawBand(dc, Y, lo, hi, 30, 50, Good, 14); // matches the "ideal 30-50%" on the humidity card
        }

        // ---- horizontal grid + y labels ----
        var yFormat = step < 1 ? "N1" : "N0";
        for (var gridValue = lo; gridValue <= hi + step * 0.001; gridValue += step)
        {
            var y = Math.Round(Y(gridValue)) + 0.5;
            dc.DrawLine(GridPen, new Point(plot.Left, y), new Point(plot.Right, y));
            var label = Text(gridValue.ToString(yFormat, CultureInfo.CurrentCulture), 12, AxisText);
            dc.DrawText(label, new Point(plot.Left - 8 - label.Width, y - label.Height / 2));
        }

        if (kind == MetricKind.Co2)
        {
            DrawCo2Threshold(dc, Y, Co2Quality.FairFromPpm, "Elevated");
            DrawCo2Threshold(dc, Y, Co2Quality.PoorFromPpm, "High");
        }
        else if (kind == MetricKind.Humidity)
        {
            DrawHumidityComfort(dc, Y, 30, 50);
        }

        // ---- time axis ----
        DrawTimeAxis(dc, X, t0, t1);

        if (end == first)
        {
            var msg = Text(pts.Length == 0
                ? "No readings yet — the first point appears once a measurement is decoded."
                : "No readings in this time range.", 13, MutedText);
            dc.DrawText(msg, new Point(plot.Left + (plot.Width - msg.Width) / 2, plot.Top + (plot.Height - msg.Height) / 2));
            return;
        }

        // ---- line + area (broken at gaps) ----
        var line = new StreamGeometry();
        var area = new StreamGeometry();
        using (var lc = line.Open())
        using (var ac = area.Open())
        {
            Point? last = null;
            for (var i = first; i < end; i++)
            {
                var p = new Point(X(pts[i].Time), Y(pts[i].Value));
                var startsRun = last is null || pts[i].Time - pts[i - 1].Time > GapThreshold;
                if (startsRun)
                {
                    if (last is { } previous)
                    {
                        ac.LineTo(new Point(previous.X, plot.Bottom), false, false);
                    }

                    lc.BeginFigure(p, false, false);
                    ac.BeginFigure(new Point(p.X, plot.Bottom), true, true);
                    ac.LineTo(p, false, false);
                }
                else
                {
                    lc.LineTo(p, true, true);
                    ac.LineTo(p, false, false);
                }
                last = p;
            }
            if (last is { } tail)
            {
                ac.LineTo(new Point(tail.X, plot.Bottom), false, false);
            }
        }
        line.Freeze();
        area.Freeze();

        Brush lineBrush, areaBrush;
        if (kind == MetricKind.Co2)
        {
            lineBrush = LevelGradient(lo, hi, 255);
            areaBrush = LevelGradient(lo, hi, 28);
        }
        else
        {
            lineBrush = Solid(accent);
            areaBrush = FadeGradient(accent);
        }

        dc.PushClip(new RectangleGeometry(plot));
        if (kind != MetricKind.Co2)
        {
            dc.DrawGeometry(areaBrush, null, area);
        }
        var pen = new Pen(lineBrush, 2.25) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        dc.DrawGeometry(null, pen, line);
        dc.Pop();
        DrawPendingLabels(dc);

        // Latest reading marker, or the hover read-out.
        if (hover == -1)
        {
            var newest = pts[end - 1];
            dc.DrawEllipse(SurfaceFill, new Pen(Solid(PointColor(kind, newest.Value, accent)), 2), new Point(X(newest.Time), Y(newest.Value)), 5, 5);
        }
        else
        {
            DrawHover(dc, kind, accent, pts[hover], X(pts[hover].Time), Y(pts[hover].Value), (t1 - t0).TotalHours > 36);
        }
    }

    private void DrawBand(DrawingContext dc, Func<double, double> y, double lo, double hi, double from, double to, Color color, byte alpha)
    {
        var a = Math.Max(from, lo);
        var b = Math.Min(to, hi);
        if (a >= b)
        {
            return;
        }

        var top = y(b);
        var bottom = y(a);
        dc.DrawRectangle(Solid(color, alpha), null, new Rect(plot.Left, top, plot.Width, bottom - top));
    }

    private void DrawCo2Threshold(DrawingContext dc, Func<double, double> y, int value, string label)
    {
        if (value < 400 || value > 20000)
        {
            return;
        }

        var yPosition = y(value);
        if (yPosition < plot.Top - 0.5 || yPosition > plot.Bottom + 0.5)
        {
            return;
        }

        dc.DrawLine(ThresholdPen, new Point(plot.Left, yPosition), new Point(plot.Right, yPosition));
        var text = Text($"{label} · {value:N0}", 11, MutedText);
        var labelY = yPosition - text.Height - 3;
        if (labelY < plot.Top)
        {
            labelY = yPosition + 3; // line sits at the top edge (e.g. "High · 1,400" with axis max 1,400): draw below it
        }

        pendingLabels.Add((text, new Point(plot.Left + 6, labelY)));
    }

    private void DrawPendingLabels(DrawingContext dc)
    {
        foreach (var (text, origin) in pendingLabels)
        {
            var plate = new Rect(origin.X - 3, origin.Y - 1, text.Width + 6, text.Height + 2);
            dc.DrawRoundedRectangle(LabelPlate, null, plate, 3, 3);
            dc.DrawText(text, origin);
        }

        pendingLabels.Clear();
    }

    private void DrawHumidityComfort(DrawingContext dc, Func<double, double> y, double from, double to)
    {
        foreach (var yPosition in new[] { y(from), y(to) })
        {
            if (yPosition >= plot.Top - 0.5 && yPosition <= plot.Bottom + 0.5)
            {
                dc.DrawLine(ThresholdPen, new Point(plot.Left, yPosition), new Point(plot.Right, yPosition));
            }
        }

        var top = y(to);
        if (top < plot.Top - 0.5 || top > plot.Bottom + 0.5)
        {
            return;
        }

        var text = Text($"Comfortable · {from:0}-{to:0}%", 11, MutedText);
        var labelY = Math.Max(plot.Top + 3, top + 3);
        pendingLabels.Add((text, new Point(plot.Left + 6, labelY)));
    }

    private void DrawTimeAxis(DrawingContext dc, Func<DateTime, double> x, DateTime t0, DateTime t1)
    {
        if (TimeAxisLabels is { Count: > 0 } labels)
        {
            var previousRight = double.NegativeInfinity;
            foreach (var axisLabel in labels)
            {
                var text = Text(axisLabel.Text, 12, AxisText);
                var px = plot.Left + plot.Width * Math.Clamp(axisLabel.Position, 0, 1);
                var maxLeft = Math.Max(plot.Left, plot.Right - text.Width);
                var left = Math.Clamp(px - text.Width / 2, plot.Left, maxLeft);
                if (left < previousRight + 8)
                {
                    continue;
                }

                dc.DrawText(text, new Point(left, plot.Bottom + 6));
                previousRight = left + text.Width;
            }

            return;
        }

        var span = t1 - t0;
        var maxTicks = Math.Max(2, (int)(plot.Width / 80));
        var step = Range is { } visibleRange && visibleRange >= TimeSpan.FromHours(6) && visibleRange <= TimeSpan.FromHours(36)
            ? TimeSpan.FromHours(6)
            : TickSteps.Cast<TimeSpan?>().FirstOrDefault(s => span.TotalSeconds / s!.Value.TotalSeconds <= maxTicks) ?? TickSteps[^1];

        // Align ticks to local "round" times (e.g. 14:00, 14:30).
        var n = Math.Ceiling((t0 - t0.Date).TotalSeconds / step.TotalSeconds);
        var tick = t0.Date + TimeSpan.FromSeconds(n * step.TotalSeconds);
        var format = Range is { TotalDays: > 1 }
            ? "ddd"
            : step >= TimeSpan.FromDays(1)
                ? "d MMM"
                : span.TotalHours > 36
                    ? "ddd HH:mm"
                    : "HH:mm";
        var nowLabel = Text("Now", 12, AxisText);
        var nowLeft = plot.Right - nowLabel.Width;

        // The cap guards against a corrupt, far-away timestamp turning the axis into millions of ticks.
        for (var drawn = 0; tick <= t1 && drawn < 200; tick += step, drawn++)
        {
            var px = Math.Round(x(tick)) + 0.5;
            var labelText = tick.TimeOfDay == TimeSpan.Zero && span.TotalHours >= 6
                ? tick.ToString("ddd HH:mm", CultureInfo.CurrentCulture)
                : tick.ToString(format, CultureInfo.CurrentCulture);
            var label = Text(labelText, 12, AxisText);
            var left = px - label.Width / 2;
            if (left < plot.Left - 12 || left + label.Width > plot.Right + 14 || left + label.Width > nowLeft - 8)
            {
                continue;
            }

            dc.DrawText(label, new Point(left, plot.Bottom + 6));
        }

        dc.DrawText(nowLabel, new Point(plot.Right - nowLabel.Width, plot.Bottom + 6));
    }

    private void DrawHover(DrawingContext dc, MetricKind kind, Color accent, Pt point, double px, double py, bool includeDay)
    {
        dc.DrawLine(CursorPen, new Point(px, plot.Top), new Point(px, plot.Bottom));
        dc.DrawEllipse(Solid(PointColor(kind, point.Value, accent)), new Pen(SurfaceFill, 2.5), new Point(px, py), 6, 6);

        var value = Text(Metrics.FormatWithUnit(point.Value, kind, TemperatureDisplayUnit), 15, InkText, bold: true);
        var when = point.Time.ToString(includeDay ? "ddd HH:mm" : "HH:mm", CultureInfo.CurrentCulture);
        var detail = Text(kind == MetricKind.Co2 ? $"{Co2Quality.Describe(Co2Quality.Classify((int)Math.Round(point.Value)))}  ·  {when}" : when, 12, MutedText);

        const double padX = 12, padY = 8;
        var boxW = Math.Max(value.Width, detail.Width) + padX * 2;
        var boxH = value.Height + detail.Height + padY * 2 + 1;
        var boxX = px + 14 + boxW > plot.Right ? px - 14 - boxW : px + 14;
        var boxY = Math.Clamp(py - boxH / 2, plot.Top + 2, plot.Bottom - boxH - 2);

        dc.DrawRoundedRectangle(SurfaceFill, TooltipPen, new Rect(boxX, boxY, boxW, boxH), 8, 8);
        dc.DrawText(value, new Point(boxX + padX, boxY + padY));
        dc.DrawText(detail, new Point(boxX + padX, boxY + padY + value.Height + 1));
    }

    /// <summary>Vertical gradient (in absolute coordinates) that paints each part of the CO₂ line by air-quality level.</summary>
    private LinearGradientBrush LevelGradient(double lo, double hi, byte alpha)
    {
        double Offset(double v) => Math.Clamp((hi - v) / (hi - lo), 0, 1);
        double o1400 = Offset(Co2Quality.PoorFromPpm), o1000 = Offset(Co2Quality.FairFromPpm);

        var brush = new LinearGradientBrush
        {
            MappingMode = BrushMappingMode.Absolute,
            StartPoint = new Point(0, plot.Top),
            EndPoint = new Point(0, plot.Bottom),
        };
        Color With(Color c) => Color.FromArgb(alpha, c.R, c.G, c.B);
        brush.GradientStops.Add(new GradientStop(With(Bad), 0));
        brush.GradientStops.Add(new GradientStop(With(Bad), o1400));
        brush.GradientStops.Add(new GradientStop(With(Warn), o1400));
        brush.GradientStops.Add(new GradientStop(With(Warn), o1000));
        brush.GradientStops.Add(new GradientStop(With(Good), o1000));
        brush.GradientStops.Add(new GradientStop(With(Good), 1));
        brush.Freeze();
        return brush;
    }

    /// <summary>Soft vertical fade under a single-colour line.</summary>
    private LinearGradientBrush FadeGradient(Color color)
    {
        var brush = new LinearGradientBrush
        {
            MappingMode = BrushMappingMode.Absolute,
            StartPoint = new Point(0, plot.Top),
            EndPoint = new Point(0, plot.Bottom),
        };
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(30, color.R, color.G, color.B), 0));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(3, color.R, color.G, color.B), 1));
        brush.Freeze();
        return brush;
    }

    private static Color PointColor(MetricKind kind, double value, Color accent) =>
        kind == MetricKind.Co2 ? (value < Co2Quality.FairFromPpm ? Good : value < Co2Quality.PoorFromPpm ? Warn : Bad) : accent;

    private static double NiceStep(double raw)
    {
        foreach (var step in ValueSteps)
        {
            if (raw <= step)
            {
                return step;
            }
        }

        return ValueSteps[^1];
    }

    /// <summary>Index of the first point at or after <paramref name="time"/>.</summary>
    private static int LowerBound(Pt[] points, DateTime time)
    {
        int lo = 0, hi = points.Length;
        while (lo < hi)
        {
            var mid = (lo + hi) / 2;
            if (points[mid].Time < time)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }
        return lo;
    }

    private FormattedText Text(string text, double size, Brush brush, bool bold = false) =>
        new(text, CultureInfo.CurrentCulture, System.Windows.FlowDirection.LeftToRight, bold ? Bold : Regular, size, brush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

    private static SolidColorBrush Solid(Color color, byte alpha = 255)
    {
        var brush = new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
        brush.Freeze();
        return brush;
    }

    /// <summary>Theme-dependent drawing objects, created once per palette change.</summary>
    private sealed class Palette
    {
        public required int Version { get; init; }
        public required Color Good { get; init; }
        public required Color Warn { get; init; }
        public required Color Bad { get; init; }
        public required Brush Ink { get; init; }
        public required Brush Muted { get; init; }
        public required Brush Surface { get; init; }
        public required Pen Grid { get; init; }
        public required Pen Threshold { get; init; }
        public required Pen Focus { get; init; }

        public static Palette Create()
        {
            var muted = Solid(ThemeService.GetColor("TextSecondaryColor"));
            return new Palette
            {
                Version = ThemeService.Version,
                Good = ThemeService.GetColor("Co2GoodStrokeColor"),
                Warn = ThemeService.GetColor("Co2FairStrokeColor"),
                Bad = ThemeService.GetColor("Co2PoorStrokeColor"),
                Ink = Solid(ThemeService.GetColor("TextPrimaryColor")),
                Muted = muted,
                Surface = Solid(ThemeService.GetColor("SurfaceColor")),
                Grid = new Pen(Solid(ThemeService.GetColor("BorderColor")), 1),
                Threshold = new Pen(Solid(ThemeService.GetColor("DisabledColor")), 1) { DashStyle = DashStyles.Dash },
                Focus = new Pen(Solid(ThemeService.GetColor("AccentColor")), 2),
            };
        }
    }
}
