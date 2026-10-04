namespace Rok.ViewModels.Genre.Services;

/// <summary>The orders offered for the albums of a genre.</summary>
public enum GenreAlbumSort
{
    Artist,
    Name,
    Year,
    DateAdded,
    MostListened,
}

/// <summary>Sorts the albums of a genre; ties always fall back to the album name so the order is stable.</summary>
public static class GenreAlbumSorter
{
    public static List<T> Sort<T>(IEnumerable<T> items, Func<T, AlbumDto> album, GenreAlbumSort sort)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(album);

        StringComparer comparer = StringComparer.CurrentCultureIgnoreCase;

        IOrderedEnumerable<T> ordered = sort switch
        {
            GenreAlbumSort.Name => items.OrderBy(item => album(item).Name, comparer),
            GenreAlbumSort.Artist => items.OrderBy(item => album(item).ArtistName, comparer)
                                          .ThenBy(item => YearOf(album(item)))
                                          .ThenBy(item => album(item).Name, comparer),
            GenreAlbumSort.Year => items.OrderByDescending(item => YearOf(album(item)))
                                        .ThenBy(item => album(item).Name, comparer),
            GenreAlbumSort.DateAdded => items.OrderByDescending(item => album(item).CreatDate)
                                             .ThenBy(item => album(item).Name, comparer),
            GenreAlbumSort.MostListened => items.OrderByDescending(item => album(item).ListenCount)
                                                .ThenBy(item => album(item).Name, comparer),
            _ => throw new ArgumentOutOfRangeException(nameof(sort), sort, null),
        };

        return ordered.ToList();
    }

    private static int YearOf(AlbumDto album)
    {
        return album.ReleaseDate?.Year ?? album.Year ?? 0;
    }
}