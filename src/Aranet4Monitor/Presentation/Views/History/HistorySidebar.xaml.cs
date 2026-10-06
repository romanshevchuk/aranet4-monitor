using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using WpfBrush = System.Windows.Media.Brush;

namespace Aranet4Monitor.Presentation.Views.History;

public partial class HistorySidebar : System.Windows.Controls.UserControl
{
    public HistorySidebar()
    {
        InitializeComponent();
    }

    public event RoutedEventHandler? ExportRequested;

    public void ClearZoneShare()
    {
        ZoneShareBar.Children.Clear();
        ZoneShareBar.ColumnDefinitions.Clear();
    }

    public void AddZoneShareSegment(WpfBrush brush, double durationSeconds, string toolTip)
    {
        var column = ZoneShareBar.ColumnDefinitions.Count;
        ZoneShareBar.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(durationSeconds, GridUnitType.Star),
        });

        var segment = new Border
        {
            Background = brush,
            CornerRadius = new CornerRadius(6),
            Margin = new Thickness(1, 0, 1, 0),
            ToolTip = toolTip,
        };
        Grid.SetColumn(segment, column);
        ZoneShareBar.Children.Add(segment);
    }

    public void SetZoneShareText(string text) => ZoneShareText.Text = text;

    public void SetOvernight(string detail) => SetInsightText(OvernightText, "Overnight", detail);

    public void SetAiring(string detail) => SetInsightText(AiringText, "Airing", detail);

    public void SetPeak(string detail) => SetInsightText(PeakText, "Highest", detail);

    public void SetExportEnabled(bool isEnabled) => ExportButton.IsEnabled = isEnabled;

    private void ExportButton_Click(object sender, RoutedEventArgs e) => ExportRequested?.Invoke(sender, e);

    private static void SetInsightText(TextBlock textBlock, string lead, string detail)
    {
        textBlock.Inlines.Clear();
        textBlock.Inlines.Add(new Bold(new Run(lead)));
        textBlock.Inlines.Add(new Run(detail));
    }
}
