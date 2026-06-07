using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Rok.ViewModels.Album;
using Rok.ViewModels.Albums;
using Rok.ViewModels.Artist;
using Rok.ViewModels.Artists;
using Rok.ViewModels.Search;
using Rok.ViewModels.Tracks;


namespace Rok.Pages;

public sealed partial class SearchPage : Page
{
    public SearchViewModel ViewModel { get; set; }

    public AlbumsViewModel AlbumsViewModel { get; set; }

    public ArtistsViewModel ArtistsViewModel { get; set; }

    public TracksViewModel TracksViewModel { get; set; }

    public int TrackCount { get; set; }
    public int ArtistCount { get; set; }
    public int AlbumCount { get; set; }


    public SearchPage()
    {
        InitializeComponent();

        ViewModel = App.ServiceProvider.GetRequiredService<SearchViewModel>();
        DataContext = ViewModel;

        AlbumsViewModel = App.ServiceProvider.GetRequiredKeyedService<AlbumsViewModel>("SearchAlbums");
        ArtistsViewModel = App.ServiceProvider.GetRequiredKeyedService<ArtistsViewModel>("SearchArtists");
        TracksViewModel = App.ServiceProvider.GetRequiredKeyedService<TracksViewModel>("SearchTracks");
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        SearchOpenArgs openArgs = e.Parameter as SearchOpenArgs ?? new SearchOpenArgs();

        TrackCount = openArgs.SearchResult.Tracks.Count;
        ArtistCount = openArgs.SearchResult.Artists.Count;
        AlbumCount = openArgs.SearchResult.Albums.Count;

        ArtistsViewModel.SetData(openArgs.SearchResult.Artists);
        AlbumsViewModel.SetData(openArgs.SearchResult.Albums);
        TracksViewModel.SetData(openArgs.SearchResult.Tracks);
    }

    private void grid_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.InRecycleQueue && args.ItemContainer?.ContentTemplateRoot is Grid root)
        {
            // Release storyboard holds so {x:Bind} values re-apply when the container is reused.
            StopStoryboard(root, "ShowFavoriteButtonStoryboard");
            StopStoryboard(root, "HideFavoriteButtonStoryboard");
            StopStoryboard(root, "ShowAlbumFavoriteButtonStoryboard");
            StopStoryboard(root, "HideAlbumFavoriteButtonStoryboard");
            return;
        }

        if (args.Item is ArtistViewModel item && item.Picture == null)
            item.LoadPicture();
        else if (args.Item is AlbumViewModel album && album.Picture == null)
            album.LoadPicture();
    }

    private void gridBottom_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        Grid? gridItem = sender as Grid;
        if (gridItem != null && gridItem.DataContext is ArtistViewModel)
        {
            (gridItem.Resources["ShowGenreNameStoryboard"] as Storyboard)?.Begin();
            (gridItem.Resources["ShowFavoriteButtonStoryboard"] as Storyboard)?.Begin();
        }

        if (gridItem != null && gridItem.DataContext is AlbumViewModel)
        {
            (gridItem.Resources["ShowAlbumArtistNameStoryboard"] as Storyboard)?.Begin();
            (gridItem.Resources["ShowAlbumFavoriteButtonStoryboard"] as Storyboard)?.Begin();
        }
    }


    private void gridBottom_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        Grid? gridItem = sender as Grid;
        if (gridItem != null && gridItem.DataContext is ArtistViewModel artistViewModel)
        {
            (gridItem.Resources["ShowSubTitleStoryboard"] as Storyboard)?.Begin();

            if (artistViewModel.IsFavorite)
            {
                // Release the animated values so the {x:Bind} on Opacity stays authoritative for favorites.
                StopStoryboard(gridItem, "ShowFavoriteButtonStoryboard");
                StopStoryboard(gridItem, "HideFavoriteButtonStoryboard");
            }
            else
            {
                (gridItem.Resources["HideFavoriteButtonStoryboard"] as Storyboard)?.Begin();
            }
        }

        if (gridItem != null && gridItem.DataContext is AlbumViewModel albumViewModel)
        {
            (gridItem.Resources["ShowAlbumSubTitleStoryboard"] as Storyboard)?.Begin();

            if (albumViewModel.IsFavorite)
            {
                StopStoryboard(gridItem, "ShowAlbumFavoriteButtonStoryboard");
                StopStoryboard(gridItem, "HideAlbumFavoriteButtonStoryboard");
            }
            else
            {
                (gridItem.Resources["HideAlbumFavoriteButtonStoryboard"] as Storyboard)?.Begin();
            }
        }
    }

    private static void StopStoryboard(Grid root, string key)
    {
        if (root.Resources.TryGetValue(key, out object? value) && value is Storyboard storyboard)
            storyboard.Stop();
    }
}