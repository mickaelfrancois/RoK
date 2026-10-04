using System.Globalization;

namespace Rok.ViewModels.Albums.Services;

/// <summary>Turns the three families of active album filters (list, genre, tag) into one ordered chip list.</summary>
public static class AlbumFilterChipBuilder
{
    public static List<AlbumFilterChip> Build(
        IEnumerable<string> filters,
        IEnumerable<long> genreIds,
        IEnumerable<string> tags,
        IEnumerable<GenreDto> genres,
        Func<string, string> filterLabel)
    {
        List<AlbumFilterChip> chips = [];

        foreach (string filter in filters)
            chips.Add(new AlbumFilterChip(AlbumFilterKind.List, filter, filterLabel(filter)));

        Dictionary<long, string> genreNames = genres.ToDictionary(g => g.Id, g => g.Name);

        foreach (long genreId in genreIds)
        {
            if (genreNames.TryGetValue(genreId, out string? name))
                chips.Add(new AlbumFilterChip(AlbumFilterKind.Genre, genreId.ToString(CultureInfo.InvariantCulture), name));
        }

        foreach (string tag in tags)
            chips.Add(new AlbumFilterChip(AlbumFilterKind.Tag, tag, tag));

        return chips;
    }
}