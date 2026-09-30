using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace BleListener;

/// <summary>
/// Lightweight CO₂ line chart: quality bands, level-coloured line, time axis, hover read-out.
/// Drawn directly with DrawingContext, so it needs no charting package.
/// </summary>
public sealed class Co2Chart : FrameworkElement
{
    private static readonly FontFamily Font = new("Segoe UI Variable Text, Segoe UI");
    private static readonly Typeface Regular = new(Font, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
    private static readonly Typeface Bold = new(Font, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

    private static readonly Color Good = Color.FromRgb(0x2E, 0xC2, 0x7E);
    private static readonly Color Warn = Color.FromRgb(0xF5, 0xB9, 0x42);
    private static readonly Color Bad = Color.FromRgb(0xEF, 0x5B, 0x5B);

    private static readonly Brush AxisText = Solid(Color.FromRgb(0x83, 0x91, 0xA7));
    private static readonly Brush InkText = Solid(Color.FromRgb(0x16, 0x22, 0x38));
    private static readonly Brush MutedText = Solid(Color.FromRgb(0x66, 0x73, 0x8A));
    private static readonly Pen GridPen = new(Solid(Color.FromRgb(0xE8, 0xED, 0xF5)), 1);
    private static readonly Pen TooltipPen = new(Solid(Color.FromRgb(0xD5, 0xDD, 0xEA)), 1);
    private static readonly Pen CursorPen = new(Solid(Color.FromRgb(0xA9, 0xB6, 0xCB)), 1) { DashStyle = DashStyles.Dash };

    /// <summary>A longer silence than this breaks the line instead of drawing a misleading straight segment.</summary>
    private static readonly TimeSpan GapThreshold = TimeSpan.FromMinutes(30);

    private static readonly TimeSpan[] TickSteps =
    [
        TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(1), TimeSpan.FromHours(2), TimeSpan.FromHours(3), TimeSpan.FromHours(6),
        TimeSpan.FromHours(12), TimeSpan.FromDays(1), TimeSpan.FromDays(2), TimeSpan.FromDays(7),
    ];

    public static readonly DependencyProperty SamplesProperty = DependencyProperty.Register(
        nameof(Samples), typeof(IReadOnlyList<Co2Sample>), typeof(Co2Chart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Visible time window ending "now"; null shows everything recorded.</summary>
    public static readonly DependencyProperty RangeProperty = DependencyProperty.Register(
        nameof(Range), typeof(TimeSpan?), typeof(Co2Chart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public IReadOnlyList<Co2Sample>? Samples { get => (IReadOnlyList<Co2Sample>?)GetValue(SamplesProperty); set => SetValue(SamplesProperty, value); }
    public TimeSpan? Range { get => (TimeSpan?)GetValue(RangeProperty); set => SetValue(RangeProperty, value); }

    // State captured during the last render so mouse hit-testing matches what is on screen.
    private Rect _plot;
    private DateTime _t0, _t1;
    private int _first, _end;
    private int _hover = -1;

    protected override Size MeasureOverride(Size availableSize) =>
        new(double.IsInfinity(availableSize.Width) ? 400 : availableSize.Width,
            double.IsInfinity(availableSize.Height) ? 200 : availableSize.Height);

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var samples = Samples;
        if (samples is null || _end <= _first || _plot.Width <= 0) return;

        var x = e.GetPosition(this).X;
        var time = _t0 + TimeSpan.FromSeconds((x - _plot.Left) / _plot.Width * (_t1 - _t0).TotalSeconds);

        // Nearest sample by time (samples are ordered, so binary search).
        int lo = _first, hi = _end - 1;
        while (lo < hi)
        {
            var mid = (lo + hi) / 2;
            if (samples[mid].Time < time) lo = mid + 1; else hi = mid;
        }
        if (lo > _first && (time - samples[lo - 1].Time) < (samples[lo].Time - time)) lo--;

        if (lo != _hover) { _hover = lo; InvalidateVisual(); }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hover != -1) { _hover = -1; InvalidateVisual(); }
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, w, h)); // makes the whole area hit-testable
        if (w < 120 || h < 80) return;

        _plot = new Rect(46, 8, w - 46 - 14, h - 8 - 26);
        var samples = Samples ?? Array.Empty<Co2Sample>();
        var now = DateTime.Now;

        // ---- time domain ----
        DateTime t0, t1;
        if (Range is { } range) { t1 = now; t0 = now - range; }
        else if (samples.Count >= 2) { t0 = samples[0].Time; t1 = samples[^1].Time; }
        else { t1 = now; t0 = now - TimeSpan.FromHours(1); }
        if (t1 - t0 < TimeSpan.FromMinutes(1)) t1 = t0 + TimeSpan.FromMinutes(1);

        // First/last visible sample (samples are ordered by time).
        var first = LowerBound(samples, t0);
        var end = LowerBound(samples, t1.AddTicks(1));
        _t0 = t0; _t1 = t1; _first = first; _end = end;
        if (_hover < first || _hover >= end) _hover = -1;

        // ---- value domain ----
        double min = 400, max = 1600;
        if (end > first)
        {
            min = double.MaxValue; max = double.MinValue;
            for (var i = first; i < end; i++) { min = Math.Min(min, samples[i].Ppm); max = Math.Max(max, samples[i].Ppm); }
        }
        var step = NiceStep(Math.Max(max - min, 200) / 4);
        var lo = Math.Max(0, Math.Floor((min - step * 0.25) / step) * step);
        var hi = Math.Ceiling((max + step * 0.25) / step) * step;
        if (hi - lo < step * 3) hi = lo + step * 3;

        double X(DateTime t) => _plot.Left + (t - t0).TotalSeconds / (t1 - t0).TotalSeconds * _plot.Width;
        double Y(double v) => _plot.Bottom - (v - lo) / (hi - lo) * _plot.Height;

        // ---- quality bands ----
        DrawBand(dc, Y, lo, hi, double.NegativeInfinity, 1000, Good);
        DrawBand(dc, Y, lo, hi, 1000, 1400, Warn);
        DrawBand(dc, Y, lo, hi, 1400, double.PositiveInfinity, Bad);

        // ---- horizontal grid + y labels ----
        for (var gridValue = lo; gridValue <= hi + 0.001; gridValue += step)
        {
            var y = Math.Round(Y(gridValue)) + 0.5;
            dc.DrawLine(GridPen, new Point(_plot.Left, y), new Point(_plot.Right, y));
            var label = Text(gridValue.ToString("N0", CultureInfo.CurrentCulture), 11, AxisText);
            dc.DrawText(label, new Point(_plot.Left - 8 - label.Width, y - label.Height / 2));
        }

        // ---- time axis ----
        DrawTimeAxis(dc, X, t0, t1);

        if (end == first)
        {
            var msg = Text(samples.Count == 0
                ? "No readings yet — the first point appears once a measurement is decoded."
                : "No readings in this time range.", 12.5, MutedText);
            dc.DrawText(msg, new Point(_plot.Left + (_plot.Width - msg.Width) / 2, _plot.Top + (_plot.Height - msg.Height) / 2));
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
                var p = new Point(X(samples[i].Time), Y(samples[i].Ppm));
                var startsRun = last is null || samples[i].Time - samples[i - 1].Time > GapThreshold;
                if (startsRun)
                {
                    if (last is { } previous) ac.LineTo(new Point(previous.X, _plot.Bottom), false, false);
                    lc.BeginFigure(p, false, false);
                    ac.BeginFigure(new Point(p.X, _plot.Bottom), true, true);
                    ac.LineTo(p, false, false);
                }
                else
                {
                    lc.LineTo(p, true, true);
                    ac.LineTo(p, false, false);
                }
                last = p;
            }
            if (last is { } tail) ac.LineTo(new Point(tail.X, _plot.Bottom), false, false);
        }
        line.Freeze();
        area.Freeze();

        dc.PushClip(new RectangleGeometry(_plot));
        dc.DrawGeometry(LevelGradient(lo, hi, 46), null, area);
        var pen = new Pen(LevelGradient(lo, hi, 255), 2.25) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        dc.DrawGeometry(null, pen, line);
        dc.Pop();

        // With only a few readings, show the individual points so isolated ones are visible too.
        if (end - first <= 80)
        {
            for (var i = first; i < end; i++)
                dc.DrawEllipse(Brushes.White, new Pen(Solid(LevelColor(samples[i].Ppm)), 1.75),
                    new Point(X(samples[i].Time), Y(samples[i].Ppm)), 3, 3);
        }

        // Latest reading marker.
        if (_hover == -1)
        {
            var newest = samples[end - 1];
            dc.DrawEllipse(Solid(LevelColor(newest.Ppm)), new Pen(Brushes.White, 2.5), new Point(X(newest.Time), Y(newest.Ppm)), 5, 5);
        }
        else
        {
            DrawHover(dc, samples[_hover], X(samples[_hover].Time), Y(samples[_hover].Ppm), (t1 - t0).TotalHours > 36);
        }
    }

    private void DrawBand(DrawingContext dc, Func<double, double> y, double lo, double hi, double from, double to, Color color)
    {
        var a = Math.Max(from, lo);
        var b = Math.Min(to, hi);
        if (a >= b) return;
        var top = y(b);
        var bottom = y(a);
        dc.DrawRectangle(Solid(color, 20), null, new Rect(_plot.Left, top, _plot.Width, bottom - top));
    }

    private void DrawTimeAxis(DrawingContext dc, Func<DateTime, double> x, DateTime t0, DateTime t1)
    {
        var span = t1 - t0;
        var maxTicks = Math.Max(2, (int)(_plot.Width / 80));
        var step = TickSteps.Cast<TimeSpan?>().FirstOrDefault(s => span.TotalSeconds / s!.Value.TotalSeconds <= maxTicks) ?? TickSteps[^1];

        // Align ticks to local "round" times (e.g. 14:00, 14:30).
        var n = Math.Ceiling((t0 - t0.Date).TotalSeconds / step.TotalSeconds);
        var tick = t0.Date + TimeSpan.FromSeconds(n * step.TotalSeconds);
        var format = step >= TimeSpan.FromDays(1) ? "d MMM" : span.TotalHours > 36 ? "ddd HH:mm" : "HH:mm";

        for (; tick <= t1; tick += step)
        {
            var px = Math.Round(x(tick)) + 0.5;
            dc.DrawLine(GridPen, new Point(px, _plot.Top), new Point(px, _plot.Bottom));
            var label = Text(tick.ToString(format, CultureInfo.CurrentCulture), 11, AxisText);
            var left = px - label.Width / 2;
            if (left < _plot.Left - 12 || left + label.Width > _plot.Right + 14) continue;
            dc.DrawText(label, new Point(left, _plot.Bottom + 6));
        }
    }

    private void DrawHover(DrawingContext dc, Co2Sample sample, double px, double py, bool includeDay)
    {
        dc.DrawLine(CursorPen, new Point(px, _plot.Top), new Point(px, _plot.Bottom));
        dc.DrawEllipse(Solid(LevelColor(sample.Ppm)), new Pen(Brushes.White, 2.5), new Point(px, py), 6, 6);

        var value = Text($"{sample.Ppm:N0} ppm", 14, InkText, bold: true);
        var when = sample.Time.ToString(includeDay ? "ddd HH:mm" : "HH:mm", CultureInfo.CurrentCulture);
        var detail = Text($"{LevelLabel(sample.Ppm)}  ·  {when}", 11.5, MutedText);

        const double padX = 12, padY = 8;
        var boxW = Math.Max(value.Width, detail.Width) + padX * 2;
        var boxH = value.Height + detail.Height + padY * 2 + 1;
        var boxX = px + 14 + boxW > _plot.Right ? px - 14 - boxW : px + 14;
        var boxY = Math.Clamp(py - boxH / 2, _plot.Top + 2, _plot.Bottom - boxH - 2);

        dc.DrawRoundedRectangle(Brushes.White, TooltipPen, new Rect(boxX, boxY, boxW, boxH), 8, 8);
        dc.DrawText(value, new Point(boxX + padX, boxY + padY));
        dc.DrawText(detail, new Point(boxX + padX, boxY + padY + value.Height + 1));
    }

    /// <summary>Vertical gradient (in absolute coordinates) that paints each part of the line by air-quality level.</summary>
    private LinearGradientBrush LevelGradient(double lo, double hi, byte alpha)
    {
        double Offset(double v) => Math.Clamp((hi - v) / (hi - lo), 0, 1);
        double o1400 = Offset(1400), o1000 = Offset(1000);

        var brush = new LinearGradientBrush
        {
            MappingMode = BrushMappingMode.Absolute,
            StartPoint = new Point(0, _plot.Top),
            EndPoint = new Point(0, _plot.Bottom),
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

    private static Color LevelColor(int ppm) => ppm < 1000 ? Good : ppm < 1400 ? Warn : Bad;
    private static string LevelLabel(int ppm) => ppm < 1000 ? "Good air" : ppm < 1400 ? "Getting stuffy" : "Poor — ventilate";

    private static double NiceStep(double raw)
    {
        foreach (var step in new[] { 25.0, 50, 100, 200, 250, 500, 1000, 2000 })
            if (raw <= step) return step;
        return 2000;
    }

    /// <summary>Index of the first sample at or after <paramref name="time"/>.</summary>
    private static int LowerBound(IReadOnlyList<Co2Sample> samples, DateTime time)
    {
        int lo = 0, hi = samples.Count;
        while (lo < hi)
        {
            var mid = (lo + hi) / 2;
            if (samples[mid].Time < time) lo = mid + 1; else hi = mid;
        }
        return lo;
    }

    private FormattedText Text(string text, double size, Brush brush, bool bold = false) =>
        new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, bold ? Bold : Regular, size, brush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

    private static SolidColorBrush Solid(Color color, byte alpha = 255)
    {
        var brush = new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
        brush.Freeze();
        return brush;
    }
}
