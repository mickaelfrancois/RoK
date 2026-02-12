using Rok.Logic.ViewModels.Playlists.Interfaces;

namespace Rok.Logic.ViewModels.Playlists.Services;

public class PlaylistViewModelFactory(IServiceProvider serviceProvider) : IPlaylistViewModelFactory
{
    public PlaylistViewModel Create()
    {
        return serviceProvider.GetRequiredService<PlaylistViewModel>();
    }
}