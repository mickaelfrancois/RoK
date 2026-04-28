using Rok.Application.Dto;
using Rok.Application.Features.Playlists.Messages;
using Rok.ViewModels.Playlists.Services;

namespace Rok.ViewModels.Playlists.Handlers;

public sealed class PlaylistImportedMessageHandler(PlaylistsDataLoader _dataLoader, ILogger<PlaylistImportedMessageHandler> _logger)
{
    public event EventHandler? DataChanged;

    public async Task HandleAsync(PlaylistImportedMessage message)
    {
        PlaylistHeaderDto? playlistDto = await _dataLoader.GetPlaylistByIdAsync(message.PlaylistId);
        if (playlistDto == null)
        {
            _logger.LogWarning("Imported playlist {Id} not found in repository", message.PlaylistId);
            return;
        }

        _dataLoader.AddPlaylist(playlistDto);
        DataChanged?.Invoke(this, EventArgs.Empty);
    }
}
