using System.Text.Json;
using CleanArch.DevKit.Mediator.Results;
using Microsoft.Extensions.Logging;
using Rok.Application.Errors;
using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Pictures;
using Rok.Application.Interfaces.Repositories;
using Rok.Domain.Entities;

namespace Rok.Application.Features.Artists.Rename;

/// <summary>
/// Renames an artist: checks every file, then updates the database, then writes the new name in the tags.
/// The database goes first and keeps the old name as a transient alias, so an incident never makes a rescan recreate the artist.
/// Every database access stays on the caller context: the application shares a single SQLite connection.
/// </summary>
public class ArtistRenameService(
    IArtistRepository artistRepository,
    ITrackRepository trackRepository,
    IPlaylistHeaderRepository playlistHeaderRepository,
    IArtistRenameRepository artistRenameRepository,
    ArtistFileRewriter fileRewriter,
    IArtistPicture artistPicture,
    IImport import,
    ILibraryWriteGate libraryWriteGate,
    IAppOptions appOptions,
    ILogger<ArtistRenameService> logger)
{
    /// <summary>
    /// Tells what a rename would do, without changing anything.
    /// </summary>
    /// <param name="artistId">Id of the artist.</param>
    /// <param name="newName">Name asked by the user.</param>
    /// <param name="cancellationToken">Token observed between two files.</param>
    /// <returns>The preview, or an error when the rename cannot even be considered.</returns>
    public async Task<Result<ArtistRenamePreview>> PreviewAsync(long artistId, string newName, CancellationToken cancellationToken)
    {
        Resolution resolution = await ResolveAsync(artistId, newName);

        if (resolution.Error is not null)
            return Result<ArtistRenamePreview>.Fail(resolution.Error);

        Context context = resolution.Context!;

        if (context.Collision is not null)
            return Result<ArtistRenamePreview>.Ok(new ArtistRenamePreview(artistId, context.OldName, context.NewName, 0, 0, [], ToCollision(context.Collision), context.IsResume));

        Analysis analysis = await AnalyzeAsync(context, cancellationToken);

        return Result<ArtistRenamePreview>.Ok(new ArtistRenamePreview(
            artistId,
            context.OldName,
            context.NewName,
            analysis.Files.Tracks.Count,
            analysis.Files.Tracks.Select(t => t.AlbumId).Distinct().Count(),
            analysis.Blockers,
            null,
            context.IsResume));
    }

    /// <summary>
    /// Renames the artist, in the database and in the tags of its files.
    /// </summary>
    /// <param name="artistId">Id of the artist.</param>
    /// <param name="newName">Name asked by the user. For a resume it is the current name.</param>
    /// <param name="progress">Receives the progress of the file writing.</param>
    /// <param name="cancellationToken">Token observed between two files.</param>
    /// <returns>The outcome, or an error when the rename is refused before any check.</returns>
    public async Task<Result<ArtistRenameOutcome>> RenameAsync(long artistId, string newName, IProgress<ArtistRenameProgress>? progress, CancellationToken cancellationToken)
    {
        IDisposable? lease = null;

        try
        {
            if (import.UpdateInProgress || !libraryWriteGate.TryEnter(out lease))
            {
                ArtistRenameBlocker busy = new(string.Empty, EArtistRenameBlock.ImportInProgress, null);

                return Result<ArtistRenameOutcome>.Ok(new ArtistRenameOutcome(EArtistRenameState.Blocked, string.Empty, [busy], [], []));
            }

            Resolution resolution = await ResolveAsync(artistId, newName);

            if (resolution.Error is not null)
                return Result<ArtistRenameOutcome>.Fail(resolution.Error);

            Context context = resolution.Context!;

            if (context.Collision is not null)
                return Result<ArtistRenameOutcome>.Fail(new ConflictError("artist.rename.collision", $"An artist named '{context.Collision.Name}' already exists."));

            Analysis analysis = await AnalyzeAsync(context, cancellationToken);

            if (analysis.Blockers.Count > 0)
                return Result<ArtistRenameOutcome>.Ok(new ArtistRenameOutcome(EArtistRenameState.Blocked, context.Artist.Name, analysis.Blockers, [], []));

            return Result<ArtistRenameOutcome>.Ok(await ApplyAsync(context, analysis, progress, cancellationToken));
        }
        finally
        {
            lease?.Dispose();
        }
    }

    private async Task<ArtistRenameOutcome> ApplyAsync(Context context, Analysis analysis, IProgress<ArtistRenameProgress>? progress, CancellationToken cancellationToken)
    {
        ArtistRenameDbChange change;
        List<long> changedPlaylistIds;

        try
        {
            (change, changedPlaylistIds) = await BuildDbChangeAsync(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not prepare the rename of artist id {Id}.", context.Artist.Id);

            return Failed(context);
        }

        ArtistFolderMoveOutcome moved;

        try
        {
            moved = context.IsResume
                ? ArtistFolderMoveOutcome.NothingToDo
                : await Task.Run(() => artistPicture.MoveFolder(context.OldName, context.NewName), CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not move the image folder of artist id {Id}.", context.Artist.Id);

            return Blocked(context, new ArtistRenameBlocker(artistPicture.GetArtistFolder(context.NewName), EArtistRenameBlock.PictureFolder, ex.Message));
        }

        if (moved == ArtistFolderMoveOutcome.Blocked)
            return Blocked(context, new ArtistRenameBlocker(artistPicture.GetArtistFolder(context.NewName), EArtistRenameBlock.PictureFolder, null));

        try
        {
            await artistRenameRepository.ApplyAsync(change, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "The database update of the rename of artist id {Id} failed and was rolled back.", context.Artist.Id);

            await RestoreFolderAsync(context, moved);

            return Failed(context);
        }

        List<ArtistRenameFailure> failures = await fileRewriter.WriteAsync(analysis.Files, context.NewName, context.IsResume, progress, cancellationToken);

        if (failures.Count > 0)
            return new ArtistRenameOutcome(EArtistRenameState.Partial, context.NewName, [], failures, changedPlaylistIds);

        EArtistRenameState state = await CompleteAsync(context) ? EArtistRenameState.Completed : EArtistRenameState.Partial;

        return new ArtistRenameOutcome(state, context.NewName, [], [], changedPlaylistIds);
    }

    private async Task<bool> CompleteAsync(Context context)
    {
        try
        {
            bool cleared = await artistRenameRepository.CompleteAsync(context.Artist.Id);

            if (!cleared)
                logger.LogWarning("The pending-rename marker of artist id {Id} could not be cleared.", context.Artist.Id);

            return cleared;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "The pending-rename marker of artist id {Id} could not be cleared.", context.Artist.Id);

            return false;
        }
    }

    private async Task RestoreFolderAsync(Context context, ArtistFolderMoveOutcome moved)
    {
        if (moved == ArtistFolderMoveOutcome.NothingToDo)
            return;

        try
        {
            await Task.Run(() => artistPicture.MoveFolder(context.NewName, context.OldName), CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not move the image folder of artist id {Id} back.", context.Artist.Id);
        }
    }

    private async Task<(ArtistRenameDbChange Change, List<long> ChangedPlaylistIds)> BuildDbChangeAsync(Context context)
    {
        List<PlaylistCriteriaChange> criteria = [];
        List<long> changedPlaylistIds = [];

        foreach (PlaylistHeaderEntity playlist in await playlistHeaderRepository.GetAllAsync())
        {
            bool pictureChanged = string.Equals(playlist.Picture, context.OldName, StringComparison.OrdinalIgnoreCase);
            string? json = context.IsResume ? null : RewriteCriteria(playlist, context);

            if (json is not null)
                criteria.Add(new PlaylistCriteriaChange(playlist.Id, json));

            if (pictureChanged || json is not null)
                changedPlaylistIds.Add(playlist.Id);
        }

        return (new ArtistRenameDbChange(context.Artist.Id, context.OldName, context.NewName, criteria), changedPlaylistIds);
    }

    private string? RewriteCriteria(PlaylistHeaderEntity playlist, Context context)
    {
        if (playlist.Type != 0 || string.IsNullOrWhiteSpace(playlist.GroupsJson))
            return null;

        try
        {
            List<PlaylistGroupDto>? groups = JsonSerializer.Deserialize<List<PlaylistGroupDto>>(playlist.GroupsJson);

            if (groups is null || !SmartPlaylistArtistRename.Apply(groups, context.OldName, context.NewName))
                return null;

            return JsonSerializer.Serialize(groups);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "The criteria of playlist id {Id} are not readable, they are left untouched.", playlist.Id);

            return null;
        }
    }

    private async Task<Resolution> ResolveAsync(long artistId, string requestedName)
    {
        if (!appOptions.WriteTagsEnabled)
            return Resolution.Fail(new OperationError("artist.rename.disabled", "Writing tags is turned off."));

        List<ArtistDto> artists = (await artistRepository.GetAllAsync()).Select(a => a.ToDto()).ToList();
        ArtistDto? artist = artists.FirstOrDefault(a => a.Id == artistId);

        if (artist is null)
            return Resolution.Fail(NotFoundError.ForEntity("Artist", artistId));

        string name = ArtistNameRules.Normalize(requestedName);

        if (name.Length == 0)
            return Resolution.Fail(new OperationError("artist.rename.invalid_name", "The artist name must not be blank."));

        if (!string.IsNullOrEmpty(artist.RenamePendingFrom))
        {
            if (!string.Equals(name, artist.Name, StringComparison.Ordinal))
                return Resolution.Fail(new OperationError("artist.rename.pending", $"A rename from '{artist.RenamePendingFrom}' is still pending."));

            return Resolution.Ok(new Context(artist, artist.RenamePendingFrom, artist.Name, true, null, artists));
        }

        if (string.Equals(name, artist.Name, StringComparison.Ordinal))
            return Resolution.Fail(new OperationError("artist.rename.unchanged", "The artist already carries this name."));

        ArtistDto? collision = ArtistNameRules.FindCollision(artists, artistId, name);

        return Resolution.Ok(new Context(artist, artist.Name, name, false, collision, artists));
    }

    private async Task<Analysis> AnalyzeAsync(Context context, CancellationToken cancellationToken)
    {
        List<ArtistRenameBlocker> blockers = [];

        if (import.UpdateInProgress)
            blockers.Add(new ArtistRenameBlocker(string.Empty, EArtistRenameBlock.ImportInProgress, null));

        List<TrackEntity> tracks = (await trackRepository.GetByArtistIdAsync(context.Artist.Id)).ToList();
        ArtistFileAnalysis files = await fileRewriter.AnalyzeAsync(tracks, context.OldName, context.NewName, [], cancellationToken);

        blockers.AddRange(files.Blockers);

        if (!context.IsResume)
        {
            IReadOnlyCollection<string> otherNames = context.Artists.Where(a => a.Id != context.Artist.Id).Select(a => a.Name).ToList();

            if (artistPicture.CheckFolderMove(context.OldName, context.NewName, otherNames) == ArtistFolderMoveOutcome.Blocked)
                blockers.Add(new ArtistRenameBlocker(artistPicture.GetArtistFolder(context.NewName), EArtistRenameBlock.PictureFolder, null));
        }

        return new Analysis(files, blockers);
    }

    private static ArtistRenameCollision ToCollision(ArtistDto artist)
    {
        return new ArtistRenameCollision(artist.Name, artist.AlbumCount, artist.TrackCount) { ArtistId = artist.Id };
    }

    private static ArtistRenameOutcome Blocked(Context context, ArtistRenameBlocker blocker)
    {
        return new ArtistRenameOutcome(EArtistRenameState.Blocked, context.Artist.Name, [blocker], [], []);
    }

    private static ArtistRenameOutcome Failed(Context context)
    {
        return new ArtistRenameOutcome(EArtistRenameState.Failed, context.Artist.Name, [], [], []);
    }

    private sealed record Context(ArtistDto Artist, string OldName, string NewName, bool IsResume, ArtistDto? Collision, IReadOnlyList<ArtistDto> Artists);

    private sealed record Analysis(ArtistFileAnalysis Files, IReadOnlyList<ArtistRenameBlocker> Blockers);

    private readonly record struct Resolution(Context? Context, Error? Error)
    {
        public static Resolution Ok(Context context) => new(context, null);

        public static Resolution Fail(Error error) => new(null, error);
    }
}