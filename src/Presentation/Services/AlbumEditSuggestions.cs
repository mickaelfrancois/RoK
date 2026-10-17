namespace Rok.Services;

/// <summary>Names the artist and genre fields of the album edit dialog suggest while the user types.</summary>
/// <param name="Artists">Names of the artists.</param>
/// <param name="Genres">Names of the genres.</param>
public sealed record AlbumEditSuggestions(IReadOnlyList<string> Artists, IReadOnlyList<string> Genres)
{
    /// <summary>Gets suggestions that hold no name.</summary>
    public static AlbumEditSuggestions None { get; } = new([], []);
}