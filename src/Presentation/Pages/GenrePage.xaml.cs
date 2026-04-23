using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
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

    private void gridBottom_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        Grid? gridItem = sender as Grid;
        if (gridItem != null && gridItem.DataContext is AlbumViewModel albumViewModel)
        {
            Storyboard? showArtistStoryboard = gridItem.Resources["ShowArtistNameStoryboard"] as Storyboard;
            showArtistStoryboard?.Begin();

            if (!albumViewModel.IsFavorite)
            {
                Storyboard? showFavoriteButtonStoryboard = gridItem.Resources["ShowFavoriteButtonStoryboard"] as Storyboard;
                showFavoriteButtonStoryboard?.Begin();
            }
        }
    }

    private void gridBottom_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        Grid? gridItem = sender as Grid;
        if (gridItem != null && gridItem.DataContext is AlbumViewModel albumViewModel)
        {
            Storyboard? showSubTitleStoryboard = gridItem.Resources["ShowSubTitleStoryboard"] as Storyboard;
            showSubTitleStoryboard?.Begin();

            if (!albumViewModel.IsFavorite)
            {
                Storyboard? hideFavoriteButtonStoryboard = gridItem.Resources["HideFavoriteButtonStoryboard"] as Storyboard;
                hideFavoriteButtonStoryboard?.Begin();
            }
        }
    }
}
