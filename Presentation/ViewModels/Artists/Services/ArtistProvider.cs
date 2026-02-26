using Rok.Application.Services.Filters;
using Rok.Application.Services.Grouping;
using Rok.ViewModels.Artist;
using Rok.ViewModels.Artists.Interfaces;

namespace Rok.ViewModels.Artists.Services;

public class ArtistProvider(TagsProvider tagsLoader, ArtistsDataLoader dataLoader, ArtistsFilter filterService, ArtistsGroupCategory groupService)
    : IArtistProvider
{
    public List<ArtistViewModel> ViewModels => dataLoader.ViewModels;
    public List<GenreDto> Genres => dataLoader.Genres;


    public async Task LoadAsync(bool excludeArtistsWithoutAlbum)
    {
        await dataLoader.LoadGenresAsync();
        await dataLoader.LoadArtistsAsync(excludeArtistsWithoutAlbum);
    }

    public void SetArtists(List<ArtistDto> artists) => dataLoader.SetArtists(artists);

    public ArtistProviderResult GetProcessedData(string groupBy, List<string> filters, List<long> genreFilters, List<string> tagFilters)
    {
        IEnumerable<IFilterableArtist> filtered = dataLoader.ViewModels;

        foreach (string filter in filters)
            filtered = filterService.Filter(filter, filtered);

        foreach (long genreId in genreFilters)
            filtered = filterService.FilterByGenreId(genreId, filtered);

        if (tagFilters.Count > 0)
            filtered = filterService.FilterByTags(tagFilters, filtered);

        List<ArtistViewModel> filteredList = filtered.Cast<ArtistViewModel>().ToList();
        List<ArtistsGroupCategoryViewModel> groups = groupService
            .GetGroupedItems(groupBy, filteredList.Cast<IGroupableArtist>().ToList())
            .Select(g => new ArtistsGroupCategoryViewModel { Title = g.Title, Items = g.Items.Cast<ArtistViewModel>().ToList() })
            .ToList();

        bool isGroupingEnabled = groups.Count > 1 || !string.IsNullOrEmpty(groups.FirstOrDefault()?.Title ?? string.Empty);

        return new ArtistProviderResult(filteredList, groups, isGroupingEnabled);
    }

    public void Clear() => dataLoader.Clear();

    public string GetFilterLabel(string filter) => filterService.GetLabel(filter);

    public string GetGroupByLabel(string groupBy) => groupService.GetGroupByLabel(groupBy);
}