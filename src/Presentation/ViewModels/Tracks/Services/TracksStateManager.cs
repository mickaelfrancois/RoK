using Rok.Application.Services.Grouping;

namespace Rok.ViewModels.Tracks.Services;


public class TracksStateManager(IAppOptions appOptions) : ViewStateManager(appOptions)
{
    protected override string GetDefaultGroupBy() => GroupingConstants.Title;

    protected override string? GetStoredGroupBy() => AppOptions.TracksGroupBy;

    protected override void SaveGroupBy(string value) => AppOptions.TracksGroupBy = value;

    protected override List<string> GetStoredFilters() => AppOptions.TracksFilterBy;

    protected override List<string> GetStoredTagFilters() => AppOptions.TracksFilterByTags;

    protected override void SaveFilters(List<string> filters) => AppOptions.TracksFilterBy = filters;

    protected override List<long> GetStoredGenreFilters() => AppOptions.TracksFilterByGenresId;

    protected override void SaveGenreFilters(List<long> filters) => AppOptions.TracksFilterByGenresId = filters;

    protected override void SaveTagFilters(List<string> tags) => AppOptions.TracksFilterByTags = tags;
}