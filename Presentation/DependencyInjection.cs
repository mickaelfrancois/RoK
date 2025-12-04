using Microsoft.UI.Dispatching;
using Rok.Application.Features.Playlists.PlaylistMenu;
using Rok.Logic.Services.Player;
using Rok.Logic.ViewModels.Album.Services;
using Rok.Logic.ViewModels.Albums;
using Rok.Logic.ViewModels.Albums.Handlers;
using Rok.Logic.ViewModels.Albums.Services;
using Rok.Logic.ViewModels.Artist.Services;
using Rok.Logic.ViewModels.Artists;
using Rok.Logic.ViewModels.Artists.Handlers;
using Rok.Logic.ViewModels.Artists.Services;
using Rok.Logic.ViewModels.Listening;
using Rok.Logic.ViewModels.Listening.Services;
using Rok.Logic.ViewModels.Main;
using Rok.Logic.ViewModels.Player;
using Rok.Logic.ViewModels.Playlist.Services;
using Rok.Logic.ViewModels.Playlists;
using Rok.Logic.ViewModels.Playlists.Handlers;
using Rok.Logic.ViewModels.Playlists.Services;
using Rok.Logic.ViewModels.Search;
using Rok.Logic.ViewModels.Start;
using Rok.Logic.ViewModels.Track.Services;
using Rok.Logic.ViewModels.Tracks;
using Rok.Logic.ViewModels.Tracks.Handlers;
using Rok.Logic.ViewModels.Tracks.Services;

namespace Rok;

public static class DependencyInjection
{
    public static IServiceCollection AddLogic(this IServiceCollection services)
    {
        services.AddSingleton<ResourceLoader>((c) => ResourceLoader.GetForViewIndependentUse());
        services.AddSingleton<NavigationService>();
        services.AddSingleton<IPlayerService, PlayerService>();
        services.AddSingleton<PlaylistsSeed>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IPlaylistMenuService, PlaylistMenuService>();
        services.AddSingleton<IFolderResolver, FolderResolver>();
        services.AddSingleton<IBackdropLoader, BackdropLoader>();

        // Albums ViewModel, services and handlers
        services.AddSingleton<AlbumsViewModel>();
        services.AddKeyedSingleton<AlbumsViewModel>("SearchAlbums");
        services.AddSingleton<AlbumsDataLoader>();
        services.AddSingleton<AlbumsSelectionManager>();
        services.AddSingleton<AlbumsStateManager>();
        services.AddSingleton<AlbumsPlaybackService>();
        services.AddSingleton<AlbumUpdateMessageHandler>();
        services.AddSingleton<AlbumsGroupCategory>();
        services.AddSingleton<AlbumsFilter>();

        // Album detail services (for AlbumViewModel - single album)
        services.AddTransient<AlbumViewModel>();
        services.AddTransient<AlbumDataLoader>();
        services.AddTransient<AlbumPictureService>();
        services.AddTransient<AlbumApiService>();
        services.AddTransient<AlbumStatisticsService>();
        services.AddTransient<AlbumEditService>();

        // Artists ViewModel, services and handlers
        services.AddSingleton<ArtistsViewModel>();
        services.AddKeyedSingleton<ArtistsViewModel>("SearchArtists");
        services.AddSingleton<ArtistsDataLoader>();
        services.AddSingleton<ArtistsSelectionManager>();
        services.AddSingleton<ArtistsStateManager>();
        services.AddSingleton<ArtistsPlaybackService>();
        services.AddSingleton<ArtistUpdateMessageHandler>();
        services.AddSingleton<ArtistImportedMessageHandler>();
        services.AddSingleton<ArtistsGroupCategory>();
        services.AddSingleton<ArtistsFilter>();

        // Artist detail services (for ArtistViewModel - single artist)
        services.AddTransient<ArtistViewModel>();
        services.AddTransient<ArtistDataLoader>();
        services.AddTransient<ArtistPictureService>();
        services.AddTransient<ArtistApiService>();
        services.AddTransient<ArtistStatisticsService>();
        services.AddTransient<ArtistEditService>();

        // Tracks ViewModel, services and handlers
        services.AddSingleton<TracksViewModel>();
        services.AddKeyedSingleton<TracksViewModel>("SearchTracks");
        services.AddSingleton<TracksDataLoader>();
        services.AddSingleton<TracksSelectionManager>();
        services.AddSingleton<TracksStateManager>();
        services.AddSingleton<TracksPlaybackService>();
        services.AddSingleton<TrackImportedMessageHandler>();
        services.AddSingleton<TracksGroupCategory>();
        services.AddSingleton<TracksFilter>();

        // Track detail services (for TrackViewModel - single track)
        services.AddTransient<TrackViewModel>();
        services.AddTransient<TrackDetailDataLoader>();
        services.AddTransient<TrackLyricsService>();
        services.AddTransient<TrackScoreService>();
        services.AddTransient<TrackNavigationService>();

        // Listening ViewModel and services
        services.AddSingleton<ListeningViewModel>();
        services.AddSingleton<ListeningDataLoader>();
        services.AddSingleton<ListeningPlaylistManager>((sp) =>
        {
            DispatcherQueue dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            ListeningDataLoader dataLoader = sp.GetRequiredService<ListeningDataLoader>();
            return new ListeningPlaylistManager(dispatcherQueue, dataLoader);
        });
        services.AddSingleton<ListeningPlaybackService>();

        // Playlists ViewModel, services and handlers
        services.AddSingleton<PlaylistsViewModel>();
        services.AddSingleton<PlaylistsDataLoader>();
        services.AddSingleton<PlaylistCreationService>();
        services.AddSingleton<PlaylistUpdateMessageHandler>();

        // Playlist detail (for PlaylistViewModel - single playlist)
        services.AddTransient<PlaylistViewModel>();
        services.AddTransient<PlaylistDataLoader>();
        services.AddTransient<PlaylistPictureService>();
        services.AddTransient<PlaylistUpdateService>();
        services.AddTransient<PlaylistGenerationService>();

        // Shared message handlers
        services.AddSingleton<LibraryRefreshMessageHandler>();
        services.AddSingleton<AlbumImportedMessageHandler>();

        services.AddSingleton<MainViewModel>();
        services.AddSingleton<PlayerViewModel>();
        services.AddTransient<SearchViewModel>();
        services.AddTransient<StartViewModel>();


        return services;
    }
}
