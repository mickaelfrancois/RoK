using Rok.ViewModels.Playlist;
using Rok.ViewModels.Playlists.Interfaces;

namespace Rok.ViewModels.Playlists.Services;

public class PlaylistViewModelFactory(IServiceProvider serviceProvider) : IPlaylistViewModelFactory
{
    public PlaylistViewModel Create()
    {
        return serviceProvider.GetRequiredService<PlaylistViewModel>();
    }
}