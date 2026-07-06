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
}