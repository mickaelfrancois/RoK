using Microsoft.Extensions.Logging;
using Rok.Application.Dto;
using Rok.Application.Features.Playlists.IO;
using Rok.Application.Features.Tracks.Requests;

namespace Rok.Application.Features.Playlists.Requests;

public sealed record ExportPlaylistRequest(long PlaylistId, string FilePath) : IRequest<Result>;

public sealed class ExportPlaylistRequestHandler(
    IMediator _mediator,
    IPlaylistFormatResolver _resolver,
    ILogger<ExportPlaylistRequestHandler> _logger)
    : IRequestHandler<ExportPlaylistRequest, Result>
{
    public async Task<Result> Handle(ExportPlaylistRequest command, CancellationToken cancellationToken)
    {
        Result<PlaylistHeaderDto> headerResult = await _mediator.Send(new GetPlaylistByIdRequest(command.PlaylistId), cancellationToken);

        if (!headerResult.IsSuccess)
        {
            _logger.LogWarning("Export aborted: playlist {Id} not found", command.PlaylistId);
            return Result.Fail(NotFoundError.ForEntity("Playlist", command.PlaylistId));
        }

        IEnumerable<TrackDto> tracks = await _mediator.Send(new GetTracksByPlaylistIdRequest(command.PlaylistId), cancellationToken);

        List<PlaylistFileEntry> entries = tracks
            .Select(t => new PlaylistFileEntry(
                t.MusicFile,
                t.Title,
                t.ArtistName,
                t.Duration > 0 ? TimeSpan.FromSeconds(t.Duration) : null))
            .ToList();

        PlaylistFileModel model = new(headerResult.Value.Name, entries);

        string extension = Path.GetExtension(command.FilePath);

        if (!_resolver.TryGetWriter(extension, out IPlaylistFormatWriter? writer) || writer == null)
        {
            _logger.LogWarning("Export aborted: unsupported format {Extension}", extension);
            return Result.Fail(new OperationError("playlist.unsupported_format", "Unsupported playlist format."));
        }

        string tempPath = command.FilePath + ".tmp";

        try
        {
            await using (FileStream fs = File.Create(tempPath))
            {
                await writer.WriteAsync(fs, model, cancellationToken);
            }

            File.Move(tempPath, command.FilePath, overwrite: true);
            _logger.LogInformation("Exported playlist {Id} to {Path}", command.PlaylistId, command.FilePath);
            return Result.Ok();
        }
        catch (OperationCanceledException)
        {
            TryDelete(tempPath);
            throw;
        }
        catch (Exception ex)
        {
            TryDelete(tempPath);
            _logger.LogError(ex, "Failed to export playlist {Id} to {Path}", command.PlaylistId, command.FilePath);
            return Result.Fail(new OperationError("playlist.write_error", "Failed to write playlist file."));
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
