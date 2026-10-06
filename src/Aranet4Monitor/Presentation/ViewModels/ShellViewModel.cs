using Aranet4Monitor.Presentation;

namespace Aranet4Monitor.Presentation.ViewModels;

public sealed class ShellViewModel : ObservableObject
{
    private AppPage currentPage = AppPage.Live;

    public ShellViewModel()
    {
        NavigateCommand = new RelayCommand(
            parameter =>
            {
                if (parameter is AppPage page)
                {
                    CurrentPage = page;
                }
            },
            parameter => parameter is AppPage);
    }

    public AppPage CurrentPage
    {
        get => currentPage;
        private set => SetProperty(ref currentPage, value);
    }

    public RelayCommand NavigateCommand { get; }
}
