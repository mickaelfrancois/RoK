using System.Globalization;
using Rok.Application.Features.Albums.Edit;
using Rok.Application.Features.Albums.Merge;
using Rok.Application.Features.Albums.Requests;
using Rok.Application.Features.Artists.Rename;
using Rok.Application.Features.Artists.Requests;
using Rok.Application.Features.Genres.Requests;

namespace Rok.ViewModels.Album.Services;

/// <summary>Edits an album: its fields, its name, artist, year and genre through the write dialog, its merge with another album, and the finish of an interrupted write.</summary>
public class AlbumEditService(IMediator mediator, IDialogService dialogService, IAppOptions appOptions, IStringResourceProvider strings, ILogger<AlbumEditService> logger)
{
    private const string UnchangedCode = "album.edit.unchanged";
    private const int MaxSuggestions = 8;

    private ValidatedEdit? _validated;

    /// <summary>Opens the edit dialog, saves the fields, then writes the name, artist, year and genre when they changed.</summary>
    /// <param name="album">The album; its fields are updated when the dialog is confirmed.</param>
    /// <returns>What the edit changed.</returns>
    public async Task<AlbumEditResult> EditAlbumAsync(AlbumDto album)
    {
        AlbumTaggedFieldsMode mode = await ResolveModeAsync(album);
        AlbumEditSuggestions suggestions = mode == AlbumTaggedFieldsMode.Hidden ? AlbumEditSuggestions.None : await LoadSuggestionsAsync();

        bool genreLocked = mode != AlbumTaggedFieldsMode.Hidden && appOptions.ImportTrackWithArtistGenre;

        AlbumEditValues current = new()
        {
            GenreLocked = genreLocked,
            Name = album.Name,
            ArtistName = album.ArtistName,
            Year = album.Year?.ToString(CultureInfo.InvariantCulture),
            GenreName = album.GenreName,
            IsBestOf = album.IsBestOf,
            IsLive = album.IsLive,
            IsCompilation = album.IsCompilation,
            MusicBrainzID = album.MusicBrainzID,
            ReleaseGroupMusicBrainzID = album.ReleaseGroupMusicBrainzID,
            IsLock = album.IsLock,
            Biography = album.Biography,
            LastFmUrl = album.LastFmUrl
        };

        _validated = null;

        AlbumEditValues? edited = await dialogService.ShowEditAlbumAsync(current, mode, suggestions, values => ValidateAsync(album, mode, genreLocked, values));

        if (edited is null)
            return AlbumEditResult.Nothing;

        UpdateAlbumRequest command = new()
        {
            Id = album.Id,
        };

        command.IsBestOf.Set(edited.IsBestOf);
        command.IsLive.Set(edited.IsLive);
        command.IsCompilation.Set(edited.IsCompilation);
        command.MusicBrainzID.Set(edited.MusicBrainzID);
        command.ReleaseGroupMusicBrainzID.Set(edited.ReleaseGroupMusicBrainzID);
        command.Biography.Set(edited.Biography);
        command.IsLock.Set(edited.IsLock);
        command.LastFmUrl.Set(edited.LastFmUrl);

        await mediator.Send(command);

        album.IsBestOf = edited.IsBestOf;
        album.IsLive = edited.IsLive;
        album.IsCompilation = edited.IsCompilation;
        album.MusicBrainzID = edited.MusicBrainzID;
        album.ReleaseGroupMusicBrainzID = edited.ReleaseGroupMusicBrainzID;
        album.Biography = edited.Biography;
        album.IsLock = edited.IsLock;
        album.LastFmUrl = edited.LastFmUrl;

        if (mode == AlbumTaggedFieldsMode.Hidden)
            return new AlbumEditResult(true, null);

        TaggedEdit? edit = ReadEdit(album, mode, genreLocked, edited);

        if (edit is null || !edit.ChangesAnything(album))
            return new AlbumEditResult(true, null);

        AlbumEditPreview? preview = _validated is not null && _validated.Edit == edit
            ? _validated.Preview
            : await LoadPreviewAsync(album, edit);

        if (preview is null)
            return new AlbumEditResult(true, null);

        if (preview.Collision is not null)
            return new AlbumEditResult(true, await OfferCollisionAsync(album, preview.Collision));

        WriteSession session = new();
        AlbumTagWriteOutcome? outcome = await dialogService.ShowAlbumTagWriteAsync(AlbumEditTexts.Prompt(preview, Localize), progress => TryWriteAsync(album.Id, edit, session, progress));

        return new AlbumEditResult(true, outcome);
    }

