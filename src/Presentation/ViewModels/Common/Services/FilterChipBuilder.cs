using System.Globalization;

namespace Rok.ViewModels.Common.Services;

/// <summary>Turns the three families of active list filters (list, genre, tag) into one ordered chip list.</summary>
public static class FilterChipBuilder
{
    public static List<FilterChip> Build(
        IEnumerable<string> filters,
        IEnumerable<long> genreIds,
        IEnumerable<string> tags,
        IEnumerable<GenreDto> genres,
        Func<string, string> filterLabel)
    {
        List<FilterChip> chips = [];

        foreach (string filter in filters)
            chips.Add(new FilterChip(FilterKind.List, filter, filterLabel(filter)));

        Dictionary<long, string> genreNames = genres.ToDictionary(g => g.Id, g => g.Name);

        foreach (long genreId in genreIds)
        {
            if (genreNames.TryGetValue(genreId, out string? name))
                chips.Add(new FilterChip(FilterKind.Genre, genreId.ToString(CultureInfo.InvariantCulture), name));
        }

        foreach (string tag in tags)
            chips.Add(new FilterChip(FilterKind.Tag, tag, tag));

        return chips;
    }
}