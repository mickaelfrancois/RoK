using Microsoft.UI.Xaml.Controls;
using Rok.Commons;
using Rok.Logic.ViewModels.Listening;


namespace Rok.Pages;

public sealed partial class ListeningPage : Page, IDisposable
{
    public ListeningViewModel ViewModel { get; set; }

    private readonly BaseScrollingPage _scrollingPage;


    public ListeningPage()
    {
        this.InitializeComponent();

        ViewModel = App.ServiceProvider.GetRequiredService<ListeningViewModel>();
        DataContext = ViewModel;

        _scrollingPage = new BaseScrollingPage(this, null, tracksList, headerRow, pictureColumn);
    }


    public void Dispose()
    {
        _scrollingPage?.Dispose();
    }
}