    /// <summary>Tells whether some files of an album still wait for a tag write.</summary>
    /// <param name="albumId">Id of the album.</param>
    /// <returns><see langword="true"/> when a write is pending; <see langword="false"/> when none is or the check failed.</returns>
    public async Task<bool> HasPendingTagWriteAsync(long albumId)
    {
        try
        {
            Result<bool> result = await mediator.Send(new GetAlbumPendingTagWriteRequest(albumId));

            return result.IsSuccess && result.Value;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to check the pending tag write of album {AlbumId}", albumId);

            return false;
        }
    }

    /// <summary>Opens the merge dialog so the user picks the other album; the displayed album stays by default.</summary>
    /// <param name="album">The displayed album.</param>
    /// <returns>The outcome of the merge; <see langword="null"/> when merging is not available or the user left without merging.</returns>
    public Task<AlbumTagWriteOutcome?> MergeWithAsync(AlbumDto album)
    {
        if (!appOptions.WriteTagsEnabled || album.IsCompilation)
            return Task.FromResult<AlbumTagWriteOutcome?>(null);

        AlbumMergeDialogRequest request = new(new AlbumMergeChoice(album.Id, album.Name, album.ArtistName), null, true, null);

        return dialogService.ShowMergeAlbumsAsync(request, BuildMergeActions(album.Id));
    }

    /// <summary>Finishes a write that was interrupted, after the user confirmed it in the write dialog.</summary>
    /// <param name="album">The album that carries a pending write.</param>
    /// <returns>What the resume changed.</returns>
    public async Task<AlbumEditResult> ResumeTagWriteAsync(AlbumDto album)
    {
        if (!appOptions.WriteTagsEnabled)
            return AlbumEditResult.Nothing;

        AlbumTagWriteOutcome? outcome = await dialogService.ShowAlbumTagWriteAsync(AlbumEditTexts.ResumePrompt(Localize), progress => TryResumeAsync(album.Id, progress));

        return new AlbumEditResult(false, outcome);
    }

    public async Task UpdateFavoriteAsync(AlbumDto album, bool isFavorite)
    {
        await mediator.Send(new UpdateAlbumFavoriteRequest(album.Id, isFavorite));
        album.IsFavorite = isFavorite;
    }

    public Task UpdateTagsAsync(long id, IEnumerable<string> tags)
    {
        return mediator.Send(new UpdateAlbumTagsRequest(id, tags));
    }

    public Task UpdatePictureDominantColorAsync(long id, long? colorValue)
    {
        return mediator.Send(new UpdateAlbumPictureDominantColorRequest(id, colorValue));
    }

    private static TaggedEdit? ReadEdit(AlbumDto album, AlbumTaggedFieldsMode mode, bool genreLocked, AlbumEditValues values)
    {
        string name = AlbumNameRules.Normalize(values.Name);

        if (name.Length == 0 || name.Length > AlbumEditLimits.MaxNameLength)
            return null;

        string artist = mode == AlbumTaggedFieldsMode.Editable ? ArtistNameRules.Normalize(values.ArtistName) : album.ArtistName;

        if (artist.Length == 0 || artist.Length > AlbumEditLimits.MaxNameLength)
            return null;

        string genre = genreLocked ? string.Empty : AlbumNameRules.Normalize(values.GenreName);

        if (genre.Length > AlbumEditLimits.MaxNameLength)
            return null;

        string yearText = (values.Year ?? string.Empty).Trim();
        int? year = null;

        if (yearText.Length > 0)
        {
            if (!int.TryParse(yearText, NumberStyles.None, CultureInfo.InvariantCulture, out int parsed) || parsed < AlbumEditLimits.MinYear || parsed > AlbumEditLimits.MaxYear)
                return null;

            year = parsed;
        }

        return new TaggedEdit(name, artist, year, genre.Length == 0 ? null : genre);
    }

