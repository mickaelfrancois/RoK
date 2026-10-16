using Rok.Application.Dto;
using Rok.Shared.Extensions;

namespace Rok.Application.Features.Artists.Rename;

/// <summary>
/// Pure rules on artist names used by the rename feature.
/// </summary>
public static class ArtistNameRules
{
    /// <summary>
    /// Returns the name exactly as the import would store it: trimmed, then normalized.
    /// </summary>
    public static string Normalize(string? input)
    {
        return (input ?? string.Empty).Trim().NormalizeIndexedName();
    }

    /// <summary>
    /// Finds another artist that the import would merge with <paramref name="newName"/>: same index key,
    /// either as its name or as its pending rename marker. The renamed artist itself never collides.
    /// </summary>
    public static ArtistDto? FindCollision(IEnumerable<ArtistDto> artists, long artistId, string newName)
    {
        string key = newName.ToIndexKey();

        foreach (ArtistDto artist in artists)
        {
            if (artist.Id == artistId)
                continue;

            if (artist.Name.ToIndexKey() == key)
                return artist;

            if (!string.IsNullOrEmpty(artist.RenamePendingFrom) && artist.RenamePendingFrom.ToIndexKey() == key)
                return artist;
        }

        return null;
    }

    /// <summary>
    /// True when both names have the same index key (a case-only or normalization-only difference included).
    /// </summary>
    public static bool HasSameKey(string left, string right)
    {
        return left.ToIndexKey() == right.ToIndexKey();
    }
}