using Microsoft.UI.Dispatching;
using Rok.Application.Features.Playlists.PlaylistMenu;
using Rok.Application.Services.Filters;
using Rok.ViewModels.Album;
using Rok.ViewModels.Album.Services;
using Rok.ViewModels.Albums;
using Rok.ViewModels.Albums.Handlers;
using Rok.ViewModels.Albums.Interfaces;
using Rok.ViewModels.Albums.Services;
using Rok.ViewModels.Artist;
using Rok.ViewModels.Artist.Services;
using Rok.ViewModels.Artists;
using Rok.ViewModels.Artists.Handlers;
using Rok.ViewModels.Artists.Interfaces;
using Rok.ViewModels.Artists.Services;
using Rok.ViewModels.Genre;
using Rok.ViewModels.Genre.Services;
using Rok.ViewModels.Listening;
using Rok.ViewModels.Listening.Services;
using Rok.ViewModels.Main;
using Rok.ViewModels.Player;
using Rok.ViewModels.Player.Services;
using Rok.ViewModels.Playlist;
using Rok.ViewModels.Playlist.Services;
using Rok.ViewModels.Playlists;
using Rok.ViewModels.Playlists.Handlers;
using Rok.ViewModels.Playlists.Interfaces;
using Rok.ViewModels.Playlists.Services;
using Rok.ViewModels.Search;
using Rok.ViewModels.Start;
using Rok.ViewModels.Statistics;
using Rok.ViewModels.Track;
using Rok.ViewModels.Track.Services;
using Rok.ViewModels.Tracks;
using Rok.ViewModels.Tracks.Handlers;
using Rok.ViewModels.Tracks.Interfaces;
using Rok.ViewModels.Tracks.Services;

namespace Rok;

