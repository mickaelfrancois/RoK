using System.Text.RegularExpressions;

namespace Rok.Import.Services;

/// <summary>
/// Gives the folder that identifies an album: a disc subfolder (CD1, Disc 2, Disque 3...) counts as its parent.
/// </summary>
internal static partial class AlbumFolderKey
{
    [GeneratedRegex(@"^(cd|dis[ck]|disque)[\s._-]*\d{1,2}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DiscFolderRegex();

    /// <summary>
    /// Returns the album folder for a track directory, without trailing separators.
    /// </summary>
    /// <param name="directory">The directory holding the track, or <see langword="null"/>.</param>
    /// <returns>The parent directory when the last segment is a disc folder; otherwise the directory itself.</returns>
    internal static string Of(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            return string.Empty;

        string trimmed = directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string lastSegment = Path.GetFileName(trimmed);

        if (!DiscFolderRegex().IsMatch(lastSegment))
            return trimmed;

        string? parent = Path.GetDirectoryName(trimmed);

        return string.IsNullOrEmpty(parent) ? trimmed : parent;
    }
}