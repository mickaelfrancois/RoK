using System.Globalization;
using Rok.Application.Features.Artists.Rename;

namespace Rok.ViewModels.Artist.Services;

/// <summary>Edits an artist: its fields, and its name through the rename dialogs.</summary>
public class ArtistEditService(IMediator mediator, IDialogService dialogService, IAppOptions appOptions, IStringResourceProvider strings, ILogger<ArtistEditService> logger)
{
    private ArtistRenamePreview? _validatedPreview;

    /// <summary>Opens the edit dialog, saves the fields, then renames the artist when the name changed.</summary>
    /// <param name="artist">The artist; its fields are updated when the dialog is confirmed.</param>
    /// <returns>What the edit changed.</returns>
    public async Task<ArtistEditResult> EditArtistAsync(ArtistDto artist)
    {
        ArtistNameMode nameMode = ArtistRenameRules.NameMode(appOptions.WriteTagsEnabled, artist.RenamePendingFrom);

        ArtistEditValues current = new()
        {
            Name = artist.Name,
            MusicBrainzID = artist.MusicBrainzID,
            FormedYear = artist.FormedYear?.ToString(CultureInfo.InvariantCulture),
            BornYear = artist.BornYear?.ToString(CultureInfo.InvariantCulture),
            DiedYear = artist.DiedYear?.ToString(CultureInfo.InvariantCulture),
            Disbanded = artist.Disbanded,
            IsLock = artist.IsLock,
            Members = artist.Members,
            Biography = artist.Biography
        };

        _validatedPreview = null;

        ArtistEditValues? edited = await dialogService.ShowEditArtistAsync(current, nameMode, name => ValidateNameAsync(artist, name));

        if (edited is null)
            return ArtistEditResult.Nothing;

        int? formedYear = ParseYear(edited.FormedYear);
        int? bornYear = ParseYear(edited.BornYear);
        int? diedYear = ParseYear(edited.DiedYear);

        UpdateArtistRequest command = new() { Id = artist.Id };
        command.MusicBrainzID.Set(edited.MusicBrainzID);
        command.FormedYear.Set(formedYear);
        command.BornYear.Set(bornYear);
        command.DiedYear.Set(diedYear);
        command.Disbanded.Set(edited.Disbanded);
        command.IsLock.Set(edited.IsLock);
        command.Members.Set(edited.Members);
        command.Biography.Set(edited.Biography);

        await mediator.Send(command);

        artist.MusicBrainzID = edited.MusicBrainzID;
        artist.FormedYear = formedYear;
        artist.BornYear = bornYear;
        artist.DiedYear = diedYear;
        artist.Disbanded = edited.Disbanded;
        artist.IsLock = edited.IsLock;
        artist.Members = edited.Members;
        artist.Biography = edited.Biography;

        if (nameMode != ArtistNameMode.Editable)
            return new ArtistEditResult(true, null);

        ArtistNameChange change = ArtistRenameRules.Evaluate(artist.Name, edited.Name);

        if (change.Kind != ArtistNameChangeKind.Changed)
            return new ArtistEditResult(true, null);

        ArtistRenamePreview? preview = _validatedPreview is not null && _validatedPreview.NewName == change.Name
            ? _validatedPreview
            : await LoadPreviewAsync(artist, change.Name);

        if (preview is null)
            return new ArtistEditResult(true, null);

        ArtistRenameOutcome? outcome = await ShowRenameAsync(artist.Id, preview);

        return new ArtistEditResult(true, outcome);
    }

    /// <summary>Finishes a rename that was interrupted, after the user confirmed it in the rename dialog.</summary>
    /// <param name="artist">The artist that carries a pending rename.</param>
    /// <returns>What the resume changed.</returns>
    public async Task<ArtistEditResult> ResumeRenameAsync(ArtistDto artist)
    {
        if (!appOptions.WriteTagsEnabled || string.IsNullOrEmpty(artist.RenamePendingFrom))
            return ArtistEditResult.Nothing;

        ArtistRenamePreview? preview = await LoadPreviewAsync(artist, artist.Name);

        if (preview is null)
            return ArtistEditResult.Nothing;

        ArtistRenameOutcome? outcome = await ShowRenameAsync(artist.Id, preview);

        return new ArtistEditResult(false, outcome);
    }

