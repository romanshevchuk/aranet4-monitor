using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using WpfBinding = System.Windows.Data.Binding;
using WpfUserControl = System.Windows.Controls.UserControl;

namespace Aranet4Monitor.Presentation.Views;

[ContentProperty(nameof(PageContent))]
public abstract class ScrollablePage : WpfUserControl
{
    public static readonly DependencyProperty PageContentProperty = DependencyProperty.Register(
        nameof(PageContent),
        typeof(object),
        typeof(ScrollablePage),
        new PropertyMetadata(null));

    private readonly ScrollViewer scrollViewer;

    protected ScrollablePage()
    {
        var contentPresenter = new ContentPresenter();
        contentPresenter.SetBinding(
            ContentPresenter.ContentProperty,
            new WpfBinding(nameof(PageContent)) { Source = this });

        scrollViewer = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = contentPresenter
        };

        Content = scrollViewer;
    }

    public object? PageContent
    {
        get => GetValue(PageContentProperty);
        set => SetValue(PageContentProperty, value);
    }

    public void ScrollToTop() => scrollViewer.ScrollToTop();
}

[ContentProperty(nameof(PageContent))]
public sealed class LivePage : ScrollablePage
{
}

[ContentProperty(nameof(PageContent))]
public sealed class HistoryPage : ScrollablePage
{
}
