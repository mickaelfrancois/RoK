namespace Rok.Logic.ViewModels.Albums.Interfaces;

public record AlbumProviderResult(List<AlbumViewModel> FilteredItems, IEnumerable<AlbumsGroupCategoryViewModel> Groups, bool IsGroupingEnabled);

public interface IAlbumProvider
{
    List<AlbumViewModel> ViewModels { get; }

    List<GenreDto> Genres { get; }

    Task LoadAsync();

    void SetAlbums(List<AlbumDto> albums);

    AlbumProviderResult GetProcessedData(string groupBy, List<string> filters, List<long> genreFilters);

    void Clear();

    string GetFilterLabel(string filter);

    string GetGroupByLabel(string groupBy);
}