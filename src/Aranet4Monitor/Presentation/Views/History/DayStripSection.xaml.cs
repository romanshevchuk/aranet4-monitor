using System.Windows;
using System.Windows.Controls;

namespace Aranet4Monitor.Presentation.Views.History;

public partial class DayStripSection : System.Windows.Controls.UserControl
{
    public DayStripSection()
    {
        InitializeComponent();
    }

    public void ClearCells() => Cells.Children.Clear();

    public void AddCell(UIElement cell) => Cells.Children.Add(cell);

    public void SetTimeLabels(string start, string quarter, string half, string threeQuarter)
    {
        StartTimeText.Text = start;
        QuarterTimeText.Text = quarter;
        HalfTimeText.Text = half;
        ThreeQuarterTimeText.Text = threeQuarter;
    }
}