    private async Task<AlbumTaggedFieldsMode> ResolveModeAsync(AlbumDto album)
    {
        if (!appOptions.WriteTagsEnabled || album.ArtistId is null)
            return AlbumTaggedFieldsMode.Hidden;

        if (await HasPendingTagWriteAsync(album.Id))
            return AlbumTaggedFieldsMode.Hidden;

        return album.IsCompilation ? AlbumTaggedFieldsMode.ArtistLocked : AlbumTaggedFieldsMode.Editable;
    }

    private async Task<AlbumEditSuggestions> LoadSuggestionsAsync()
    {
        try
        {
            IEnumerable<ArtistDto> artists = await mediator.Send(new GetAllArtistsRequest());
            IEnumerable<GenreDto> genres = await mediator.Send(new GetAllGenresRequest());

            return new AlbumEditSuggestions([.. artists.Select(artist => artist.Name)], [.. genres.Select(genre => genre.Name)]);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load the artist and genre names to suggest");

            return AlbumEditSuggestions.None;
        }
    }

    private async Task<string?> ValidateAsync(AlbumDto album, AlbumTaggedFieldsMode mode, bool genreLocked, AlbumEditValues values)
    {
        TaggedEdit? edit = ReadEdit(album, mode, genreLocked, values);

        if (edit is null)
            return InvalidInputMessage(mode, genreLocked, values);

        if (!edit.ChangesAnything(album))
            return null;

        try
        {
            Result<AlbumEditPreview> result = await mediator.Send(new GetAlbumEditPreviewRequest(album.Id, edit.Name, edit.ArtistName, edit.Year, edit.GenreName));

            if (result.IsSuccess)
            {
                _validated = new ValidatedEdit(edit, result.Value);

                return null;
            }

            string? code = result.Errors.FirstOrDefault()?.Code;

            return code == UnchangedCode ? null : AlbumEditTexts.ErrorMessage(code, Localize);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to check the new values of album {AlbumId}", album.Id);

            return AlbumEditTexts.ErrorMessage(null, Localize);
        }
    }

    private async Task<AlbumEditPreview?> LoadPreviewAsync(AlbumDto album, TaggedEdit edit)
    {
        string error;

        try
        {
            Result<AlbumEditPreview> result = await mediator.Send(new GetAlbumEditPreviewRequest(album.Id, edit.Name, edit.ArtistName, edit.Year, edit.GenreName));

            if (result.IsSuccess)
                return result.Value;

            string? code = result.Errors.FirstOrDefault()?.Code;

            if (code == UnchangedCode)
                return null;

            error = AlbumEditTexts.ErrorMessage(code, Localize);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to prepare the edit of album {AlbumId}", album.Id);
            error = AlbumEditTexts.ErrorMessage(null, Localize);
        }

        await dialogService.ShowTextAsync(album.Name, error);

        return null;
    }

    private async Task<AlbumTagWriteOutcome?> OfferCollisionAsync(AlbumDto album, AlbumCollision collision)
    {
        if (collision.AlbumId <= 0 || album.IsCompilation || !appOptions.WriteTagsEnabled)
        {
            await dialogService.ShowTextAsync(album.Name, AlbumEditTexts.CollisionMessage(collision, Localize));

            return null;
        }

        AlbumMergeDialogRequest request = new(
            new AlbumMergeChoice(album.Id, album.Name, album.ArtistName),
            new AlbumMergeChoice(collision.AlbumId, collision.Name, collision.ArtistName),
            false,
            AlbumMergeTexts.CollisionIntro(collision, Localize));

        return await dialogService.ShowMergeAlbumsAsync(request, BuildMergeActions(album.Id));
    }

    private AlbumMergeDialogActions BuildMergeActions(long displayedAlbumId)
    {
        MergeSession session = new();

        return new AlbumMergeDialogActions(
            PreviewMergeAsync,
            (sourceId, targetId, shiftDiscs, progress) => TryMergeAsync(sourceId, targetId, shiftDiscs, session, progress),
            text => SearchAlbumsAsync(session, displayedAlbumId, text));
    }

