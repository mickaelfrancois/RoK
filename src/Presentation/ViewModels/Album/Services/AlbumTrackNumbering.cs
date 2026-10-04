namespace Rok.ViewModels.Album.Services;

/// <summary>Decides whether the track numbers of an album can label its rows.</summary>
public static class AlbumTrackNumbering
{
    /// <summary>
    /// True when every track has a positive number and no number repeats. A repeated number means
    /// several discs share the album, and the row index is then the only unambiguous label.
    /// </summary>
    public static bool CanUseTrackNumbers(IReadOnlyCollection<int?> trackNumbers)
    {
        ArgumentNullException.ThrowIfNull(trackNumbers);

        if (trackNumbers.Count == 0)
            return false;

        HashSet<int> seen = [];

        foreach (int? number in trackNumbers)
        {
            if (number is not > 0 || !seen.Add(number.Value))
                return false;
        }

        return true;
    }
}