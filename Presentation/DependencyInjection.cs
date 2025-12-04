using Rok.Application.Features.Playlists.PlaylistMenu;
using Rok.Logic.Services.Player;
using Rok.Logic.ViewModels.Albums;
using Rok.Logic.ViewModels.Albums.Handlers;
using Rok.Logic.ViewModels.Albums.Services;
using Rok.Logic.ViewModels.Artists;
using Rok.Logic.ViewModels.Artists.Handlers;
using Rok.Logic.ViewModels.Artists.Services;
using Rok.Logic.ViewModels.Listening;
using Rok.Logic.ViewModels.Main;
using Rok.Logic.ViewModels.Player;
using Rok.Logic.ViewModels.Playlists;
using Rok.Logic.ViewModels.Search;
using Rok.Logic.ViewModels.Start;
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

        // Shared message handlers
        services.AddSingleton<LibraryRefreshMessageHandler>();
        services.AddSingleton<AlbumImportedMessageHandler>();

        services.AddSingleton<MainViewModel>();
        services.AddSingleton<PlayerViewModel>();
        services.AddSingleton<ListeningViewModel>();
        services.AddTransient<SearchViewModel>();
        services.AddTransient<StartViewModel>();

        services.AddSingleton<PlaylistsViewModel>();
        services.AddTransient<PlaylistViewModel>();
        services.AddTransient<ArtistViewModel>();
        services.AddTransient<AlbumViewModel>();
        services.AddTransient<TrackViewModel>();


        return services;
    }
}