    private async Task<AlbumMergePreviewAttempt> PreviewMergeAsync(long sourceId, long targetId)
    {
        try
        {
            Result<AlbumMergePreview> result = await mediator.Send(new GetAlbumMergePreviewRequest(sourceId, targetId));

            if (result.IsSuccess)
                return AlbumMergePreviewAttempt.Success(result.Value);

            return AlbumMergePreviewAttempt.Failure(AlbumMergeTexts.ErrorMessage(result.Errors.FirstOrDefault()?.Code, Localize));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to preview the merge of album {SourceId} into {TargetId}", sourceId, targetId);

            return AlbumMergePreviewAttempt.Failure(AlbumMergeTexts.ErrorMessage(null, Localize));
        }
    }

    private async Task<AlbumTagWriteAttempt> TryMergeAsync(long sourceId, long targetId, bool shiftDiscs, MergeSession session, IProgress<ArtistRenameProgress> progress)
    {
        try
        {
            if (session.Partial is not null)
                return await TryResumeMergeAsync(session, progress);

            Result<AlbumTagWriteOutcome> result = await mediator.Send(new MergeAlbumsRequest(sourceId, targetId, shiftDiscs) { Progress = progress });

            if (!result.IsSuccess)
                return AlbumTagWriteAttempt.Failure(AlbumMergeTexts.ErrorMessage(result.Errors.FirstOrDefault()?.Code, Localize));

            if (result.Value.State == EArtistRenameState.Partial)
                session.Partial = result.Value;

            return AlbumTagWriteAttempt.Success(result.Value);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to merge album {SourceId} into {TargetId}", sourceId, targetId);

            return AlbumTagWriteAttempt.Failure(AlbumMergeTexts.ErrorMessage(null, Localize));
        }
    }

    private async Task<AlbumTagWriteAttempt> TryResumeMergeAsync(MergeSession session, IProgress<ArtistRenameProgress> progress)
    {
        AlbumTagWriteOutcome partial = session.Partial!;

        Result<AlbumTagWriteOutcome> result = await mediator.Send(new ResumeAlbumTagWriteRequest(partial.AlbumId) { Progress = progress });

        if (!result.IsSuccess)
            return AlbumTagWriteAttempt.Failure(AlbumEditTexts.ErrorMessage(result.Errors.FirstOrDefault()?.Code, Localize));

        AlbumTagWriteOutcome resumed = result.Value;

        AlbumTagWriteOutcome next = partial with
        {
            State = resumed.State,
            Blockers = resumed.Blockers,
            Failures = resumed.Failures,
            UpdatedArtistIds = [.. partial.UpdatedArtistIds.Union(resumed.UpdatedArtistIds)],
            DeletedAlbumIds = [.. partial.DeletedAlbumIds.Union(resumed.DeletedAlbumIds)]
        };

        if (resumed.State == EArtistRenameState.Completed)
            session.Partial = null;
        else if (resumed.State == EArtistRenameState.Partial)
            session.Partial = next;

        return AlbumTagWriteAttempt.Success(next);
    }

    private async Task<IReadOnlyList<AlbumMergeChoice>> SearchAlbumsAsync(MergeSession session, long displayedAlbumId, string text)
    {
        try
        {
            if (session.Albums is null)
            {
                IEnumerable<AlbumDto> all = await mediator.Send(new GetAllAlbumsRequest());

                session.Albums = [.. all.Where(album => !album.IsCompilation).Select(album => new AlbumMergeChoice(album.Id, album.Name, album.ArtistName))];
            }

            string typed = text.Trim();

            return
            [
                .. session.Albums
                    .Where(album => album.Id != displayedAlbumId && album.Label.Contains(typed, StringComparison.CurrentCultureIgnoreCase))
                    .OrderBy(album => album.Label, StringComparer.CurrentCultureIgnoreCase)
                    .Take(MaxSuggestions)
            ];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to search the albums to merge with album {AlbumId}", displayedAlbumId);

            return [];
        }
    }

