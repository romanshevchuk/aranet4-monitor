using System.Windows;
using System.Windows.Media;

namespace Aranet4Monitor.Presentation.Controls;

/// <summary>Tiny 4-bar signal strength indicator (like a phone's reception icon).</summary>
public sealed class SignalBars : FrameworkElement
{
    private static readonly Brush DefaultActive = Freeze(new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6)));
    private static readonly Brush DefaultInactive = Freeze(new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1)));
    private static readonly double[] Heights = [5, 8, 12, 16];

    public static readonly DependencyProperty BarsProperty = DependencyProperty.Register(
        nameof(Bars), typeof(int), typeof(SignalBars),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ActiveBrushProperty = DependencyProperty.Register(
        nameof(ActiveBrush), typeof(Brush), typeof(SignalBars),
        new FrameworkPropertyMetadata(DefaultActive, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty InactiveBrushProperty = DependencyProperty.Register(
        nameof(InactiveBrush), typeof(Brush), typeof(SignalBars),
        new FrameworkPropertyMetadata(DefaultInactive, FrameworkPropertyMetadataOptions.AffectsRender));

    public int Bars { get => (int)GetValue(BarsProperty); set => SetValue(BarsProperty, value); }
    public Brush ActiveBrush { get => (Brush)GetValue(ActiveBrushProperty); set => SetValue(ActiveBrushProperty, value); }
    public Brush InactiveBrush { get => (Brush)GetValue(InactiveBrushProperty); set => SetValue(InactiveBrushProperty, value); }

    protected override Size MeasureOverride(Size availableSize) => new(22, 16);

    protected override void OnRender(DrawingContext dc)
    {
        const double barWidth = 4, gap = 2;
        for (var i = 0; i < Heights.Length; i++)
        {
            var brush = i < Bars ? ActiveBrush : InactiveBrush;
            var rect = new Rect(i * (barWidth + gap), 16 - Heights[i], barWidth, Heights[i]);
            dc.DrawRoundedRectangle(brush, null, rect, 1.5, 1.5);
        }
    }

    private static Brush Freeze(Brush brush) { brush.Freeze(); return brush; }
}
