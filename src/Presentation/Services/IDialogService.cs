using Rok.Application.Features.Tracks.Services;

namespace Rok.Services;

public interface IDialogService
{
    Task ShowTextAsync(string title, string content, bool showTranslateButton = false, string targetLanguage = "fr");

    /// <summary>Shows the album edit dialog seeded with <paramref name="current"/>.</summary>
    /// <returns>The edited values when the user confirms; <see langword="null"/> when the dialog is dismissed.</returns>
    Task<AlbumEditValues?> ShowEditAlbumAsync(AlbumEditValues current);

    /// <summary>Shows the artist edit dialog seeded with <paramref name="current"/>.</summary>
    /// <returns>The edited values when the user confirms; <see langword="null"/> when the dialog is dismissed.</returns>
    Task<ArtistEditValues?> ShowEditArtistAsync(ArtistEditValues current);

    /// <summary>Shows the track edit dialog. The dialog stays open while <paramref name="trySaveAsync"/> returns a refusal.</summary>
    /// <param name="current">The tag fields the track has now.</param>
    /// <param name="trySaveAsync">Saves the changed fields; returns the saved values, or the message to show.</param>
    /// <returns>The saved values; <see langword="null"/> when the dialog is dismissed.</returns>
    Task<TrackEditSaved?> ShowEditTrackAsync(TrackEditValues current, Func<TrackTagsEdit, Task<TrackEditAttempt>> trySaveAsync);
}