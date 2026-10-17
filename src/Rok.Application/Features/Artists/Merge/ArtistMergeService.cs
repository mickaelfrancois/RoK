using System.Text.Json;
using CleanArch.DevKit.Mediator.Results;
using Microsoft.Extensions.Logging;
using Rok.Application.Errors;
using Rok.Application.Features.Artists.Rename;
using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Pictures;
using Rok.Application.Interfaces.Repositories;
using Rok.Domain.Entities;
using Rok.Domain.Enums;

namespace Rok.Application.Features.Artists.Merge;

/// <summary>
/// Merges an artist into another one: checks every file, then updates the database in one transaction,
/// then merges the image folder, then writes the kept artist name in the tags of the moved files.
/// The database goes first and keeps the merged artist name as a transient alias on the kept artist, so an incident never makes a rescan recreate it.
/// Every database access stays on the caller context: the application shares a single SQLite connection.
/// </summary>
public class ArtistMergeService(
    IArtistRepository artistRepository,
    ITrackRepository trackRepository,
    IAlbumRepository albumRepository,
    IPlaylistHeaderRepository playlistHeaderRepository,
    IArtistMergeRepository artistMergeRepository,
    IArtistRenameRepository artistRenameRepository,
    ArtistFileRewriter fileRewriter,
    IArtistPicture artistPicture,
    IImport import,
    ILibraryWriteGate libraryWriteGate,
    IAppOptions appOptions,
    ILogger<ArtistMergeService> logger)
{
    /// <summary>
    /// Tells what a merge would do, without changing anything.
    /// </summary>
    /// <param name="sourceId">Id of the artist that disappears.</param>
    /// <param name="targetId">Id of the artist that stays.</param>
    /// <param name="cancellationToken">Token observed while checking the files.</param>
    /// <returns>The preview, or an error when the merge cannot even be considered.</returns>
    public async Task<Result<ArtistMergePreview>> PreviewAsync(long sourceId, long targetId, CancellationToken cancellationToken)
    {
        Resolution resolution = await ResolveAsync(sourceId, targetId, cancellationToken);

        if (resolution.Error is not null)
            return Result<ArtistMergePreview>.Fail(resolution.Error);

        Context context = resolution.Context!;
        Plan plan = BuildPlan(context);

        return Result<ArtistMergePreview>.Ok(new ArtistMergePreview(
            context.Source.Id,
            context.Source.Name,
            context.Target.Id,
            context.Target.Name,
            context.Files.Tracks.Count,
            context.Files.Tracks.Select(t => t.AlbumId).Distinct().Count(),
            plan.AlbumGroups.Select(g => g.ToPreview()).ToList(),
            plan.Merge.Conflicts,
            context.Blockers,
            plan.Impacts,
            plan.Picture));
    }

    /// <summary>
    /// Merges the artist <paramref name="sourceId"/> into <paramref name="targetId"/>, in the database and in the tags of the moved files.
    /// </summary>
    /// <param name="sourceId">Id of the artist that disappears.</param>
    /// <param name="targetId">Id of the artist that stays.</param>
    /// <param name="progress">Receives the progress of the file writing.</param>
    /// <param name="cancellationToken">Token observed while checking and between two files.</param>
    /// <returns>The outcome, or an error when the merge is refused before any check.</returns>
    public async Task<Result<ArtistMergeOutcome>> MergeAsync(long sourceId, long targetId, IProgress<ArtistRenameProgress>? progress, CancellationToken cancellationToken)
    {
        IDisposable? lease = null;

        try
        {
            if (import.UpdateInProgress || !libraryWriteGate.TryEnter(out lease))
            {
                ArtistRenameBlocker busy = new(string.Empty, EArtistRenameBlock.ImportInProgress, null);

                return Result<ArtistMergeOutcome>.Ok(new ArtistMergeOutcome(EArtistRenameState.Blocked, targetId, string.Empty, [busy], [], [], 0, [], []));
            }

            Resolution resolution = await ResolveAsync(sourceId, targetId, cancellationToken);

            if (resolution.Error is not null)
                return Result<ArtistMergeOutcome>.Fail(resolution.Error);

            Context context = resolution.Context!;

            if (context.Blockers.Count > 0)
                return Result<ArtistMergeOutcome>.Ok(new ArtistMergeOutcome(EArtistRenameState.Blocked, context.Target.Id, context.Target.Name, context.Blockers, [], [], 0, [], []));

            return Result<ArtistMergeOutcome>.Ok(await ApplyAsync(context, progress, cancellationToken));
        }
        finally
        {
            lease?.Dispose();
        }
    }

    private async Task<ArtistMergeOutcome> ApplyAsync(Context context, IProgress<ArtistRenameProgress>? progress, CancellationToken cancellationToken)
    {
        Plan plan;
        ArtistMergeDbChange change;

        try
        {
            plan = BuildPlan(context);
            change = BuildDbChange(context, plan);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not prepare the merge of artist id {Source} into id {Target}.", context.Source.Id, context.Target.Id);

            return Failed(context);
        }

        try
        {
            await artistMergeRepository.ApplyAsync(change, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "The database update of the merge of artist id {Source} into id {Target} failed and was rolled back.", context.Source.Id, context.Target.Id);

            return Failed(context);
        }

        await MergeFolderAsync(context);

        List<ArtistRenameFailure> failures = await fileRewriter.WriteAsync(context.Files, context.Target.Name, true, progress, cancellationToken);
        bool cleared = failures.Count == 0 && await CompleteAsync(context, change);
        EArtistRenameState state = cleared ? EArtistRenameState.Completed : EArtistRenameState.Partial;

        List<long> deletedAlbumIds = plan.AlbumGroups.SelectMany(g => g.MergedIds).ToList();
        List<long> updatedAlbumIds = context.SourceAlbums
            .Select(a => a.Id)
            .Concat(plan.AlbumGroups.Select(g => g.SurvivorId))
            .Concat(context.ConvertibleCompilationIds)
            .Except(deletedAlbumIds)
            .Distinct()
            .ToList();

        return new ArtistMergeOutcome(state, context.Target.Id, context.Target.Name, [], failures, plan.ChangedPlaylistIds, context.Source.Id, deletedAlbumIds, updatedAlbumIds);
    }

    private async Task MergeFolderAsync(Context context)
    {
        IReadOnlyCollection<string> otherNames = context.Artists
            .Where(a => a.Id != context.Source.Id && a.Id != context.Target.Id)
            .Select(a => a.Name)
            .ToList();

        try
        {
            await Task.Run(() => artistPicture.MergeFolderInto(context.Source.Name, context.Target.Name, otherNames), CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not merge the image folder of artist id {Source} into the one of id {Target}.", context.Source.Id, context.Target.Id);
        }
    }

    private async Task<bool> CompleteAsync(Context context, ArtistMergeDbChange change)
    {
        if (change.PendingFrom is null)
            return true;

        try
        {
            bool cleared = await artistRenameRepository.CompleteAsync(context.Target.Id);

            if (!cleared)
                logger.LogWarning("The pending-rename marker of artist id {Id} could not be cleared.", context.Target.Id);

            return cleared;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "The pending-rename marker of artist id {Id} could not be cleared.", context.Target.Id);

            return false;
        }
    }

    private static ArtistMergeDbChange BuildDbChange(Context context, Plan plan)
    {
        string? pendingFrom = context.Files.Files.Any(f => f.NeedsWrite) ? context.Source.Name : null;

        return new ArtistMergeDbChange(
            context.Source.Id,
            context.Source.Name,
            context.Target.Id,
            context.Target.Name,
            plan.Merge.Merged,
            plan.AlbumGroups,
            pendingFrom,
            plan.Criteria,
            [.. context.ConvertibleCompilationIds.Order()]);
    }

    private Plan BuildPlan(Context context)
    {
        bool targetHasPicture = artistPicture.PictureFileExists(context.Target.Name);
        bool sourceHasPicture = artistPicture.PictureFileExists(context.Source.Name);
        ArtistMergeResult merge = ArtistMergeRules.Merge(context.Target, context.Source, targetHasPicture);
        IReadOnlyList<AlbumMergeGroup> albumGroups = AlbumMergePlanner.Plan(context.TargetAlbums, context.SourceAlbums, context.ConvertibleCompilationIds);
        EArtistMergePicture picture = targetHasPicture ? EArtistMergePicture.Target : sourceHasPicture ? EArtistMergePicture.Source : EArtistMergePicture.None;

        List<PlaylistCriteriaChange> criteria = [];
        List<SmartPlaylistMergeImpact> impacts = [];
        List<long> changedPlaylistIds = [];

        foreach (PlaylistHeaderEntity playlist in context.Playlists)
        {
            bool pictureChanged = string.Equals(playlist.Picture, context.Source.Name, StringComparison.OrdinalIgnoreCase);
            List<PlaylistGroupDto>? groups = ReadGroups(playlist);
            string? json = null;

            if (groups is not null)
            {
                SmartPlaylistMergeImpact? impact = SmartPlaylistArtistMerge.Describe(playlist.Id, playlist.Name, groups, context.Source.Name, context.Target.Name);

                if (impact is not null)
                    impacts.Add(impact);

                if (SmartPlaylistArtistMerge.Apply(groups, context.Source.Name, context.Target.Name))
                    json = JsonSerializer.Serialize(groups);
            }

            if (json is not null)
                criteria.Add(new PlaylistCriteriaChange(playlist.Id, json));

            if (pictureChanged || json is not null)
                changedPlaylistIds.Add(playlist.Id);
        }

        return new Plan(merge, albumGroups, criteria, impacts, changedPlaylistIds, picture);
    }

    private List<PlaylistGroupDto>? ReadGroups(PlaylistHeaderEntity playlist)
    {
        if (playlist.Type != 0 || string.IsNullOrWhiteSpace(playlist.GroupsJson))
            return null;

        try
        {
            return JsonSerializer.Deserialize<List<PlaylistGroupDto>>(playlist.GroupsJson);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "The criteria of playlist id {Id} are not readable, they are left untouched.", playlist.Id);

            return null;
        }
    }

    private async Task<Resolution> ResolveAsync(long sourceId, long targetId, CancellationToken cancellationToken)
    {
        if (!appOptions.WriteTagsEnabled)
            return Resolution.Fail(new OperationError("artist.merge.disabled", "Writing tags is turned off."));

        List<ArtistEntity> artists = (await artistRepository.GetAllAsync()).ToList();
        ArtistEntity? source = artists.FirstOrDefault(a => a.Id == sourceId);

        if (source is null)
            return Resolution.Fail(NotFoundError.ForEntity("Artist", sourceId));

        ArtistEntity? target = artists.FirstOrDefault(a => a.Id == targetId);

        if (target is null)
            return Resolution.Fail(NotFoundError.ForEntity("Artist", targetId));

        if (sourceId == targetId)
            return Resolution.Fail(new OperationError("artist.merge.same_artist", "An artist cannot be merged with itself."));

        ArtistEntity? pending = !string.IsNullOrEmpty(source.RenamePendingFrom)
            ? source
            : !string.IsNullOrEmpty(target.RenamePendingFrom) ? target : null;

        if (pending is not null)
            return Resolution.Fail(new OperationError("artist.merge.pending_rename", $"A rename of '{pending.Name}' from '{pending.RenamePendingFrom}' is still pending."));

        List<TrackEntity> sourceTracks = (await trackRepository.GetByArtistIdAsync(sourceId)).ToList();
        List<TrackEntity> targetTracks = (await trackRepository.GetByArtistIdAsync(targetId)).ToList();
        List<AlbumEntity> sourceAlbums = (await albumRepository.GetByArtistIdAsync(sourceId)).OfType<AlbumEntity>().ToList();
        List<AlbumEntity> targetAlbums = (await albumRepository.GetByArtistIdAsync(targetId)).OfType<AlbumEntity>().ToList();
        List<PlaylistHeaderEntity> playlists = (await playlistHeaderRepository.GetAllAsync()).ToList();
        List<ArtistRenameBlocker> blockers = [];

        if (import.UpdateInProgress)
            blockers.Add(new ArtistRenameBlocker(string.Empty, EArtistRenameBlock.ImportInProgress, null));

        ArtistFileAnalysis files = await fileRewriter.AnalyzeAsync(sourceTracks, source.Name, target.Name, targetTracks, cancellationToken);

        blockers.AddRange(files.Blockers);

        HashSet<long> convertible = await FindConvertibleCompilationsAsync(sourceId, targetId, sourceAlbums.Concat(targetAlbums));

        return Resolution.Ok(new Context(source, target, artists, sourceAlbums, targetAlbums, playlists, files, blockers, convertible));
    }

    private async Task<HashSet<long>> FindConvertibleCompilationsAsync(long sourceId, long targetId, IEnumerable<AlbumEntity> albums)
    {
        HashSet<long> convertible = [];

        foreach (AlbumEntity album in albums.Where(a => a.IsCompilation && a.CompilationSource == ECompilationSource.Derived))
        {
            List<TrackEntity> tracks = (await trackRepository.GetByAlbumIdAsync(album.Id)).ToList();

            if (tracks.Count > 0 && tracks.All(t => t.ArtistId == sourceId || t.ArtistId == targetId))
                convertible.Add(album.Id);
        }

        return convertible;
    }

    private static ArtistMergeOutcome Failed(Context context)
    {
        return new ArtistMergeOutcome(EArtistRenameState.Failed, context.Target.Id, context.Target.Name, [], [], [], 0, [], []);
    }

    private sealed record Context(
        ArtistEntity Source,
        ArtistEntity Target,
        IReadOnlyList<ArtistEntity> Artists,
        IReadOnlyList<AlbumEntity> SourceAlbums,
        IReadOnlyList<AlbumEntity> TargetAlbums,
        IReadOnlyList<PlaylistHeaderEntity> Playlists,
        ArtistFileAnalysis Files,
        IReadOnlyList<ArtistRenameBlocker> Blockers,
        IReadOnlySet<long> ConvertibleCompilationIds);

    private sealed record Plan(
        ArtistMergeResult Merge,
        IReadOnlyList<AlbumMergeGroup> AlbumGroups,
        IReadOnlyList<PlaylistCriteriaChange> Criteria,
        IReadOnlyList<SmartPlaylistMergeImpact> Impacts,
        IReadOnlyList<long> ChangedPlaylistIds,
        EArtistMergePicture Picture);

    private readonly record struct Resolution(Context? Context, Error? Error)
    {
        public static Resolution Ok(Context context) => new(context, null);

        public static Resolution Fail(Error error) => new(null, error);
    }
}