public static class DependencyInjection
{
    public static IServiceCollection AddLogic(this IServiceCollection services)
    {
        services.AddSingleton<ResourceLoader>((c) => ResourceLoader.GetForViewIndependentUse());
        services.AddSingleton<NavigationService>();
        services.AddSingleton<PlaylistsSeed>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IPlaylistMenuService, PlaylistMenuService>();
        services.AddSingleton<IFolderResolver, FolderResolver>();
        services.AddSingleton<IBackdropLoader, BackdropLoader>();

        services.AddSingleton<TagsProvider>();

        // Albums ViewModel, services and handlers
        services.AddSingleton<AlbumsViewModel>();
        services.AddTransient<AlbumsDataLoader>();
        services.AddTransient<AlbumsSelectionManager>();
        services.AddTransient<AlbumsStateManager>();
        services.AddTransient<AlbumsPlaybackService>();
        services.AddTransient<AlbumUpdateMessageHandler>();
        services.AddTransient<AlbumImportedMessageHandler>();
        services.AddTransient<IAlbumProvider, AlbumProvider>();
        services.AddTransient<IAlbumLibraryMonitor, AlbumLibraryMonitor>();
        services.AddTransient<AlbumsGroupCategory>();
        services.AddTransient<IAlbumViewModelFactory, AlbumViewModelFactory>();
        services.AddTransient<AlbumsFilter>();

        services.AddKeyedTransient<AlbumsViewModel>("SearchAlbums", (sp, _) =>
        {
            return new AlbumsViewModel(
                        sp.GetRequiredService<TagsProvider>(),
                        sp.GetRequiredService<IAlbumProvider>(),
                        sp.GetRequiredService<IAlbumLibraryMonitor>(),
                        sp.GetRequiredService<AlbumsSelectionManager>(),
                        sp.GetRequiredService<AlbumsStateManager>(),
                        sp.GetRequiredService<AlbumsPlaybackService>(),
                        sp.GetRequiredService<ILogger<AlbumsViewModel>>()
                       );
        });

        // Album detail services (for AlbumViewModel - single album)
        services.AddTransient<AlbumViewModel>();
        services.AddTransient<AlbumDataLoader>();
        services.AddTransient<AlbumPictureService>();
        services.AddTransient<IAlbumPictureService, AlbumPictureService>();
        services.AddTransient<AlbumStatisticsService>();
        services.AddTransient<AlbumEditService>();

        // Genre ViewModel and services
        services.AddTransient<GenreViewModel>();
        services.AddTransient<GenreDataLoader>();
        services.AddTransient<GenreEditService>();

        // Artists ViewModel, services and handlers
        services.AddSingleton<ArtistsViewModel>();
        services.AddTransient<ArtistsDataLoader>();
        services.AddTransient<ArtistsSelectionManager>();
        services.AddTransient<ArtistsStateManager>();
        services.AddTransient<ArtistsPlaybackService>();
        services.AddSingleton<ArtistUpdateMessageHandler>();
        services.AddSingleton<ArtistImportedMessageHandler>();
        services.AddTransient<IArtistProvider, ArtistProvider>();
        services.AddTransient<IArtistLibraryMonitor, ArtistLibraryMonitor>();
        services.AddTransient<ArtistsGroupCategory>();
        services.AddTransient<IArtistViewModelFactory, ArtistViewModelFactory>();
        services.AddTransient<ArtistsFilter>();

        services.AddKeyedTransient<ArtistsViewModel>("SearchArtists", (sp, _) =>
        {
            return new ArtistsViewModel(
                        sp.GetRequiredService<TagsProvider>(),
                        sp.GetRequiredService<IArtistProvider>(),
                        sp.GetRequiredService<IArtistLibraryMonitor>(),
                        sp.GetRequiredService<ArtistsSelectionManager>(),
                        sp.GetRequiredService<ArtistsStateManager>(),
                        sp.GetRequiredService<ArtistsPlaybackService>(),
                        sp.GetRequiredService<IAppOptions>(),
                        sp.GetRequiredService<ILogger<ArtistsViewModel>>()
                       );
        });

        // Artist detail services (for ArtistViewModel - single artist)
        services.AddTransient<ArtistViewModel>();
        services.AddTransient<ArtistDataLoader>();
        services.AddTransient<ArtistPictureService>();
        services.AddTransient<IArtistPictureService, ArtistPictureService>();
        services.AddTransient<ArtistStatisticsService>();
        services.AddTransient<ArtistEditService>();

        // Tracks ViewModel, services and handlers
        services.AddSingleton<TracksViewModel>();
        services.AddTransient<TracksDataLoader>();
        services.AddTransient<TracksSelectionManager>();
        services.AddTransient<TracksStateManager>();
        services.AddTransient<TracksPlaybackService>();
        services.AddSingleton<TrackImportedMessageHandler>();
        services.AddTransient<TracksGroupCategory>();
        services.AddTransient<TracksFilter>();
        services.AddTransient<ITrackProvider, TrackProvider>();
        services.AddTransient<ITrackViewModelFactory, TrackViewModelFactory>();
        services.AddTransient<ITrackLibraryMonitor, TrackLibraryMonitor>();

        services.AddKeyedTransient<TracksViewModel>("SearchTracks", (sp, _) =>
        {
            return new TracksViewModel(
                        sp.GetRequiredService<ITrackProvider>(),
                        sp.GetRequiredService<ITrackLibraryMonitor>(),
                        sp.GetRequiredService<TracksSelectionManager>(),
                        sp.GetRequiredService<TracksStateManager>(),
                        sp.GetRequiredService<TracksPlaybackService>(),
                        sp.GetRequiredService<ILogger<TracksViewModel>>()
                       );
        });

        // Track detail services (for TrackViewModel - single track)
        services.AddTransient<TrackViewModel>();
        services.AddTransient<TrackDetailDataLoader>();
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
        services.AddTransient<IPlaylistViewModelFactory, PlaylistViewModelFactory>();

        // Playlist detail (for PlaylistViewModel - single playlist)
        services.AddTransient<PlaylistViewModel>();
        services.AddTransient<PlaylistDataLoader>();
        services.AddTransient<PlaylistPictureService>();
        services.AddTransient<PlaylistUpdateService>();
        services.AddTransient<PlaylistGenerationService>();

        // Player ViewModel and services
        services.AddSingleton<PlayerViewModel>();
        services.AddSingleton<PlayerDataLoader>();
        services.AddSingleton<PlayerLyricsService>();
        services.AddSingleton<PlayerListenTracker>();
        services.AddSingleton<PlayerTimerManager>();
        services.AddSingleton<PlayerStateManager>((sp) =>
        {
            DispatcherQueue dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            return new PlayerStateManager(dispatcherQueue);
        });


        // Shared message handlers
        services.AddSingleton<LibraryRefreshMessageHandler>();
        services.AddTransient<TagUpdatedMessageHandler>();

        // Other ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddTransient<SearchSuggestionsViewModel>();
        services.AddTransient<SearchViewModel>();
        services.AddTransient<StartViewModel>();
        services.AddTransient<StatisticsViewModel>();


        return services;
    }
}
