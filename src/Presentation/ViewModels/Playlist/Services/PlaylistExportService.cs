using System.Threading;
using Microsoft.Extensions.Logging;
using Rok.Application.Dto;
using Rok.Application.Features.Playlists.Requests;
using Rok.Application.Messages;
using Rok.Shared.Enums;

namespace Rok.ViewModels.Playlist.Services;

public sealed class PlaylistExportService(
    IMediator _mediator,
    IPlaylistExportPrompts _prompts,
    IMessenger _messenger,
    ILogger<PlaylistExportService> _logger)
{
    public async Task RunAsync(PlaylistHeaderDto playlist, CancellationToken cancellationToken)
    {
        if (playlist.IsSmart)
        {
            bool proceed = await _prompts.ConfirmSmartPlaylistExportAsync();
            if (!proceed)
                return;
        }

        string? path = await _prompts.PickSavePathAsync($"{playlist.Name}.m3u8");
        if (string.IsNullOrEmpty(path))
            return;

        Result result = await _mediator.Send(new ExportPlaylistRequest(playlist.Id, path), cancellationToken);

        if (result.IsSuccess)
        {
            _messenger.Send(new ShowNotificationMessage { Message = "Playlist exportée", Type = NotificationType.Success });
        }
        else
        {
            _logger.LogError("Export failed for playlist {Id}: {Error}", playlist.Id, result.Errors[0]);
            _messenger.Send(new ShowNotificationMessage { Message = "Échec de l'export", Type = NotificationType.Error });
        }
    }
}
