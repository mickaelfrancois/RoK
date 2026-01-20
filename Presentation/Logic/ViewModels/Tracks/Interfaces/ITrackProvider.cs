namespace Rok.Logic.ViewModels.Tracks.Interfaces;

public record TrackProviderResult(List<TrackViewModel> FilteredItems, IEnumerable<TracksGroupCategoryViewModel> Groups, bool IsGroupingEnabled);

public interface ITrackProvider
{
    List<TrackViewModel> ViewModels { get; }

    List<GenreDto> Genres { get; }

    Task LoadAsync();

    void SetTracks(List<TrackDto> tracks);

    TrackProviderResult GetProcessedData(string groupBy, List<string> filters, List<long> genreFilters);

    void Clear();

    string GetFilterLabel(string filter);

    string GetGroupByLabel(string groupBy);
}