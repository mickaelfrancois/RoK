using Rok.Domain.Entities;
using Rok.Shared.Extensions;

namespace Rok.Application.Features.Albums.Edit;

/// <summary>
/// Pure rules on album names used by the album edit. They follow the key of the library import.
/// </summary>
public static class AlbumNameRules
{
    /// <summary>
    /// Returns the name exactly as the import would store it: trimmed, then normalized.
    /// </summary>
    public static string Normalize(string? input)
    {
        return (input ?? string.Empty).Trim().NormalizeIndexedName();
    }

    /// <summary>
    /// True when both names have the same import key (normalized, invariant culture, ignoring case).
    /// </summary>
    public static bool HasSameKey(string? left, string? right)
    {
        return string.Equals(
            (left ?? string.Empty).NormalizeIndexedName(),
            (right ?? string.Empty).NormalizeIndexedName(),
            StringComparison.InvariantCultureIgnoreCase);
    }

    /// <summary>
    /// Finds another non compilation album, among the albums of the target artist, that the import would merge with <paramref name="newName"/>.
    /// The edited album itself never collides.
    /// </summary>
    /// <param name="albumsOfTargetArtist">Albums of the artist the edited album will belong to.</param>
    /// <param name="albumId">Id of the edited album.</param>
    /// <param name="newName">New name of the edited album.</param>
    public static AlbumEntity? FindCollision(IEnumerable<AlbumEntity> albumsOfTargetArtist, long albumId, string newName)
    {
        foreach (AlbumEntity album in albumsOfTargetArtist)
        {
            if (album.Id == albumId || album.IsCompilation)
                continue;

            if (HasSameKey(album.Name, newName))
                return album;
        }

        return null;
    }
}