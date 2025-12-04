namespace Rok.Logic.ViewModels.Albums.Services;

public class AlbumsStateManager(IAppOptions appOptions)
{
    public string GroupBy { get; set; } = AlbumsGroupCategory.KGroupByAlbum;

    public List<string> SelectedFilters { get; set; } = [];

    public List<long> SelectedGenreFilters { get; set; } = [];

    public void Load()
    {
        GroupBy = string.IsNullOrEmpty(appOptions.AlbumsGroupBy)
            ? AlbumsGroupCategory.KGroupByAlbum
            : appOptions.AlbumsGroupBy;

        SelectedFilters = appOptions.AlbumsFilterBy;
        SelectedGenreFilters = appOptions.AlbumsFilterByGenresId;
    }

    public void Save()
    {
        appOptions.AlbumsGroupBy = GroupBy;
        appOptions.AlbumsFilterBy = SelectedFilters;
        appOptions.AlbumsFilterByGenresId = SelectedGenreFilters;
    }
}
