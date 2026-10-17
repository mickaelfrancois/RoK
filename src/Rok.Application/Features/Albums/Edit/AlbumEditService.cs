using CleanArch.DevKit.Mediator.Results;
using Microsoft.Extensions.Logging;
using Rok.Application.Errors;
using Rok.Application.Features.Artists.Rename;
using Rok.Application.Features.Tags;
using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Repositories;
using Rok.Application.Tag;
using Rok.Domain.Entities;
using Rok.Domain.Enums;
using Rok.Shared.Extensions;

namespace Rok.Application.Features.Albums.Edit;

/// <summary>
/// Edits the tagged fields of an album (name, artist, year, genre): checks every file, then updates the database in one
/// transaction that also records which files are still to write, then writes the files one by one.
/// A rescan during an incident reads the database values for the files still to write, so it never recreates the old album or artist.
/// Every database access stays on the caller context: the application shares a single SQLite connection.
/// </summary>
public class AlbumEditService(
    IAlbumRepository albumRepository,
    IArtistRepository artistRepository,
    IGenreRepository genreRepository,
    ITrackRepository trackRepository,
    IAlbumEditRepository albumEditRepository,
    IPendingTagWriteRepository pendingTagWriteRepository,
    TrackFileTagWriter fileWriter,
    IImport import,
    ILibraryWriteGate libraryWriteGate,
    IAppOptions appOptions,
    ILogger<AlbumEditService> logger)
{
    /// <summary>
    /// Tells what an edit would do, without changing anything.
    /// </summary>
    /// <param name="albumId">Id of the album.</param>
    /// <param name="name">Album name asked by the user.</param>
    /// <param name="artistName">Artist name asked by the user.</param>
    /// <param name="year">Year asked by the user, null to keep the current one.</param>
    /// <param name="genreName">Genre name asked by the user, null to keep the current one.</param>
    /// <param name="cancellationToken">Token observed while checking the files.</param>
    /// <returns>The preview, or an error when the edit cannot even be considered.</returns>
    public async Task<Result<AlbumEditPreview>> PreviewAsync(long albumId, string name, string artistName, int? year, string? genreName, CancellationToken cancellationToken)
    {
        Resolution resolution = await ResolveAsync(albumId, name, artistName, year, genreName);

        if (resolution.Error is not null)
            return Result<AlbumEditPreview>.Fail(resolution.Error);

        Context context = resolution.Context!;
        Analysis analysis = await AnalyzeAsync(context, cancellationToken);
        AlbumEditPlan plan = analysis.Plan;
        OrphanArtistWarning? orphan = await FindOrphanArtistAsync(context);

        AlbumCollision? collision = context.Collision is null
            ? null
            : new AlbumCollision(context.Collision.Id, context.Collision.Name, context.TargetArtistName, context.Collision.TrackCount);

        return Result<AlbumEditPreview>.Ok(new AlbumEditPreview(
            albumId,
            context.Album.Name,
            context.Name,
            context.Subject.ArtistName,
            context.TargetArtistName,
            context.CreatesArtist,
            context.Album.Year,
            plan.YearChanged ? context.Year : context.Album.Year,
            context.Subject.GenreName,
            context.TargetGenreName,
            context.CreatesGenre,
            plan.Writes.Count,
            plan.GenreTrackCount,
            plan.GenreKeptCount,
            plan.OtherArtistTrackCount,
            collision,
            orphan,
            analysis.Blockers));
    }

    /// <summary>
    /// Edits the album, in the database and in the tags of its files.
    /// </summary>
    /// <param name="albumId">Id of the album.</param>
    /// <param name="name">Album name asked by the user.</param>
    /// <param name="artistName">Artist name asked by the user.</param>
    /// <param name="year">Year asked by the user, null to keep the current one.</param>
    /// <param name="genreName">Genre name asked by the user, null to keep the current one.</param>
    /// <param name="progress">Receives the progress of the file writing.</param>
    /// <param name="cancellationToken">Token observed between two files.</param>
    /// <returns>The outcome, or an error when the edit is refused before any check.</returns>
    public async Task<Result<AlbumTagWriteOutcome>> EditAsync(
        long albumId,
        string name,
        string artistName,
        int? year,
        string? genreName,
        IProgress<ArtistRenameProgress>? progress,
        CancellationToken cancellationToken)
    {
        IDisposable? lease = null;

        try
        {
            if (import.UpdateInProgress || !libraryWriteGate.TryEnter(out lease))
                return Result<AlbumTagWriteOutcome>.Ok(Busy(albumId));

            Resolution resolution = await ResolveAsync(albumId, name, artistName, year, genreName);

            if (resolution.Error is not null)
                return Result<AlbumTagWriteOutcome>.Fail(resolution.Error);

            Context context = resolution.Context!;

            if (context.Collision is not null)
                return Result<AlbumTagWriteOutcome>.Fail(new ConflictError("album.rename.collision", $"An album named '{context.Collision.Name}' already exists."));

            Analysis analysis = await AnalyzeAsync(context, cancellationToken);

            if (analysis.Blockers.Count > 0)
                return Result<AlbumTagWriteOutcome>.Ok(new AlbumTagWriteOutcome(EArtistRenameState.Blocked, albumId, analysis.Blockers, [], [], []));

            return Result<AlbumTagWriteOutcome>.Ok(await ApplyAsync(context, analysis.Plan, progress, cancellationToken));
        }
        finally
        {
            lease?.Dispose();
        }
    }

    /// <summary>
    /// Writes again, from the database values, the files of an album that an earlier edit or merge left half done.
    /// </summary>
    /// <param name="albumId">Id of the album.</param>
    /// <param name="progress">Receives the progress of the file writing.</param>
    /// <param name="cancellationToken">Token observed between two files.</param>
    /// <returns>The outcome, or an error when the album cannot be resumed.</returns>
    public async Task<Result<AlbumTagWriteOutcome>> ResumeAsync(long albumId, IProgress<ArtistRenameProgress>? progress, CancellationToken cancellationToken)
    {
        IDisposable? lease = null;

        try
        {
            if (import.UpdateInProgress || !libraryWriteGate.TryEnter(out lease))
                return Result<AlbumTagWriteOutcome>.Ok(Busy(albumId));

            if (!appOptions.WriteTagsEnabled)
                return Result<AlbumTagWriteOutcome>.Fail(new OperationError("album.edit.disabled", "Writing tags is turned off."));

            AlbumEntity? album = await albumRepository.GetByIdAsync(albumId);

            if (album is null)
                return Result<AlbumTagWriteOutcome>.Fail(NotFoundError.ForEntity("Album", albumId));

            await pendingTagWriteRepository.DeleteOrphansAsync(cancellationToken);

            IReadOnlyList<PendingTagWrite> pending = await pendingTagWriteRepository.GetByAlbumAsync(albumId, cancellationToken);
            IReadOnlyList<long> artistIds = album.ArtistId is long artistId ? [artistId] : [];

            if (pending.Count == 0)
                return Result<AlbumTagWriteOutcome>.Ok(new AlbumTagWriteOutcome(EArtistRenameState.Completed, albumId, [], [], artistIds, []));

            Dictionary<long, ETagWriteFields> fieldsByTrack = pending.ToDictionary(p => p.TrackId, p => p.Fields);
            List<TrackEntity> tracks = (await trackRepository.GetByAlbumIdAsync(albumId)).Where(t => fieldsByTrack.ContainsKey(t.Id)).ToList();
            TrackFileAnalysis analysis = await fileWriter.AnalyzeAsync(tracks, [], cancellationToken);

            if (analysis.Blockers.Count > 0)
                return Result<AlbumTagWriteOutcome>.Ok(new AlbumTagWriteOutcome(EArtistRenameState.Blocked, albumId, analysis.Blockers, [], [], []));

            return Result<AlbumTagWriteOutcome>.Ok(await ResumeFilesAsync(album, analysis, fieldsByTrack, artistIds, progress, cancellationToken));
        }
        finally
        {
            lease?.Dispose();
        }
    }

    private async Task<AlbumTagWriteOutcome> ResumeFilesAsync(
        AlbumEntity album,
        TrackFileAnalysis analysis,
        Dictionary<long, ETagWriteFields> fieldsByTrack,
        IReadOnlyList<long> artistIds,
        IProgress<ArtistRenameProgress>? progress,
        CancellationToken cancellationToken)
    {
        List<PlannedFileWrite> toWrite = [];
        List<long> unsaved = [];

        foreach (CheckedTrackFile file in analysis.Files)
        {
            TagChanges changes = BuildResumeChanges(album, file.Track, fieldsByTrack[file.Track.Id]);

            if (IsAlreadyWritten(changes, file.Check.Current))
            {
                await CompleteTrackAsync(file.Track, file.Check.FileDateModified, file.Check.Size, unsaved);
                continue;
            }

            toWrite.Add(new PlannedFileWrite(file.Track, changes));
        }

        List<ArtistRenameFailure> failures = await WriteAsync(toWrite, unsaved, progress, cancellationToken);

        failures.AddRange(ToFailures(analysis.Files.Select(f => f.Track), unsaved));

        EArtistRenameState state = failures.Count == 0 ? EArtistRenameState.Completed : EArtistRenameState.Partial;

        return new AlbumTagWriteOutcome(state, album.Id, [], failures, artistIds, []);
    }

    private static TagChanges BuildResumeChanges(AlbumEntity album, TrackEntity track, ETagWriteFields fields)
    {
        return new TagChanges
        {
            Album = fields.HasFlag(ETagWriteFields.Album) ? album.Name : null,
            Artist = fields.HasFlag(ETagWriteFields.Artist) && !string.IsNullOrEmpty(track.ArtistName) ? track.ArtistName : null,
            Year = fields.HasFlag(ETagWriteFields.Year) ? album.Year : null,
            Genre = fields.HasFlag(ETagWriteFields.Genre) && !string.IsNullOrEmpty(track.GenreName) ? track.GenreName : null,
            Disc = fields.HasFlag(ETagWriteFields.Disc) ? track.Disc : null
        };
    }

    private static bool IsAlreadyWritten(TagChanges changes, TagFieldValues? current)
    {
        if (current is null)
            return false;

        return (changes.Album is null || string.Equals(current.Album, changes.Album, StringComparison.Ordinal))
            && (changes.Artist is null || string.Equals(current.Artist, changes.Artist, StringComparison.Ordinal))
            && (changes.Genre is null || string.Equals(current.Genre, changes.Genre, StringComparison.Ordinal))
            && (changes.Year is null || current.Year == changes.Year)
            && (changes.Disc is null || current.Disc == changes.Disc);
    }

    private async Task<AlbumTagWriteOutcome> ApplyAsync(Context context, AlbumEditPlan plan, IProgress<ArtistRenameProgress>? progress, CancellationToken cancellationToken)
    {
        AlbumEntity album = context.Album;
        long oldArtistId = album.ArtistId!.Value;

        AlbumEditDbChange change = new(
            album.Id,
            context.Name,
            plan.YearChanged ? context.Year : null,
            oldArtistId,
            plan.ArtistChanged ? context.ExistingArtist?.Id : null,
            plan.ArtistChanged && context.ExistingArtist is null ? context.TargetArtistName : null,
            album.GenreId,
            plan.GenreChanged ? context.ExistingGenre?.Id : null,
            plan.GenreChanged && context.ExistingGenre is null ? context.TargetGenreName : null,
            plan.Writes.Select(w => new PendingTagWrite(w.Track.Id, w.Fields)).ToList());

        AlbumEditApplied applied;

        try
        {
            applied = await albumEditRepository.ApplyAsync(change, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "The database update of the edit of album id {Id} failed and was rolled back.", album.Id);

            return new AlbumTagWriteOutcome(EArtistRenameState.Failed, album.Id, [], [], [], []);
        }

        List<long> unsaved = [];
        List<PlannedFileWrite> toWrite = plan.Writes.Select(w => new PlannedFileWrite(w.Track, w.Changes)).ToList();
        List<ArtistRenameFailure> failures = await WriteAsync(toWrite, unsaved, progress, cancellationToken);

        failures.AddRange(ToFailures(plan.Writes.Select(w => w.Track), unsaved));

        EArtistRenameState state = failures.Count == 0 ? EArtistRenameState.Completed : EArtistRenameState.Partial;
        List<long> artistIds = [oldArtistId, applied.ArtistId];

        return new AlbumTagWriteOutcome(state, album.Id, [], failures, artistIds.Distinct().ToList(), []);
    }

    private Task<List<ArtistRenameFailure>> WriteAsync(
        IReadOnlyList<PlannedFileWrite> writes,
        List<long> unsaved,
        IProgress<ArtistRenameProgress>? progress,
        CancellationToken cancellationToken)
    {
        Dictionary<long, TrackEntity> tracks = writes.ToDictionary(w => w.Track.Id, w => w.Track);

        return fileWriter.WriteAsync(writes, (id, date, size) => CompleteTrackAsync(tracks[id], date, size, unsaved), progress, cancellationToken);
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

    private static IEnumerable<ArtistRenameFailure> ToFailures(IEnumerable<TrackEntity> tracks, List<long> unsaved)
    {
        return tracks
            .Where(t => unsaved.Contains(t.Id))
            .Select(t => new ArtistRenameFailure(t.MusicFile, ETagWriteReason.IoError));
    }

    private static AlbumTagWriteOutcome Busy(long albumId)
    {
        ArtistRenameBlocker busy = new(string.Empty, EArtistRenameBlock.ImportInProgress, null);

        return new AlbumTagWriteOutcome(EArtistRenameState.Blocked, albumId, [busy], [], [], []);
    }

    private async Task<Resolution> ResolveAsync(long albumId, string requestedName, string requestedArtist, int? year, string? requestedGenre)
    {
        if (!appOptions.WriteTagsEnabled)
            return Resolution.Fail(new OperationError("album.edit.disabled", "Writing tags is turned off."));

        AlbumEntity? album = await albumRepository.GetByIdAsync(albumId);

        if (album is null)
            return Resolution.Fail(NotFoundError.ForEntity("Album", albumId));

        if (album.ArtistId is null)
            return Resolution.Fail(new OperationError("album.edit.no_artist", "The album has no artist and cannot be edited."));

        if (await pendingTagWriteRepository.CountByAlbumAsync(albumId) > 0)
            return Resolution.Fail(new OperationError("album.edit.pending", "Some files of the album are still waiting for a write: resume it first."));

        string name = AlbumNameRules.Normalize(requestedName);
        string artistInput = ArtistNameRules.Normalize(requestedArtist);
        string? genreInput = string.IsNullOrWhiteSpace(requestedGenre) ? null : AlbumNameRules.Normalize(requestedGenre);
        AlbumEditSubject subject = new(album, album.ArtistName ?? string.Empty, album.GenreName ?? string.Empty);
        bool artistChanged = !ArtistNameRules.HasSameKey(subject.ArtistName, artistInput);

        if (album.IsCompilation && artistChanged)
            return Resolution.Fail(new OperationError("album.edit.compilation_artist", "The artist of a compilation cannot be changed."));

        List<ArtistEntity> artists = (await artistRepository.GetAllAsync()).ToList();
        ArtistEntity? oldArtist = artists.FirstOrDefault(a => a.Id == album.ArtistId);
        ArtistEntity? existingArtist = artistChanged ? FindArtist(artists, artistInput) : oldArtist;

        if (!string.IsNullOrEmpty(oldArtist?.RenamePendingFrom) || !string.IsNullOrEmpty(existingArtist?.RenamePendingFrom))
            return Resolution.Fail(new OperationError("album.edit.artist_pending_rename", "An artist involved in the edit has a rename still pending."));

        string targetArtistName = artistChanged ? existingArtist?.Name ?? artistInput : subject.ArtistName;
        bool genreChanged = genreInput is not null && !ArtistNameRules.HasSameKey(subject.GenreName, genreInput);

        if (genreChanged && appOptions.ImportTrackWithArtistGenre)
            return Resolution.Fail(new OperationError("album.edit.genre_from_artist", "The import gives each track the genre of its artist: the album genre cannot be edited while that option is on."));

        GenreEntity? existingGenre = null;

        if (genreChanged)
            existingGenre = (await genreRepository.GetAllAsync()).FirstOrDefault(g => ArtistNameRules.HasSameKey(g.Name, genreInput!));

        string targetGenreName = genreChanged ? existingGenre?.Name ?? genreInput! : subject.GenreName;
        List<TrackEntity> tracks = (await trackRepository.GetByAlbumIdAsync(albumId)).ToList();
        AlbumEditPlan shape = AlbumEditPlanner.Plan(subject, tracks, new Dictionary<long, TagFieldValues>(), name, targetArtistName, year, genreChanged ? targetGenreName : null);

        if (!shape.HasChanges)
            return Resolution.Fail(new OperationError("album.edit.unchanged", "The album already carries these values."));

        if (shape.GenreChanged && existingGenre is null && shape.GenreTrackCount == 0)
            return Resolution.Fail(new OperationError("album.edit.genre_unused", "No track carries the album genre: the new genre would be removed by the next library cleaning."));

        AlbumEntity? collision = await FindCollisionAsync(album, shape, existingArtist, name);

        if (collision is not null && album.IsCompilation)
            return Resolution.Fail(new ConflictError("album.rename.collision", $"A compilation named '{collision.Name}' already exists."));

        Context context = new(
            album,
            subject,
            tracks,
            name,
            targetArtistName,
            targetGenreName,
            year,
            existingArtist,
            existingGenre,
            artistChanged && existingArtist is null,
            shape.GenreChanged && existingGenre is null,
            collision,
            oldArtist,
            genreChanged);

        return Resolution.Ok(context);
    }

    private async Task<AlbumEntity?> FindCollisionAsync(AlbumEntity album, AlbumEditPlan shape, ArtistEntity? existingArtist, string name)
    {
        if (!shape.NameChanged && !shape.ArtistChanged)
            return null;

        if (album.IsCompilation)
            return (await albumRepository.GetAllAsync()).FirstOrDefault(a => a.Id != album.Id && a.IsCompilation && AlbumNameRules.HasSameKey(a.Name, name));

        if (existingArtist is null)
            return null;

        IEnumerable<AlbumEntity> albums = (await albumRepository.GetByArtistIdAsync(existingArtist.Id)).OfType<AlbumEntity>();

        return AlbumNameRules.FindCollision(albums, album.Id, name);
    }

    private static ArtistEntity? FindArtist(List<ArtistEntity> artists, string name)
    {
        string key = name.ToIndexKey();

        return artists.FirstOrDefault(a => a.Name.ToIndexKey() == key)
            ?? artists.FirstOrDefault(a => !string.IsNullOrEmpty(a.RenamePendingFrom) && a.RenamePendingFrom.ToIndexKey() == key);
    }

    private async Task<Analysis> AnalyzeAsync(Context context, CancellationToken cancellationToken)
    {
        List<ArtistRenameBlocker> blockers = [];

        if (import.UpdateInProgress)
            blockers.Add(new ArtistRenameBlocker(string.Empty, EArtistRenameBlock.ImportInProgress, null));

        TrackFileAnalysis files = await fileWriter.AnalyzeAsync(
            context.Tracks,
            [],
            cancellationToken,
            (track, check) => AlbumEditPlanner.FindOutOfSync(context.Subject, track, check.Current, context.Name, context.TargetArtistName));

        blockers.AddRange(files.Blockers);

        Dictionary<long, TagFieldValues> current = files.Files
            .Where(f => f.Check.Current is not null)
            .ToDictionary(f => f.Track.Id, f => f.Check.Current!);

        AlbumEditPlan plan = AlbumEditPlanner.Plan(
            context.Subject,
            context.Tracks,
            current,
            context.Name,
            context.TargetArtistName,
            context.Year,
            context.GenreChanged ? context.TargetGenreName : null);

        return new Analysis(plan, blockers);
    }

    private async Task<OrphanArtistWarning?> FindOrphanArtistAsync(Context context)
    {
        if (context.OldArtist is null || ArtistNameRules.HasSameKey(context.Subject.ArtistName, context.TargetArtistName))
            return null;

        IEnumerable<TrackEntity> tracks = await trackRepository.GetByArtistIdAsync(context.OldArtist.Id);

        if (tracks.Any(t => t.AlbumId != context.Album.Id))
            return null;

        return new OrphanArtistWarning(context.OldArtist.Id, context.OldArtist.Name, context.OldArtist.IsFavorite, context.OldArtist.ListenCount);
    }

    private sealed record Context(
        AlbumEntity Album,
        AlbumEditSubject Subject,
        IReadOnlyList<TrackEntity> Tracks,
        string Name,
        string TargetArtistName,
        string TargetGenreName,
        int? Year,
        ArtistEntity? ExistingArtist,
        GenreEntity? ExistingGenre,
        bool CreatesArtist,
        bool CreatesGenre,
        AlbumEntity? Collision,
        ArtistEntity? OldArtist,
        bool GenreChanged);

    private sealed record Analysis(AlbumEditPlan Plan, IReadOnlyList<ArtistRenameBlocker> Blockers);

    private readonly record struct Resolution(Context? Context, Error? Error)
    {
        public static Resolution Ok(Context context) => new(context, null);

        public static Resolution Fail(Error error) => new(null, error);
    }
}