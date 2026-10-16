using Rok.Application.Features.Artists.Merge;
using Rok.Application.Features.Artists.Rename;
using Rok.Application.Features.Tracks.Services;

namespace Rok.Services;

public interface IDialogService
{
    Task ShowTextAsync(string title, string content, bool showTranslateButton = false, string targetLanguage = "fr");

    /// <summary>Shows the album edit dialog seeded with <paramref name="current"/>.</summary>
    /// <returns>The edited values when the user confirms; <see langword="null"/> when the dialog is dismissed.</returns>
    Task<AlbumEditValues?> ShowEditAlbumAsync(AlbumEditValues current);

    /// <summary>Shows the artist edit dialog seeded with <paramref name="current"/>.</summary>
    /// <param name="current">The values the artist has now.</param>
    /// <param name="nameMode">Whether the name field is hidden, locked or editable.</param>
    /// <param name="validateNameAsync">Judges the typed name when the user saves; returns the message to show, or <see langword="null"/> when the name is accepted.</param>
    /// <returns>The edited values when the user confirms; <see langword="null"/> when the dialog is dismissed.</returns>
    Task<ArtistEditValues?> ShowEditArtistAsync(ArtistEditValues current, ArtistNameMode nameMode, Func<string, Task<string?>> validateNameAsync);

    /// <summary>Shows the rename dialog: the confirmation, then the progress, then the result.</summary>
    /// <param name="preview">What the rename would do.</param>
    /// <param name="renameAsync">Runs the rename and reports its progress; called again when the user retries.</param>
    /// <returns>The last outcome of the rename; <see langword="null"/> when the user left without starting it.</returns>
    Task<ArtistRenameOutcome?> ShowRenameArtistAsync(ArtistRenamePreview preview, Func<IProgress<ArtistRenameProgress>, Task<ArtistRenameAttempt>> renameAsync);

    /// <summary>Shows the merge dialog: the choice of the artists, the preview, then the progress and the result.</summary>
    /// <param name="request">What the dialog starts with.</param>
    /// <param name="actions">What the dialog calls to preview, run and search.</param>
    /// <returns>The last outcome that changed the library (completed or partial); <see langword="null"/> when nothing was merged.</returns>
    Task<ArtistMergeOutcome?> ShowMergeArtistsAsync(ArtistMergeDialogRequest request, ArtistMergeDialogActions actions);

    /// <summary>Shows the track edit dialog. The dialog stays open while <paramref name="trySaveAsync"/> returns a refusal.</summary>
    /// <param name="current">The tag fields the track has now.</param>
    /// <param name="trySaveAsync">Saves the changed fields; returns the saved values, or the message to show.</param>
    /// <returns>The saved values; <see langword="null"/> when the dialog is dismissed.</returns>
    Task<TrackEditSaved?> ShowEditTrackAsync(TrackEditValues current, Func<TrackTagsEdit, Task<TrackEditAttempt>> trySaveAsync);
}