    private async Task<string?> ValidateNameAsync(ArtistDto artist, string typedName)
    {
        ArtistNameChange change = ArtistRenameRules.Evaluate(artist.Name, typedName);

        if (change.Kind == ArtistNameChangeKind.Invalid)
            return Localize(ArtistRenameTextKeys.InvalidName);

        if (change.Kind == ArtistNameChangeKind.Unchanged)
            return null;

        try
        {
            Result<ArtistRenamePreview> result = await mediator.Send(new GetArtistRenamePreviewRequest(artist.Id, change.Name));

            if (!result.IsSuccess)
                return ArtistRenameTexts.ErrorMessage(result.Errors.FirstOrDefault()?.Code, Localize);

            if (result.Value.Collision is not null)
                return ArtistRenameTexts.CollisionMessage(result.Value.Collision, Localize);

            _validatedPreview = result.Value;

            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to check the new name of artist {ArtistId}", artist.Id);

            return ArtistRenameTexts.ErrorMessage(null, Localize);
        }
    }

    private async Task<ArtistRenamePreview?> LoadPreviewAsync(ArtistDto artist, string newName)
    {
        string error;

        try
        {
            Result<ArtistRenamePreview> result = await mediator.Send(new GetArtistRenamePreviewRequest(artist.Id, newName));

            if (result.IsSuccess && result.Value.Collision is null)
                return result.Value;

            error = result.IsSuccess
                ? ArtistRenameTexts.CollisionMessage(result.Value.Collision!, Localize)
                : ArtistRenameTexts.ErrorMessage(result.Errors.FirstOrDefault()?.Code, Localize);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to prepare the rename of artist {ArtistId}", artist.Id);
            error = ArtistRenameTexts.ErrorMessage(null, Localize);
        }

        await dialogService.ShowTextAsync(artist.Name, error);

        return null;
    }

    private Task<ArtistRenameOutcome?> ShowRenameAsync(long artistId, ArtistRenamePreview preview)
    {
        return dialogService.ShowRenameArtistAsync(preview, progress => TryRenameAsync(artistId, preview.NewName, progress));
    }

    private async Task<ArtistRenameAttempt> TryRenameAsync(long artistId, string newName, IProgress<ArtistRenameProgress> progress)
    {
        try
        {
            Result<ArtistRenameOutcome> result = await mediator.Send(new RenameArtistRequest(artistId, newName) { Progress = progress });

            if (result.IsSuccess)
                return ArtistRenameAttempt.Success(result.Value);

            return ArtistRenameAttempt.Failure(ArtistRenameTexts.ErrorMessage(result.Errors.FirstOrDefault()?.Code, Localize));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to rename artist {ArtistId}", artistId);

            return ArtistRenameAttempt.Failure(ArtistRenameTexts.ErrorMessage(null, Localize));
        }
    }

    private string Localize(string key) => strings.GetString(key);

    /// <summary>Parses a year typed in the dialog; empty or non-numeric input maps to <see langword="null"/>.</summary>
    private static int? ParseYear(string? value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int year)
            ? year
            : null;
    }

    public async Task UpdateFavoriteAsync(ArtistDto artist, bool isFavorite)
    {
        await mediator.Send(new UpdateArtistFavoriteRequest(artist.Id, isFavorite));
        artist.IsFavorite = isFavorite;
    }

    public async Task<bool> OpenOfficialSiteAsync(ArtistDto artist)
    {
        try
        {
            string? url = artist.OfficialSiteUrl;
            if (url == null)
                url = artist.WikipediaUrl;

            if (string.IsNullOrWhiteSpace(url))
                return false;

            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                url = "https://" + url.Trim();
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
            {
                logger.LogWarning("Invalid URL for artist {ArtistName}: {Url}", artist.Name, url);
                return false;
            }

            bool success = await Windows.System.Launcher.LaunchUriAsync(uri);
            if (!success)
            {
                logger.LogWarning("Unable to open URL {Url} for artist {ArtistName}", url, artist.Name);
            }

            return success;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while opening URL for artist {ArtistName}", artist.Name);
            return false;
        }
    }

    public Task UpdateTagsAsync(long id, IEnumerable<string> tags)
    {
        return mediator.Send(new UpdateArtistTagsRequest(id, tags));
    }

    public Task UpdatePictureDominantColorAsync(long id, long? colorValue)
    {
        return mediator.Send(new UpdateArtistPictureDominantColorRequest(id, colorValue));
    }
}