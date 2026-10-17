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

namespace Rok.Application.Features.Albums.Merge;

/// <summary>
/// Merges an album into another one: checks every file, then updates the database in one transaction that also records
/// which files are still to write, then writes the files one by one. A resume goes through <see cref="AlbumEditService.ResumeAsync"/>.
/// Every database access stays on the caller context: the application shares a single SQLite connection.
/// </summary>
public class AlbumMergeService(
    IAlbumRepository albumRepository,
    IArtistRepository artistRepository,
    ITrackRepository trackRepository,
    IAlbumMergeRepository albumMergeRepository,
    IPendingTagWriteRepository pendingTagWriteRepository,
    TrackFileTagWriter fileWriter,
    IImport import,
    ILibraryWriteGate libraryWriteGate,
    IAppOptions appOptions,
    ILogger<AlbumMergeService> logger)
{
    /// <summary>
    /// Tells what a merge would do, without changing anything.
    /// </summary>
    /// <param name="sourceId">Id of the album that disappears.</param>
    /// <param name="targetId">Id of the album that stays.</param>
    /// <param name="cancellationToken">Token observed while checking the files.</param>
    /// <returns>The preview, or an error when the merge cannot even be considered.</returns>
    public async Task<Result<AlbumMergePreview>> PreviewAsync(long sourceId, long targetId, CancellationToken cancellationToken)
    {
        Resolution resolution = await ResolveAsync(sourceId, targetId);

        if (resolution.Error is not null)
            return Result<AlbumMergePreview>.Fail(resolution.Error);

        Context context = resolution.Context!;
        bool hasConflicts = context.DiscConflicts.Conflicts.Count > 0;
        Analysis analysis = await AnalyzeAsync(context, hasConflicts, cancellationToken);
        OrphanArtistWarning? orphan = await FindOrphanArtistAsync(context);

        return Result<AlbumMergePreview>.Ok(new AlbumMergePreview(
            sourceId,
            context.Source.Name,
            targetId,
            context.Target.Name,
            context.SourceArtistName,
            context.TargetArtistName,
            context.ArtistChanges,
            context.SourceTracks.Count,
            context.TargetTracks.Count,
            analysis.Writes.Count,
            context.Merge.Conflicts,
            context.DiscConflicts.Conflicts,
            hasConflicts ? context.DiscConflicts.Offset : 0,
            HasSeparateFolders(context),
            orphan,
            analysis.Blockers));
    }

    /// <summary>
    /// Merges the album, in the database and in the tags of its files.
    /// </summary>
    /// <param name="sourceId">Id of the album that disappears.</param>
    /// <param name="targetId">Id of the album that stays.</param>
    /// <param name="shiftDiscs">True to shift the discs of the merged album when track numbers collide; false refuses the merge in that case.</param>
    /// <param name="progress">Receives the progress of the file writing.</param>
    /// <param name="cancellationToken">Token observed between two files.</param>
    /// <returns>The outcome, or an error when the merge is refused before any check.</returns>
    public async Task<Result<AlbumTagWriteOutcome>> MergeAsync(
        long sourceId,
        long targetId,
        bool shiftDiscs,
        IProgress<ArtistRenameProgress>? progress,
        CancellationToken cancellationToken)
    {
        IDisposable? lease = null;

        try
        {
            if (import.UpdateInProgress || !libraryWriteGate.TryEnter(out lease))
                return Result<AlbumTagWriteOutcome>.Ok(Busy(targetId));

            Resolution resolution = await ResolveAsync(sourceId, targetId);

            if (resolution.Error is not null)
                return Result<AlbumTagWriteOutcome>.Fail(resolution.Error);

            Context context = resolution.Context!;
            bool hasConflicts = context.DiscConflicts.Conflicts.Count > 0;

            if (hasConflicts && !shiftDiscs)
                return Result<AlbumTagWriteOutcome>.Fail(new ConflictError("album.merge.conflicts", "Some tracks of the two albums share a disc and a track number."));

            Analysis analysis = await AnalyzeAsync(context, hasConflicts, cancellationToken);

            if (analysis.Blockers.Count > 0)
                return Result<AlbumTagWriteOutcome>.Ok(new AlbumTagWriteOutcome(EArtistRenameState.Blocked, targetId, analysis.Blockers, [], [], []));

            int offset = hasConflicts ? context.DiscConflicts.Offset : 0;

            return Result<AlbumTagWriteOutcome>.Ok(await ApplyAsync(context, analysis, offset, progress, cancellationToken));
        }
        finally
        {
            lease?.Dispose();
        }
    }

    private async Task<AlbumTagWriteOutcome> ApplyAsync(Context context, Analysis analysis, int discOffset, IProgress<ArtistRenameProgress>? progress, CancellationToken cancellationToken)
    {
        AlbumMergeDbChange change = new(
            context.Source.Id,
            context.Target.Id,
            context.Merge.Merged,
            discOffset,
            analysis.Writes.Select(w => new PendingTagWrite(w.Track.Id, w.Fields)).ToList());

        try
        {
            await albumMergeRepository.ApplyAsync(change, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "The database update of the merge of album id {Source} into {Target} failed and was rolled back.", context.Source.Id, context.Target.Id);

            return new AlbumTagWriteOutcome(EArtistRenameState.Failed, context.Target.Id, [], [], [], []);
        }

        List<long> unsaved = [];
        Dictionary<long, TrackEntity> tracks = analysis.Writes.ToDictionary(w => w.Track.Id, w => w.Track);
        List<PlannedFileWrite> toWrite = analysis.Writes.Select(w => new PlannedFileWrite(w.Track, w.Changes)).ToList();

        List<ArtistRenameFailure> failures = await fileWriter.WriteAsync(
            toWrite,
            (id, date, size) => CompleteTrackAsync(tracks[id], date, size, unsaved),
            progress,
            cancellationToken);

        failures.AddRange(analysis.Writes
            .Where(w => unsaved.Contains(w.Track.Id))
            .Select(w => new ArtistRenameFailure(w.Track.MusicFile, ETagWriteReason.IoError)));

        EArtistRenameState state = failures.Count == 0 ? EArtistRenameState.Completed : EArtistRenameState.Partial;
        List<long> artistIds = [context.Source.ArtistId!.Value, context.Target.ArtistId!.Value];

        return new AlbumTagWriteOutcome(state, context.Target.Id, [], failures, artistIds.Distinct().ToList(), [context.Source.Id]);
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

    private static AlbumTagWriteOutcome Busy(long targetId)
    {
        ArtistRenameBlocker busy = new(string.Empty, EArtistRenameBlock.ImportInProgress, null);

        return new AlbumTagWriteOutcome(EArtistRenameState.Blocked, targetId, [busy], [], [], []);
    }

    private async Task<Resolution> ResolveAsync(long sourceId, long targetId)
    {
        if (!appOptions.WriteTagsEnabled)
            return Resolution.Fail(new OperationError("album.merge.disabled", "Writing tags is turned off."));

        if (sourceId == targetId)
            return Resolution.Fail(new OperationError("album.merge.same_album", "An album cannot be merged into itself."));

        AlbumEntity? source = await albumRepository.GetByIdAsync(sourceId);

        if (source is null)
            return Resolution.Fail(NotFoundError.ForEntity("Album", sourceId));

        AlbumEntity? target = await albumRepository.GetByIdAsync(targetId);

        if (target is null)
            return Resolution.Fail(NotFoundError.ForEntity("Album", targetId));

        if (source.IsCompilation || target.IsCompilation)
            return Resolution.Fail(new OperationError("album.merge.compilation", "A compilation cannot be merged."));

        if (source.ArtistId is null || target.ArtistId is null)
            return Resolution.Fail(new OperationError("album.merge.no_artist", "An album without artist cannot be merged."));

        if (await pendingTagWriteRepository.CountByAlbumAsync(sourceId) > 0 || await pendingTagWriteRepository.CountByAlbumAsync(targetId) > 0)
            return Resolution.Fail(new OperationError("album.merge.pending", "Some files of an album are still waiting for a write: resume it first."));

        List<ArtistEntity> artists = (await artistRepository.GetAllAsync()).ToList();
        ArtistEntity? sourceArtist = artists.FirstOrDefault(a => a.Id == source.ArtistId);
        ArtistEntity? targetArtist = artists.FirstOrDefault(a => a.Id == target.ArtistId);

        if (!string.IsNullOrEmpty(sourceArtist?.RenamePendingFrom) || !string.IsNullOrEmpty(targetArtist?.RenamePendingFrom))
            return Resolution.Fail(new OperationError("album.merge.artist_pending_rename", "An artist involved in the merge has a rename still pending."));

        List<TrackEntity> sourceTracks = (await trackRepository.GetByAlbumIdAsync(sourceId)).ToList();
        List<TrackEntity> targetTracks = (await trackRepository.GetByAlbumIdAsync(targetId)).ToList();

        Context context = new(
            source,
            target,
            sourceTracks,
            targetTracks,
            AlbumMergeRules.Merge(target, source),
            AlbumDiscConflicts.Find(targetTracks, sourceTracks),
            sourceArtist?.Name ?? source.ArtistName,
            targetArtist?.Name ?? target.ArtistName,
            source.ArtistId != target.ArtistId,
            sourceArtist);

        return Resolution.Ok(context);
    }

    private async Task<Analysis> AnalyzeAsync(Context context, bool shiftDiscs, CancellationToken cancellationToken)
    {
        List<ArtistRenameBlocker> blockers = [];

        if (import.UpdateInProgress)
            blockers.Add(new ArtistRenameBlocker(string.Empty, EArtistRenameBlock.ImportInProgress, null));

        TrackFileAnalysis files = await fileWriter.AnalyzeAsync(
            context.SourceTracks,
            context.TargetTracks,
            cancellationToken,
            (track, check) => FindOutOfSync(context, track, check.Current));

        blockers.AddRange(files.Blockers);

        int offset = shiftDiscs ? context.DiscConflicts.Offset : 0;
        List<PlannedTrackFields> writes = [];

        foreach (CheckedTrackFile file in files.Files)
        {
            PlannedTrackFields? write = PlanFile(context, file, offset);

            if (write is not null)
                writes.Add(write);
        }

        return new Analysis(writes, blockers);
    }

    private static PlannedTrackFields? PlanFile(Context context, CheckedTrackFile file, int offset)
    {
        TrackEntity track = file.Track;
        TagFieldValues? current = file.Check.Current;
        ETagWriteFields fields = ETagWriteFields.None;
        string? album = null;
        string? artist = null;
        int? disc = null;

        if (current is null || !AlbumNameRules.HasSameKey(current.Album, context.Target.Name))
        {
            fields |= ETagWriteFields.Album;
            album = context.Target.Name;
        }

        if (context.ArtistChanges
            && track.ArtistId == context.Source.ArtistId
            && (current is null || !ArtistNameRules.HasSameKey(current.Artist, context.TargetArtistName)))
        {
            fields |= ETagWriteFields.Artist;
            artist = context.TargetArtistName;
        }

        if (offset > 0)
        {
            int newDisc = (track.Disc is > 0 ? track.Disc.Value : 1) + offset;

            if (current?.Disc != newDisc)
            {
                fields |= ETagWriteFields.Disc;
                disc = newDisc;
            }
        }

        if (fields == ETagWriteFields.None)
            return null;

        return new PlannedTrackFields(track, fields, new TagChanges { Album = album, Artist = artist, Disc = disc });
    }

    private static ArtistRenameBlocker? FindOutOfSync(Context context, TrackEntity track, TagFieldValues? current)
    {
        if (current is null)
            return null;

        if (!AlbumNameRules.HasSameKey(current.Album, context.Source.Name) && !AlbumNameRules.HasSameKey(current.Album, context.Target.Name))
            return new ArtistRenameBlocker(track.MusicFile, EArtistRenameBlock.OutOfSync, current.Album);

        return null;
    }

    private static bool HasSeparateFolders(Context context)
    {
        string target = NormalizeFolder(context.Target.AlbumPath);

        return context.SourceTracks.Any(t => !string.Equals(NormalizeFolder(Path.GetDirectoryName(t.MusicFile)), target, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeFolder(string? path)
    {
        return (path ?? string.Empty).Replace('/', '\\').TrimEnd('\\');
    }

    private async Task<OrphanArtistWarning?> FindOrphanArtistAsync(Context context)
    {
        if (!context.ArtistChanges || context.SourceArtist is null)
            return null;

        IEnumerable<TrackEntity> tracks = await trackRepository.GetByArtistIdAsync(context.SourceArtist.Id);

        if (tracks.Any(t => t.AlbumId != context.Source.Id))
            return null;

        return new OrphanArtistWarning(context.SourceArtist.Id, context.SourceArtist.Name, context.SourceArtist.IsFavorite, context.SourceArtist.ListenCount);
    }

    private sealed record Context(
        AlbumEntity Source,
        AlbumEntity Target,
        IReadOnlyList<TrackEntity> SourceTracks,
        IReadOnlyList<TrackEntity> TargetTracks,
        AlbumMergeResult Merge,
        AlbumDiscConflictsResult DiscConflicts,
        string SourceArtistName,
        string TargetArtistName,
        bool ArtistChanges,
        ArtistEntity? SourceArtist);

    private sealed record Analysis(IReadOnlyList<PlannedTrackFields> Writes, IReadOnlyList<ArtistRenameBlocker> Blockers);

    private readonly record struct Resolution(Context? Context, Error? Error)
    {
        public static Resolution Ok(Context context) => new(context, null);

        public static Resolution Fail(Error error) => new(null, error);
    }
}