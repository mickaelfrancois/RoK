using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Rok.ViewModels.Album;
using Rok.ViewModels.Genre;


namespace Rok.Pages;

public sealed partial class GenrePage : Page
{
    public GenreViewModel ViewModel { get; set; }

    public GenrePage()
    {
        this.InitializeComponent();

        ViewModel = App.ServiceProvider.GetRequiredService<GenreViewModel>();
        DataContext = ViewModel;
    }


    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is not GenreOpenArgs options)
            throw new ArgumentNullException(nameof(options), "GenreOpenArgs cannot be null");

        await ViewModel.LoadDataAsync(options.GenreId);

        base.OnNavigatedTo(e);
    }

    private void grid_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.Item is AlbumViewModel item && item.Picture == null)
        {
            item.LoadPicture();
        }
    }
}
