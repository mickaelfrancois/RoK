using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Rok.Logic.ViewModels.Albums;
using Rok.Logic.ViewModels.Artists;
using Rok.Logic.ViewModels.Search;
using Rok.Logic.ViewModels.Tracks;


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
}
