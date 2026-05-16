using System.Threading;
using Rok.Application.Features.Playlists;
using Rok.Application.Features.Playlists.Requests;

namespace Rok.ViewModels.Playlists.Services;

public sealed class PlaylistImportService(
    IMediator _mediator,
    IPlaylistFilePickerService _picker,
    ILogger<PlaylistImportService> _logger)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<string> files = await _picker.PickPlaylistFilesAsync();
        if (files.Count == 0)
            return;

        int imported = 0;
        int tracksTotal = 0;
        int ignoredTotal = 0;
        int skipped = 0;
        int failed = 0;

        foreach (string file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Result<PlaylistImportResult> result = await _mediator.Send(new ImportPlaylistRequest(file), cancellationToken);

            if (!result.IsSuccess)
            {
                failed++;
                _logger.LogError("Import failed for {File}: {Error}", file, result.Error);
                continue;
            }

            switch (result.Value!.Status)
            {
                case PlaylistImportStatus.Imported:
                    imported++;
                    tracksTotal += result.Value.MatchedCount;
                    ignoredTotal += result.Value.IgnoredCount;
                    break;

                case PlaylistImportStatus.Skipped:
                    skipped++;
                    break;
            }
        }

        Messenger.Send(BuildToast(imported, tracksTotal, ignoredTotal, skipped, failed));
    }

    private static ShowNotificationMessage BuildToast(int imported, int tracks, int ignored, int skipped, int failed)
    {
        string message = $"{imported} importée(s) — {tracks} piste(s), {ignored} ignorée(s)";

        if (skipped > 0)
            message += $" — {skipped} vide(s) ignorée(s)";

        if (failed > 0)
            message += $" — {failed} en échec";

        NotificationType type = (imported == 0 && (skipped > 0 || failed > 0))
            ? NotificationType.Warning
            : NotificationType.Success;

        return new ShowNotificationMessage { Message = message, Type = type };
    }
}
