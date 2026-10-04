namespace Rok.ViewModels.Genre.Services;

/// <summary>Chooses the artist whose picture and backdrop illustrate a genre.</summary>
public static class GenreArtistPicker
{
    /// <summary>Returns a random non-empty artist name, or <c>null</c> when no album carries one.</summary>
    public static string? PickArtistName(IEnumerable<string?> artistNames, Random random)
    {
        ArgumentNullException.ThrowIfNull(artistNames);
        ArgumentNullException.ThrowIfNull(random);

        List<string> names = artistNames.Where(name => !string.IsNullOrEmpty(name)).Select(name => name!).ToList();

        return names.Count == 0 ? null : names[random.Next(names.Count)];
    }
}