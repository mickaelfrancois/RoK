namespace Rok.Logic.ViewModels.Artists.Services;

public class ArtistsStateManager(IAppOptions appOptions) : ViewStateManager(appOptions)
{
    protected override string GetDefaultGroupBy() => GroupingConstants.Artist;

    protected override string? GetStoredGroupBy() => AppOptions.ArtistsGroupBy;

    protected override void SaveGroupBy(string value) => AppOptions.ArtistsGroupBy = value;

    protected override List<string> GetStoredFilters() => AppOptions.ArtistsFilterBy;

    protected override void SaveFilters(List<string> filters) => AppOptions.ArtistsFilterBy = filters;

    protected override List<long> GetStoredGenreFilters() => AppOptions.ArtistsFilterByGenresId;

    protected override List<string> GetStoredTagFilters() => AppOptions.ArtistsFilterByTags;

    protected override void SaveGenreFilters(List<long> filters) => AppOptions.ArtistsFilterByGenresId = filters;

    protected override void SaveTagFilters(List<string> tags) => AppOptions.ArtistsFilterByTags = tags;
}