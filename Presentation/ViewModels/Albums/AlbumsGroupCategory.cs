using Rok.ViewModels.Album;

namespace Rok.ViewModels.Albums;

public class AlbumsGroupCategory(ResourceLoader resourceLoader) : GroupCategoryService<AlbumViewModel, AlbumsGroupCategoryViewModel>(resourceLoader)
{
    public override string GetGroupByLabel(string groupBy)
    {
        return groupBy switch
        {
            GroupingConstants.Decade => ResourceLoader.GetString("albumsViewGroupByYear"),
            GroupingConstants.Year => ResourceLoader.GetString("albumsViewGroupByYear"),
            GroupingConstants.Country => ResourceLoader.GetString("albumsViewGroupByCountry"),
            GroupingConstants.CreatDate => ResourceLoader.GetString("albumsViewGroupByCreatDate"),
            GroupingConstants.Artist => ResourceLoader.GetString("albumsViewGroupByArtist"),
            GroupingConstants.Album => ResourceLoader.GetString("albumsViewGroupByAlbum"),
            GroupingConstants.LastListen => ResourceLoader.GetString("albumsViewGroupByLastListen"),
            GroupingConstants.ListenCount => ResourceLoader.GetString("albumsViewGroupByListenCount"),
            _ => groupBy,
        };
    }

    protected override void RegisterGroupingStrategies()
    {
        RegisterStrategy(GroupingConstants.None, albums => GroupByName(albums, a => a.Album.Name, a => a.Album.Name));

        RegisterStrategy(GroupingConstants.Decade, albums => GroupByDecade(albums, a => a.Album.Year, a => a.Album.Name));
        RegisterStrategy(GroupingConstants.Year, albums => GroupByYear(albums, a => a.Album.Year, a => a.Album.Name));
        RegisterStrategy(GroupingConstants.Artist, albums => GroupByName(albums, a => a.Album.ArtistName, a => a.Album.ArtistName));
        RegisterStrategy(GroupingConstants.Album, albums => GroupByName(albums, a => a.Album.Name, a => a.Album.Name));
        RegisterStrategy(GroupingConstants.CreatDate, albums => GroupByCreatDate(albums, a => a.Album.CreatDate));
        RegisterStrategy(GroupingConstants.LastListen, albums => SortByLastListen(albums, a => a.Album.LastListen));
        RegisterStrategy(GroupingConstants.ListenCount, albums => SortByListenCount(albums, a => a.Album.ListenCount));
        RegisterStrategy(GroupingConstants.Country, albums => GroupByCountry(albums, a => a.Album.CountryCode, a => a.Album.Name));
    }
}