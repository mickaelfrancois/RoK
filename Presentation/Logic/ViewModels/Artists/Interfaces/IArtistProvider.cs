namespace Rok.Logic.ViewModels.Artists.Interfaces;

public record ArtistProviderResult(List<ArtistViewModel> FilteredItems, IEnumerable<ArtistsGroupCategoryViewModel> Groups, bool IsGroupingEnabled);

public interface IArtistProvider
{
    List<ArtistViewModel> ViewModels { get; }

    List<GenreDto> Genres { get; }

    Task LoadAsync(bool excludeArtistsWithoutAlbum);

    void SetArtists(List<ArtistDto> artists);

    ArtistProviderResult GetProcessedData(string groupBy, List<string> filters, List<long> genreFilters);

    void Clear();

    string GetFilterLabel(string filter);

    string GetGroupByLabel(string groupBy);
}