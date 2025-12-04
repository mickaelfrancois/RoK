namespace Rok.Logic.ViewModels.Tracks.Services;

public class TracksStateManager(IAppOptions appOptions)
{
    public string GroupBy { get; set; } = TracksGroupCategory.KGroupByTitle;

    public List<string> SelectedFilters { get; set; } = [];

    public List<long> SelectedGenreFilters { get; set; } = [];

    public void Load()
    {
        GroupBy = string.IsNullOrEmpty(appOptions.TracksGroupBy)
            ? TracksGroupCategory.KGroupByAlbum
            : appOptions.TracksGroupBy;

        SelectedFilters = appOptions.TracksFilterBy;
        SelectedGenreFilters = appOptions.TracksFilterByGenresId;
    }

    public void Save()
    {
        appOptions.TracksGroupBy = GroupBy;
        appOptions.TracksFilterBy = SelectedFilters;
        appOptions.TracksFilterByGenresId = SelectedGenreFilters;
    }
}