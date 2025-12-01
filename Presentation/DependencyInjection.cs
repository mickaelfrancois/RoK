using Rok.Application.Features.Playlists.PlaylistMenu;
using Rok.Logic.Services.Player;
using Rok.Logic.ViewModels.Albums;
using Rok.Logic.ViewModels.Artists;
using Rok.Logic.ViewModels.Listening;
using Rok.Logic.ViewModels.Main;
using Rok.Logic.ViewModels.Player;
using Rok.Logic.ViewModels.Playlists;
using Rok.Logic.ViewModels.Search;
using Rok.Logic.ViewModels.Start;
using Rok.Logic.ViewModels.Tracks;

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

        services.AddSingleton<MainViewModel>();
        services.AddSingleton<PlayerViewModel>();
        services.AddSingleton<ListeningViewModel>();
        services.AddTransient<SearchViewModel>();
        services.AddTransient<StartViewModel>();

        services.AddKeyedSingleton<ArtistsViewModel>("SearchArtists");
        services.AddKeyedSingleton<AlbumsViewModel>("SearchAlbums");
        services.AddKeyedSingleton<TracksViewModel>("SearchTracks");

        services.AddSingleton<ArtistsViewModel>();
        services.AddSingleton<AlbumsViewModel>();
        services.AddSingleton<TracksViewModel>();
        services.AddSingleton<PlaylistsViewModel>();

        services.AddTransient<PlaylistViewModel>();
        services.AddTransient<ArtistViewModel>();
        services.AddTransient<AlbumViewModel>();
        services.AddTransient<TrackViewModel>();

        services.AddSingleton<ArtistsGroupCategory>();
        services.AddSingleton<ArtistsFilter>();

        services.AddSingleton<TracksGroupCategory>();
        services.AddSingleton<TracksFilter>();

        services.AddSingleton<AlbumsGroupCategory>();
        services.AddSingleton<AlbumsFilter>();

        return services;
    }
}
