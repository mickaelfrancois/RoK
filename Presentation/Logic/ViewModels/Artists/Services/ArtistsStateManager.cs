namespace Rok.Logic.ViewModels.Artists.Services;

public class ArtistsStateManager(IAppOptions appOptions)
{
    public string GroupBy { get; set; } = ArtistsGroupCategory.KGroupByArtist;

    public List<string> SelectedFilters { get; set; } = [];

    public List<long> SelectedGenreFilters { get; set; } = [];

    public void Load()
    {
        GroupBy = string.IsNullOrEmpty(appOptions.ArtistsGroupBy)
            ? ArtistsGroupCategory.KGroupByArtist
            : appOptions.ArtistsGroupBy;

        SelectedFilters = appOptions.ArtistsFilterBy;
        SelectedGenreFilters = appOptions.ArtistsFilterByGenresId;
    }

    public void Save()
    {
        appOptions.ArtistsGroupBy = GroupBy;
        appOptions.ArtistsFilterBy = SelectedFilters;
        appOptions.ArtistsFilterByGenresId = SelectedGenreFilters;
    }
}