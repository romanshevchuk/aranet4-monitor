using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace Aranet4Monitor.Presentation.Tray;

public enum ToastKind { Info, Warning, Danger, Success }

/// <summary>
/// The app's own notification pop-up, shown bottom-right above the taskbar. Unlike a Windows toast its text size
/// is ours to choose, so it can be big and readable. Click it to open the dashboard; ✕ or the timeout dismisses it.
/// </summary>
public sealed class ToastWindow : Window
{
    private const double CardWidth = 440;
    private const double ShadowMargin = 16;
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(12);

    private readonly DispatcherTimer _timer = new() { Interval = Lifetime };
    private bool _closing;

    public event EventHandler? Clicked;

    public ToastWindow(string title, string body, ToastKind kind)
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;
        Opacity = 0;

        var accent = kind switch
        {
            ToastKind.Danger => Color.FromRgb(0xEF, 0x5B, 0x5B),
            ToastKind.Warning => Color.FromRgb(0xF5, 0xB9, 0x42),
            ToastKind.Success => Color.FromRgb(0x2E, 0xC2, 0x7E),
            _ => Color.FromRgb(0x5B, 0x9B, 0xFF),
        };
        var font = new FontFamily("Segoe UI Variable Text, Segoe UI");

        var titleText = new TextBlock
        {
            Text = title,
            FontFamily = font,
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 28, 0),
        };
        var bodyText = new TextBlock
        {
            Text = body,
            FontFamily = font,
            FontSize = 16,
            Foreground = new SolidColorBrush(Color.FromRgb(0xD7, 0xE7, 0xFF)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 6, 0, 0),
        };
        var hint = new TextBlock
        {
            Text = "Click to open Aranet4 Monitor",
            FontFamily = font,
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(0x8F, 0xA9, 0xD0)),
            Margin = new Thickness(0, 10, 0, 0),
        };
        var text = new StackPanel { Margin = new Thickness(18, 14, 16, 14) };
        text.Children.Add(titleText);
        text.Children.Add(bodyText);
        text.Children.Add(hint);

        var close = new TextBlock
        {
            Text = "✕",
            FontSize = 14,
            Foreground = new SolidColorBrush(Color.FromRgb(0x8F, 0xA9, 0xD0)),
            Margin = new Thickness(0, 10, 12, 0),
            Cursor = System.Windows.Input.Cursors.Hand,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            VerticalAlignment = System.Windows.VerticalAlignment.Top,
            ToolTip = "Dismiss",
        };
        close.MouseLeftButtonUp += (_, e) => { e.Handled = true; Dismiss(); };

        var accentBar = new Border
        {
            Width = 6,
            Background = new SolidColorBrush(accent),
            CornerRadius = new CornerRadius(14, 0, 0, 14),
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(text, 1);
        Grid.SetColumn(close, 1);
        grid.Children.Add(accentBar);
        grid.Children.Add(text);
        grid.Children.Add(close);

        var card = new Border
        {
            Width = CardWidth,
            Margin = new Thickness(ShadowMargin),
            CornerRadius = new CornerRadius(14),
            Background = new LinearGradientBrush(Color.FromRgb(0x1E, 0x3C, 0x66), Color.FromRgb(0x14, 0x25, 0x44), 45),
            BorderBrush = new SolidColorBrush(Color.FromArgb(90, 0x89, 0xB4, 0xFF)),
            BorderThickness = new Thickness(1),
            Cursor = System.Windows.Input.Cursors.Hand,
            Effect = new DropShadowEffect { BlurRadius = 18, ShadowDepth = 3, Opacity = 0.35, Color = Colors.Black },
            Child = grid,
        };
        card.MouseLeftButtonUp += (_, _) => { Clicked?.Invoke(this, EventArgs.Empty); Dismiss(); };
        card.MouseEnter += (_, _) => _timer.Stop();
        card.MouseLeave += (_, _) => { if (!_closing) _timer.Start(); };
        Content = card;

        // Measure now so the window can be placed in the bottom-right corner before it is shown.
        card.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Width = card.DesiredSize.Width;
        Height = card.DesiredSize.Height;
        var work = SystemParameters.WorkArea;
        Left = work.Right - Width - 8;
        Top = work.Bottom - Height - 8;

        _timer.Tick += (_, _) => Dismiss();
        Loaded += (_, _) =>
        {
            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
            _timer.Start();
        };
        Closed += (_, _) => _timer.Stop();
    }

    /// <summary>Fades the pop-up out and closes it. Safe to call more than once.</summary>
    public void Dismiss()
    {
        if (_closing) return;
        _closing = true;
        _timer.Stop();
        var fade = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(180));
        fade.Completed += (_, _) => Close();
        BeginAnimation(OpacityProperty, fade);
    }
}
