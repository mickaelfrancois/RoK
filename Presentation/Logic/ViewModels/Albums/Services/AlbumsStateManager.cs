namespace Rok.Logic.ViewModels.Albums.Services;


public class AlbumsStateManager(IAppOptions appOptions) : ViewStateManager(appOptions)
{
    protected override string GetDefaultGroupBy() => GroupingConstants.Album;

    protected override string? GetStoredGroupBy() => AppOptions.AlbumsGroupBy;

    protected override void SaveGroupBy(string value) => AppOptions.AlbumsGroupBy = value;

    protected override List<string> GetStoredFilters() => AppOptions.AlbumsFilterBy;

    protected override void SaveFilters(List<string> filters) => AppOptions.AlbumsFilterBy = filters;

    protected override List<long> GetStoredGenreFilters() => AppOptions.AlbumsFilterByGenresId;

    protected override List<string> GetStoredTagFilters() => AppOptions.AlbumsFilterByTags;

    protected override void SaveGenreFilters(List<long> filters) => AppOptions.AlbumsFilterByGenresId = filters;

    protected override void SaveTagFilters(List<string> tags) => AppOptions.AlbumsFilterByTags = tags;
}