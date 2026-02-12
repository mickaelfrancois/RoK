using Rok.ViewModels.Album;
using Rok.ViewModels.Albums.Interfaces;

namespace Rok.ViewModels.Albums.Services;

public class AlbumProvider(TagsProvider tagsLoader, AlbumsDataLoader dataLoader, AlbumsFilter filterService, AlbumsGroupCategory groupService)
    : IAlbumProvider
{
    public List<AlbumViewModel> ViewModels => dataLoader.ViewModels;
    public List<GenreDto> Genres => dataLoader.Genres;


    public async Task LoadAsync()
    {
        await dataLoader.LoadGenresAsync();
        await dataLoader.LoadAlbumsAsync();
    }

    public void SetAlbums(List<AlbumDto> albums) => dataLoader.SetAlbums(albums);

    public AlbumProviderResult GetProcessedData(string groupBy, List<string> filters, List<long> genreFilters, List<string> tagFilters)
    {
        IEnumerable<AlbumViewModel> filtered = dataLoader.ViewModels;

        foreach (string filter in filters)
            filtered = filterService.Filter(filter, filtered);

        foreach (long genreId in genreFilters)
            filtered = filterService.FilterByGenreId(genreId, filtered);

        if (tagFilters.Count > 0)
            filtered = filterService.FilterByTags(tagFilters, filtered);

        List<AlbumViewModel> filteredList = filtered.ToList();
        List<AlbumsGroupCategoryViewModel> groups = groupService.GetGroupedItems(groupBy, filteredList).ToList();

        bool isGroupingEnabled = groups.Count > 1 || !string.IsNullOrEmpty(groups.FirstOrDefault()?.Title ?? string.Empty);

        return new AlbumProviderResult(filteredList, groups, isGroupingEnabled);
    }

    public void Clear() => dataLoader.Clear();

    public string GetFilterLabel(string filter) => filterService.GetLabel(filter);

    public string GetGroupByLabel(string groupBy) => groupService.GetGroupByLabel(groupBy);
}