    private async Task<AlbumTagWriteAttempt> TryWriteAsync(long albumId, TaggedEdit edit, WriteSession session, IProgress<ArtistRenameProgress> progress)
    {
        try
        {
            AlbumTagWriteOutcome? partial = session.Partial;

            Result<AlbumTagWriteOutcome> result = partial is not null
                ? await mediator.Send(new ResumeAlbumTagWriteRequest(albumId) { Progress = progress })
                : await mediator.Send(new EditAlbumTagsRequest(albumId, edit.Name, edit.ArtistName, edit.Year, edit.GenreName) { Progress = progress });

            if (!result.IsSuccess)
                return AlbumTagWriteAttempt.Failure(AlbumEditTexts.ErrorMessage(result.Errors.FirstOrDefault()?.Code, Localize));

            AlbumTagWriteOutcome outcome = partial is null
                ? result.Value
                : partial with
                {
                    State = result.Value.State,
                    Blockers = result.Value.Blockers,
                    Failures = result.Value.Failures,
                    UpdatedArtistIds = [.. partial.UpdatedArtistIds.Union(result.Value.UpdatedArtistIds)],
                    DeletedAlbumIds = [.. partial.DeletedAlbumIds.Union(result.Value.DeletedAlbumIds)]
                };

            if (result.Value.State == EArtistRenameState.Partial)
                session.Partial = outcome;
            else if (result.Value.State == EArtistRenameState.Completed)
                session.Partial = null;

            return AlbumTagWriteAttempt.Success(outcome);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to write the tags of album {AlbumId}", albumId);

            return AlbumTagWriteAttempt.Failure(AlbumEditTexts.ErrorMessage(null, Localize));
        }
    }

    private async Task<AlbumTagWriteAttempt> TryResumeAsync(long albumId, IProgress<ArtistRenameProgress> progress)
    {
        try
        {
            Result<AlbumTagWriteOutcome> result = await mediator.Send(new ResumeAlbumTagWriteRequest(albumId) { Progress = progress });

            return result.IsSuccess
                ? AlbumTagWriteAttempt.Success(result.Value)
                : AlbumTagWriteAttempt.Failure(AlbumEditTexts.ErrorMessage(result.Errors.FirstOrDefault()?.Code, Localize));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to resume the tag write of album {AlbumId}", albumId);

            return AlbumTagWriteAttempt.Failure(AlbumEditTexts.ErrorMessage(null, Localize));
        }
    }

    private string InvalidInputMessage(AlbumTaggedFieldsMode mode, bool genreLocked, AlbumEditValues values)
    {
        string name = AlbumNameRules.Normalize(values.Name);

        if (name.Length == 0 || name.Length > AlbumEditLimits.MaxNameLength)
            return Localize(AlbumEditTextKeys.InvalidName);

        string artist = ArtistNameRules.Normalize(values.ArtistName);

        if (mode == AlbumTaggedFieldsMode.Editable && (artist.Length == 0 || artist.Length > AlbumEditLimits.MaxNameLength))
            return Localize(AlbumEditTextKeys.InvalidArtist);

        if (!genreLocked && AlbumNameRules.Normalize(values.GenreName).Length > AlbumEditLimits.MaxNameLength)
            return Localize(AlbumEditTextKeys.InvalidGenre);

        return Localize(AlbumEditTextKeys.InvalidYear);
    }

    private string Localize(string key) => strings.GetString(key);

    private sealed record TaggedEdit(string Name, string ArtistName, int? Year, string? GenreName)
    {
        public bool ChangesAnything(AlbumDto album)
        {
            return Name != album.Name
                || ArtistName != album.ArtistName
                || (Year is not null && Year != album.Year)
                || (GenreName is not null && GenreName != album.GenreName);
        }
    }

    private sealed record ValidatedEdit(TaggedEdit Edit, AlbumEditPreview Preview);

    private sealed class WriteSession
    {
        public AlbumTagWriteOutcome? Partial { get; set; }
    }

    private sealed class MergeSession
    {
        public AlbumTagWriteOutcome? Partial { get; set; }

        public IReadOnlyList<AlbumMergeChoice>? Albums { get; set; }
    }
}