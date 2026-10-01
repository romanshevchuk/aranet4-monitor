using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace Aranet4Monitor.Presentation.Tray;

public enum ToastKind
{
    Info, Warning, Danger, Success
}

/// <summary>
/// The app's own notification pop-up, shown bottom-right above the taskbar. Unlike a Windows toast its text size
/// is ours to choose, so it can be big and readable. Click it to open the dashboard; ✕ or the timeout dismisses it.
/// </summary>
public sealed class ToastWindow : Window
{
    private const double CardWidth = 400;
    private const double ShadowMargin = 12;
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(12);

    private readonly DispatcherTimer timer = new() { Interval = Lifetime };
    private bool closing;

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
        var symbol = kind switch
        {
            ToastKind.Danger or ToastKind.Warning => "!",
            ToastKind.Success => "✓",
            _ => "i",
        };
        var font = new FontFamily("Segoe UI Variable Text, Segoe UI");

        var titleText = new TextBlock
        {
            Text = title,
            FontFamily = font,
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 10, 0),
        };
        var bodyText = new TextBlock
        {
            Text = body,
            FontFamily = font,
            FontSize = 14,
            Foreground = new SolidColorBrush(Color.FromRgb(0xD7, 0xE7, 0xFF)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 5, 0, 0),
        };
        var iconText = new TextBlock
        {
            Text = symbol,
            FontFamily = font,
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(0x0F, 0x1B, 0x2E)),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var iconBadge = new Border
        {
            Width = 30,
            Height = 30,
            Margin = new Thickness(14, 14, 0, 0),
            Background = new SolidColorBrush(accent),
            CornerRadius = new CornerRadius(15),
            Child = iconText,
            VerticalAlignment = VerticalAlignment.Top,
        };

        var close = new Button
        {
            Content = "×",
            Width = 26,
            Height = 26,
            Padding = new Thickness(0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = new SolidColorBrush(Color.FromRgb(0xA9, 0xB6, 0xCB)),
            FontSize = 18,
            Cursor = System.Windows.Input.Cursors.Hand,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            ToolTip = "Dismiss",
        };
        close.Click += (_, _) => Dismiss();

        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(close, 1);
        header.Children.Add(titleText);
        header.Children.Add(close);

        var text = new StackPanel { Margin = new Thickness(14, 12, 12, 12) };
        text.Children.Add(header);
        text.Children.Add(bodyText);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(iconBadge, 0);
        Grid.SetColumn(text, 1);
        grid.Children.Add(iconBadge);
        grid.Children.Add(text);

        var card = new Border
        {
            Width = CardWidth,
            Margin = new Thickness(ShadowMargin),
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(Color.FromRgb(0x0F, 0x1B, 0x2E)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x36, 0x50, 0x7A)),
            BorderThickness = new Thickness(1),
            Cursor = System.Windows.Input.Cursors.Hand,
            Effect = new DropShadowEffect { BlurRadius = 16, ShadowDepth = 3, Opacity = 0.3, Color = Colors.Black },
            Child = grid,
        };
        card.MouseLeftButtonUp += (_, _) =>
        {
            Clicked?.Invoke(this, EventArgs.Empty);
            Dismiss();
        };
        card.MouseEnter += (_, _) => timer.Stop();
        card.MouseLeave += (_, _) =>
        {
            if (!closing)
            {
                timer.Start();
            }
        };
        Content = card;

        // Measure now so the window can be placed in the bottom-right corner before it is shown.
        card.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Width = card.DesiredSize.Width;
        Height = card.DesiredSize.Height;
        var work = SystemParameters.WorkArea;
        Left = work.Right - Width - 8;
        Top = work.Bottom - Height - 8;

        timer.Tick += (_, _) => Dismiss();
        Loaded += (_, _) =>
        {
            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
            timer.Start();
        };
        Closed += (_, _) => timer.Stop();
    }

    /// <summary>Fades the pop-up out and closes it. Safe to call more than once.</summary>
    public void Dismiss()
    {
        if (closing)
        {
            return;
        }

        closing = true;
        timer.Stop();
        var fade = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(180));
        fade.Completed += (_, _) => Close();
        BeginAnimation(OpacityProperty, fade);
    }
}
