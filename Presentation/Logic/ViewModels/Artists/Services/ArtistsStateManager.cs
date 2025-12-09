namespace Rok.Logic.ViewModels.Artists.Services;

public class ArtistsStateManager(IAppOptions appOptions) : ViewStateManager(appOptions)
{
    protected override string GetDefaultGroupBy() => ArtistsGroupCategory.KGroupByArtist;

    protected override string? GetStoredGroupBy() => AppOptions.ArtistsGroupBy;

    protected override void SaveGroupBy(string value) => AppOptions.ArtistsGroupBy = value;

    protected override List<string> GetStoredFilters() => AppOptions.ArtistsFilterBy;

    protected override void SaveFilters(List<string> filters) => AppOptions.ArtistsFilterBy = filters;

    protected override List<long> GetStoredGenreFilters() => AppOptions.ArtistsFilterByGenresId;

    protected override void SaveGenreFilters(List<long> filters) => AppOptions.ArtistsFilterByGenresId = filters;
}