using Rok.Logic.ViewModels.Tracks;
using Rok.ViewModels.Tracks;
using Rok.ViewModels.Tracks.Interfaces;

namespace Rok.ViewModels.Tracks.Services;

public class TrackProvider(TracksDataLoader dataLoader, TracksFilter filterService, TracksGroupCategory groupService) : ITrackProvider
{
    public List<TrackViewModel> ViewModels => dataLoader.ViewModels;
    public List<GenreDto> Genres => dataLoader.Genres;

    public async Task LoadAsync()
    {
        await dataLoader.LoadGenresAsync();
        await dataLoader.LoadTracksAsync();
    }

    public void SetTracks(List<TrackDto> albums) => dataLoader.SetTracks(albums);

    public TrackProviderResult GetProcessedData(string groupBy, List<string> filters, List<long> genreFilters)
    {
        IEnumerable<TrackViewModel> filtered = dataLoader.ViewModels;

        foreach (string filter in filters)
            filtered = filterService.Filter(filter, filtered);

        foreach (long genreId in genreFilters)
            filtered = filterService.FilterByGenreId(genreId, filtered);

        List<TrackViewModel> filteredList = filtered.ToList();
        List<TracksGroupCategoryViewModel> groups = groupService.GetGroupedItems(groupBy, filteredList).ToList();

        bool isGroupingEnabled = groups.Count > 1 || !string.IsNullOrEmpty(groups.FirstOrDefault()?.Title ?? string.Empty);

        return new TrackProviderResult(filteredList, groups, isGroupingEnabled);
    }

    public void Clear() => dataLoader.Clear();

    public string GetFilterLabel(string filter) => filterService.GetLabel(filter);

    public string GetGroupByLabel(string groupBy) => groupService.GetGroupByLabel(groupBy);
}