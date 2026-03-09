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
        if (args.Item is ArtistViewModel item && item.Picture == null)
            item.LoadPicture();
        else if (args.Item is AlbumViewModel album && album.Picture == null)
            album.LoadPicture();
    }

    private void gridBottom_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        Grid? gridItem = sender as Grid;
        if (gridItem != null && gridItem.DataContext is ArtistViewModel artistViewModel)
        {
            Storyboard? showGenreStoryboard = gridItem.Resources["ShowGenreNameStoryboard"] as Storyboard;
            showGenreStoryboard?.Begin();

            if (!artistViewModel.IsFavorite)
            {
                Storyboard? showFavoriteButtonStoryboard = gridItem.Resources["ShowFavoriteButtonStoryboard"] as Storyboard;
                showFavoriteButtonStoryboard?.Begin();
            }
        }

        if (gridItem != null && gridItem.DataContext is AlbumViewModel albumViewModel)
        {
            Storyboard? showArtistStoryboard = gridItem.Resources["ShowAlbumArtistNameStoryboard"] as Storyboard;
            showArtistStoryboard?.Begin();

            if (!albumViewModel.IsFavorite)
            {
                Storyboard? showFavoriteButtonStoryboard = gridItem.Resources["ShowAlbumFavoriteButtonStoryboard"] as Storyboard;
                showFavoriteButtonStoryboard?.Begin();
            }
        }
    }


    private void gridBottom_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        Grid? gridItem = sender as Grid;
        if (gridItem != null && gridItem.DataContext is ArtistViewModel artistViewModel)
        {
            Storyboard? showSubTitleStoryboard = gridItem.Resources["ShowSubTitleStoryboard"] as Storyboard;
            showSubTitleStoryboard?.Begin();

            if (!artistViewModel.IsFavorite)
            {
                Storyboard? hideFavoriteButtonStoryboard = gridItem.Resources["HideFavoriteButtonStoryboard"] as Storyboard;
                hideFavoriteButtonStoryboard?.Begin();
            }
        }

        if (gridItem != null && gridItem.DataContext is AlbumViewModel albumViewModel)
        {
            Storyboard? showSubTitleStoryboard = gridItem.Resources["ShowAlbumSubTitleStoryboard"] as Storyboard;
            showSubTitleStoryboard?.Begin();

            if (!albumViewModel.IsFavorite)
            {
                Storyboard? hideFavoriteButtonStoryboard = gridItem.Resources["HideAlbumFavoriteButtonStoryboard"] as Storyboard;
                hideFavoriteButtonStoryboard?.Begin();
            }
        }
    }
}
