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

    /// <summary>Shows the track title edit dialog. The dialog stays open while <paramref name="trySaveAsync"/> returns a message.</summary>
    /// <param name="currentTitle">The title the track has now.</param>
    /// <param name="trySaveAsync">Saves the typed title; returns <see langword="null"/> when saved, otherwise the message to show.</param>
    /// <returns>The saved title; <see langword="null"/> when the dialog is dismissed.</returns>
    Task<string?> ShowEditTrackTitleAsync(string currentTitle, Func<string, Task<string?>> trySaveAsync);
}