using Microsoft.UI.Dispatching;
using Rok.Application.Features.Playlists.PlaylistMenu;
using Rok.Logic.Services.Player;
using Rok.Logic.ViewModels.Album.Services;
using Rok.Logic.ViewModels.Albums;
using Rok.Logic.ViewModels.Albums.Handlers;
using Rok.Logic.ViewModels.Albums.Interfaces;
using Rok.Logic.ViewModels.Albums.Services;
using Rok.Logic.ViewModels.Artist.Services;
using Rok.Logic.ViewModels.Artists;
using Rok.Logic.ViewModels.Artists.Handlers;
using Rok.Logic.ViewModels.Artists.Interfaces;
using Rok.Logic.ViewModels.Artists.Services;
using Rok.Logic.ViewModels.Genre;
using Rok.Logic.ViewModels.Genre.Services;
using Rok.Logic.ViewModels.Listening;
using Rok.Logic.ViewModels.Listening.Services;
using Rok.Logic.ViewModels.Main;
using Rok.Logic.ViewModels.Player;
using Rok.Logic.ViewModels.Player.Services;
using Rok.Logic.ViewModels.Playlist.Services;
using Rok.Logic.ViewModels.Playlists;
using Rok.Logic.ViewModels.Playlists.Handlers;
using Rok.Logic.ViewModels.Playlists.Services;
using Rok.Logic.ViewModels.Search;
using Rok.Logic.ViewModels.Start;
using Rok.Logic.ViewModels.Statistics;
using Rok.Logic.ViewModels.Track.Services;
using Rok.Logic.ViewModels.Tracks;
using Rok.Logic.ViewModels.Tracks.Handlers;
using Rok.Logic.ViewModels.Tracks.Interfaces;
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
