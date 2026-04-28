using MiF.Mediator.Interfaces;
using Microsoft.Extensions.Logging;
using Rok.Application.Dto;
using Rok.Application.Features.Playlists.IO;
using Rok.Application.Features.Playlists.Query;
using Rok.Application.Features.Tracks.Query;

namespace Rok.Application.Features.Playlists.Command;

public sealed record ExportPlaylistCommand(long PlaylistId, string FilePath) : ICommand<Result>;

public sealed class ExportPlaylistCommandHandler(
    IMediator _mediator,
    IPlaylistFormatResolver _resolver,
    ILogger<ExportPlaylistCommandHandler> _logger)
    : ICommandHandler<ExportPlaylistCommand, Result>
{
    public async Task<Result> HandleAsync(ExportPlaylistCommand command, CancellationToken cancellationToken)
    {
        Result<PlaylistHeaderDto> headerResult = await _mediator.SendMessageAsync(new GetPlaylistByIdQuery(command.PlaylistId), cancellationToken);

        if (!headerResult.IsSuccess)
        {
            _logger.LogWarning("Export aborted: playlist {Id} not found", command.PlaylistId);
            return Result.Fail("PlaylistNotFound");
        }

        IEnumerable<TrackDto> tracks = await _mediator.SendMessageAsync(new GetTracksByPlaylistIdQuery(command.PlaylistId), cancellationToken);

        List<PlaylistFileEntry> entries = tracks
            .Select(t => new PlaylistFileEntry(
                t.MusicFile,
                t.Title,
                t.ArtistName,
                t.Duration > 0 ? TimeSpan.FromSeconds(t.Duration) : null))
            .ToList();

        PlaylistFileModel model = new(headerResult.Value!.Name, entries);

        string extension = Path.GetExtension(command.FilePath);

        if (!_resolver.TryGetWriter(extension, out IPlaylistFormatWriter? writer) || writer == null)
        {
            _logger.LogWarning("Export aborted: unsupported format {Extension}", extension);
            return Result.Fail("UnsupportedFormat");
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
            return Result.Success();
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
            return Result.Fail("WriteError");
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
