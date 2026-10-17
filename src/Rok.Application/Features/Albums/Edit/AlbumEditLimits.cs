namespace Rok.Application.Features.Albums.Edit;

/// <summary>
/// Limits of the album edit.
/// </summary>
public static class AlbumEditLimits
{
    /// <summary>
    /// Longest accepted album, artist or genre name.
    /// </summary>
    public const int MaxNameLength = 255;

    /// <summary>
    /// Lowest accepted year.
    /// </summary>
    public const int MinYear = 1;

    /// <summary>
    /// Highest accepted year.
    /// </summary>
    public const int MaxYear = 9999;
}