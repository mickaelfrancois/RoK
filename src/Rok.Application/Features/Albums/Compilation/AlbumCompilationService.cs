using CleanArch.DevKit.Mediator.Results;
using Microsoft.Extensions.Logging;
using Rok.Application.Errors;
using Rok.Application.Features.Albums.Edit;
using Rok.Application.Features.Artists.Rename;
using Rok.Application.Features.Tags;
using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Repositories;
using Rok.Application.Tag;
using Rok.Domain.Entities;
using Rok.Domain.Enums;

namespace Rok.Application.Features.Albums.Compilation;

/// <summary>
/// Sets by hand whether an album is a compilation. The choice is stored with the origin Manual so a rescan never undoes it.
/// When writing tags is on, every file is checked first, then the database is updated in one transaction that also records
/// which files are still to write, then the files are written one by one.
/// Every database access stays on the caller context: the application shares a single SQLite connection.
/// </summary>
public class AlbumCompilationService(
    IAlbumRepository albumRepository,
    ITrackRepository trackRepository,
    IAlbumCompilationRepository albumCompilationRepository,
    IPendingTagWriteRepository pendingTagWriteRepository,
    TrackFileTagWriter fileWriter,
    IImport import,
    ILibraryWriteGate libraryWriteGate,
    IAppOptions appOptions,
    ILogger<AlbumCompilationService> logger)
{
    /// <summary>
    /// Sets the compilation status of an album.
    /// </summary>
    /// <param name="albumId">Id of the album.</param>
    /// <param name="isCompilation">The status asked by the user.</param>
    /// <param name="progress">Receives the progress of the file writing.</param>
    /// <param name="cancellationToken">Token observed between two files.</param>
    /// <returns>The outcome, or an error when the change is refused before any check.</returns>
    public async Task<Result<AlbumTagWriteOutcome>> SetAsync(long albumId, bool isCompilation, IProgress<ArtistRenameProgress>? progress, CancellationToken cancellationToken)
    {
        IDisposable? lease = null;

        try
        {
            if (import.UpdateInProgress || !libraryWriteGate.TryEnter(out lease))
                return Result<AlbumTagWriteOutcome>.Ok(Busy(albumId));

            AlbumEntity? album = await albumRepository.GetByIdAsync(albumId);

            if (album is null)
                return Result<AlbumTagWriteOutcome>.Fail(NotFoundError.ForEntity("Album", albumId));

            if (await pendingTagWriteRepository.CountByAlbumAsync(albumId, cancellationToken) > 0)
                return Result<AlbumTagWriteOutcome>.Fail(new OperationError("album.edit.pending", "Some files of the album are still waiting for a write: resume it first."));

            if (album.IsCompilation == isCompilation)
                return Result<AlbumTagWriteOutcome>.Fail(new OperationError("album.compilation.unchanged", "The album already has this compilation status."));

            List<TrackEntity> tracks = (await trackRepository.GetByAlbumIdAsync(albumId)).ToList();
            long? artistId = album.ArtistId ?? FindSingleArtist(tracks);

            if (!isCompilation && artistId is null)
                return Result<AlbumTagWriteOutcome>.Fail(new OperationError("album.compilation.no_artist", "The album has no artist: it cannot stop being a compilation."));

            AlbumEntity? collision = await FindCollisionAsync(album, isCompilation, artistId);

            if (collision is not null)
                return Result<AlbumTagWriteOutcome>.Fail(new ConflictError("album.compilation.collision", $"An album named '{collision.Name}' would be merged with this one."));

            IReadOnlyList<long> artistIds = artistId is long id ? [id] : [];

            if (!appOptions.WriteTagsEnabled)
            {
                if (!await TryApplyAsync(album, isCompilation, artistId, [], cancellationToken))
                    return Result<AlbumTagWriteOutcome>.Ok(new AlbumTagWriteOutcome(EArtistRenameState.Failed, albumId, [], [], [], []));

                return Result<AlbumTagWriteOutcome>.Ok(new AlbumTagWriteOutcome(EArtistRenameState.Completed, albumId, [], [], artistIds, []));
            }

            TrackFileAnalysis analysis = await fileWriter.AnalyzeAsync(tracks, [], cancellationToken);

            if (analysis.Blockers.Count > 0)
                return Result<AlbumTagWriteOutcome>.Ok(new AlbumTagWriteOutcome(EArtistRenameState.Blocked, albumId, analysis.Blockers, [], [], []));

            List<PendingTagWrite> pending = analysis.Files.Select(f => new PendingTagWrite(f.Track.Id, ETagWriteFields.Compilation)).ToList();

            if (!await TryApplyAsync(album, isCompilation, artistId, pending, cancellationToken))
                return Result<AlbumTagWriteOutcome>.Ok(new AlbumTagWriteOutcome(EArtistRenameState.Failed, albumId, [], [], [], []));

            return Result<AlbumTagWriteOutcome>.Ok(await WriteFilesAsync(album, analysis, isCompilation, artistIds, progress, cancellationToken));
        }
        finally
        {
            lease?.Dispose();
        }
    }

    private async Task<bool> TryApplyAsync(AlbumEntity album, bool isCompilation, long? artistId, IReadOnlyList<PendingTagWrite> pending, CancellationToken cancellationToken)
    {
        try
        {
            await albumCompilationRepository.ApplyAsync(new AlbumCompilationDbChange(album.Id, isCompilation, artistId, album.GenreId, pending), cancellationToken);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "The database update of the compilation status of album id {Id} failed and was rolled back.", album.Id);

            return false;
        }
    }

    private async Task<AlbumTagWriteOutcome> WriteFilesAsync(
        AlbumEntity album,
        TrackFileAnalysis analysis,
        bool isCompilation,
        IReadOnlyList<long> artistIds,
        IProgress<ArtistRenameProgress>? progress,
        CancellationToken cancellationToken)
    {
        Dictionary<long, TrackEntity> tracks = analysis.Files.ToDictionary(f => f.Track.Id, f => f.Track);
        List<long> unsaved = [];
        List<PlannedFileWrite> writes = analysis.Files
            .Select(f => new PlannedFileWrite(f.Track, new TagChanges { IsCompilation = isCompilation }))
            .ToList();

        List<ArtistRenameFailure> failures = await fileWriter.WriteAsync(writes, (id, date, size) => CompleteTrackAsync(tracks[id], date, size, unsaved), progress, cancellationToken);

        failures.AddRange(tracks.Values.Where(t => unsaved.Contains(t.Id)).Select(t => new ArtistRenameFailure(t.MusicFile, ETagWriteReason.IoError)));

        EArtistRenameState state = failures.Count == 0 ? EArtistRenameState.Completed : EArtistRenameState.Partial;

        return new AlbumTagWriteOutcome(state, album.Id, [], failures, artistIds, []);
    }

    private async Task CompleteTrackAsync(TrackEntity track, DateTimeOffset? fileDate, long? size, List<long> unsaved)
    {
        try
        {
            await pendingTagWriteRepository.CompleteAsync(track.Id, fileDate?.DateTime ?? track.FileDate, size ?? track.Size);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Tags written in the file of track id {Id} but its pending write could not be cleared.", track.Id);

            unsaved.Add(track.Id);
        }
    }

    private async Task<AlbumEntity?> FindCollisionAsync(AlbumEntity album, bool isCompilation, long? artistId)
    {
        if (isCompilation)
            return (await albumRepository.GetAllAsync()).FirstOrDefault(a => a.Id != album.Id && a.IsCompilation && AlbumNameRules.HasSameKey(a.Name, album.Name));

        IEnumerable<AlbumEntity> albums = (await albumRepository.GetByArtistIdAsync(artistId!.Value)).OfType<AlbumEntity>();

        return AlbumNameRules.FindCollision(albums, album.Id, album.Name);
    }

    private static long? FindSingleArtist(List<TrackEntity> tracks)
    {
        long[] artistIds = tracks.Where(t => t.ArtistId.HasValue).Select(t => t.ArtistId!.Value).Distinct().ToArray();

        return artistIds.Length == 1 ? artistIds[0] : null;
    }

    private static AlbumTagWriteOutcome Busy(long albumId)
    {
        ArtistRenameBlocker busy = new(string.Empty, EArtistRenameBlock.ImportInProgress, null);

        return new AlbumTagWriteOutcome(EArtistRenameState.Blocked, albumId, [busy], [], [], []);
    }
}