using System.Windows.Controls;

namespace Aranet4Monitor.Presentation.Views;

public partial class SettingsPage : System.Windows.Controls.UserControl
{
    public SettingsPage()
    {
        InitializeComponent();
    }

    public void ScrollToTop() => SettingsScrollViewer.ScrollToTop();
}
