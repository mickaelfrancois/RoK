using Rok.Application.Features.Playlists.Query;
using Rok.Logic.ViewModels.Playlists.Interfaces;

namespace Rok.Logic.ViewModels.Playlists.Services;

public class PlaylistsDataLoader(IMediator mediator, IPlaylistViewModelFactory playlistViewModelFactory, ILogger<PlaylistsDataLoader> logger)
{
    public List<PlaylistViewModel> ViewModels { get; private set; } = [];

    public async Task LoadPlaylistsAsync()
    {
        using (PerfLogger perfLogger = new PerfLogger(logger).Parameters("Playlists loaded"))
        {
            IEnumerable<PlaylistHeaderDto> playlists = await mediator.SendMessageAsync(new GetAllPlaylistsQuery());
            ViewModels = CreatePlaylistsViewModels(playlists.OrderBy(c => c.Name));
        }
    }

    public async Task<PlaylistHeaderDto?> GetPlaylistByIdAsync(long id)
    {
        Result<PlaylistHeaderDto> result = await mediator.SendMessageAsync(new GetPlaylistByIdQuery(id));

        if (result.IsError)
        {
            logger.LogError("Failed to retrieve playlist {Id}: {ErrorMessage}", id, result.Error);
            return null;
        }

        return result.Value;
    }

    public void AddPlaylist(PlaylistHeaderDto playlistDto)
    {
        PlaylistViewModel viewModel = playlistViewModelFactory.Create();
        viewModel.SetData(playlistDto);
        ViewModels.Add(viewModel);
    }

    public void UpdatePlaylist(long id, PlaylistHeaderDto playlistDto)
    {
        PlaylistViewModel? playlistToUpdate = ViewModels.FirstOrDefault(c => c.Playlist.Id == id);

        if (playlistToUpdate != null)
        {
            playlistToUpdate.SetData(playlistDto);
        }
        else
        {
            logger.LogWarning("Playlist {Id} not found for update.", id);
        }
    }

    public void RemovePlaylist(long id)
    {
        PlaylistViewModel? playlistToRemove = ViewModels.FirstOrDefault(c => c.Playlist.Id == id);
        if (playlistToRemove != null)
        {
            ViewModels.Remove(playlistToRemove);
        }
    }

    public void Clear()
    {
        ViewModels.Clear();
    }

    private List<PlaylistViewModel> CreatePlaylistsViewModels(IEnumerable<PlaylistHeaderDto> playlists)
    {
        int capacity = playlists.Count();
        List<PlaylistViewModel> playlistViewModels = new(capacity);

        foreach (PlaylistHeaderDto playlist in playlists)
        {
            PlaylistViewModel playlistViewModel = playlistViewModelFactory.Create();
            playlistViewModel.SetData(playlist);
            playlistViewModels.Add(playlistViewModel);
        }

        return playlistViewModels;
    }
}