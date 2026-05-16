using System.Data;
using Dapper;
using Microsoft.Extensions.Logging;
using MiF.SimpleMessenger;
using Rok.Application.Features.Playlists.IO;
using Rok.Application.Features.Playlists.Messages;
using Rok.Application.Interfaces.Repositories;
using Rok.Shared.Enums;

namespace Rok.Application.Features.Playlists.Requests;

public sealed record ImportPlaylistRequest(string FilePath) : IRequest<Result<PlaylistImportResult>>;

public sealed class ImportPlaylistRequestHandler(
    IPlaylistFormatResolver _resolver,
    ITrackRepository _trackRepository,
    IDbConnection _connection,
    ILogger<ImportPlaylistRequestHandler> _logger)
    : IRequestHandler<ImportPlaylistRequest, Result<PlaylistImportResult>>
{
    private const int NameCollisionHardCap = 999;
    private const string ProbeNameSql = "SELECT 1 FROM playlists WHERE name = @name LIMIT 1";
    private const string InsertHeaderSql =
        "INSERT INTO playlists(name, picture, duration, trackCount, trackMaximum, durationMaximum, groupsJson, type, creatDate) " +
        "VALUES (@Name, '', @Duration, @TrackCount, 0, 0, '', @Type, @CreatDate); SELECT last_insert_rowid();";
    private const string InsertTrackSql =
        "INSERT INTO playlisttracks(playlistId, trackId, position, listened, creatDate) " +
        "VALUES (@PlaylistId, @TrackId, @Position, 0, @CreatDate)";

    public async Task<Result<PlaylistImportResult>> Handle(ImportPlaylistRequest command, CancellationToken cancellationToken)
    {
        string extension = Path.GetExtension(command.FilePath);

        if (!_resolver.TryGetReader(extension, out IPlaylistFormatReader? reader) || reader == null)
        {
            _logger.LogWarning("Unsupported playlist format: {Extension}", extension);
            return Result<PlaylistImportResult>.Fail("UnsupportedFormat");
        }

        PlaylistFileModel model;
        try
        {
            await using FileStream fs = new(command.FilePath, FileMode.Open, FileAccess.Read);
            model = await reader.ReadAsync(fs, Path.GetFileName(command.FilePath), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse playlist {Path}", command.FilePath);
            return Result<PlaylistImportResult>.Fail("ParseError");
        }

        List<(TrackEntity Track, PlaylistFileEntry Entry)> matched = new List<(TrackEntity, PlaylistFileEntry)>();
        int ignored = 0;

        foreach (PlaylistFileEntry entry in model.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            TrackEntity? track = await _trackRepository.GetByFilePathAsync(entry.FilePath, cancellationToken);

            if (track == null)
            {
                ignored++;
                continue;
            }

            matched.Add((track, entry));
        }

        if (matched.Count == 0)
        {
            _logger.LogInformation("Playlist {Name} skipped: 0 tracks matched, {Ignored} ignored", model.Name, ignored);
            return Result<PlaylistImportResult>.Success(new PlaylistImportResult(PlaylistImportStatus.Skipped, null, null, 0, ignored));
        }

        string? finalName = await ResolveFinalNameAsync(model.Name, cancellationToken);

        if (finalName == null)
        {
            _logger.LogError("Name collision exhausted for {Name}", model.Name);
            return Result<PlaylistImportResult>.Fail("NameCollisionExhausted");
        }

        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        long playlistId;
        using (IDbTransaction transaction = _connection.BeginTransaction())
        {
            try
            {
                long totalDuration = matched.Sum(m => m.Track.Duration);
                DateTime now = DateTime.UtcNow;

                playlistId = await _connection.QuerySingleAsync<long>(new CommandDefinition(
                    InsertHeaderSql,
                    new
                    {
                        Name = finalName,
                        Duration = totalDuration,
                        TrackCount = matched.Count,
                        Type = (int)PlaylistType.Classic,
                        CreatDate = now
                    },
                    transaction,
                    cancellationToken: cancellationToken));

                for (int i = 0; i < matched.Count; i++)
                {
                    await _connection.ExecuteAsync(new CommandDefinition(
                        InsertTrackSql,
                        new
                        {
                            PlaylistId = playlistId,
                            TrackId = matched[i].Track.Id,
                            Position = i,
                            CreatDate = now
                        },
                        transaction,
                        cancellationToken: cancellationToken));
                }

                transaction.Commit();
            }
            catch (OperationCanceledException)
            {
                transaction.Rollback();
                throw;
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                _logger.LogError(ex, "Failed to insert playlist {Name}", finalName);
                return Result<PlaylistImportResult>.Fail("DatabaseError");
            }
        }

        Messenger.Send(new PlaylistImportedMessage(playlistId));

        _logger.LogInformation("Imported playlist {Name} (Id={Id}): {Matched} tracks, {Ignored} ignored", finalName, playlistId, matched.Count, ignored);

        return Result<PlaylistImportResult>.Success(new PlaylistImportResult(PlaylistImportStatus.Imported, playlistId, finalName, matched.Count, ignored));
    }

    private async Task<string?> ResolveFinalNameAsync(string baseName, CancellationToken cancellationToken)
    {
        string candidate = baseName;

        for (int suffix = 1; suffix <= NameCollisionHardCap; suffix++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int? hit = await _connection.ExecuteScalarAsync<int?>(new CommandDefinition(ProbeNameSql, new { name = candidate }, cancellationToken: cancellationToken));

            if (hit == null)
                return candidate;

            candidate = $"{baseName} ({suffix + 1})";
        }

        return null;
    }
}
