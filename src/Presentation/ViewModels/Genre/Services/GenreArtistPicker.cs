namespace Rok.ViewModels.Genre.Services;

/// <summary>Chooses the artist whose picture and backdrop illustrate a genre.</summary>
public static class GenreArtistPicker
{
    /// <summary>
    /// Returns the artists of the genre, the one with the most listens across its albums first, then
    /// the one with the most albums, then by name. Albums without an artist name are ignored.
    /// </summary>
    public static IReadOnlyList<string> RankByListens(IEnumerable<(string? ArtistName, int ListenCount)> albums)
    {
        ArgumentNullException.ThrowIfNull(albums);

        return albums
            .Where(album => !string.IsNullOrEmpty(album.ArtistName))
            .GroupBy(album => album.ArtistName!)
            .Select(group => new { Name = group.Key, Listens = group.Sum(album => album.ListenCount), AlbumCount = group.Count() })
            .OrderByDescending(artist => artist.Listens)
            .ThenByDescending(artist => artist.AlbumCount)
            .ThenBy(artist => artist.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(artist => artist.Name)
            .ToList();
    }

    /// <summary>
    /// Returns the most listened artist, or <c>null</c> when no album carries an artist name.
    /// </summary>
    public static string? PickMostListenedArtist(IEnumerable<(string? ArtistName, int ListenCount)> albums)
    {
        return RankByListens(albums).FirstOrDefault();
    }
}