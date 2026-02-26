using Rok.Application.Interfaces;

namespace Rok.Application.Services.Grouping;

public class AlbumsGroupCategory(IResourceService resourceLoader)
    : GroupCategoryService<IGroupableAlbum, AlbumGroupResult>(resourceLoader)
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
        RegisterStrategy(GroupingConstants.None, albums => GroupByName(albums, a => a.Name, a => a.Name));

        RegisterStrategy(GroupingConstants.Decade, albums => GroupByDecade(albums, a => a.Year, a => a.Name));
        RegisterStrategy(GroupingConstants.Year, albums => GroupByYear(albums, a => a.Year, a => a.Name));
        RegisterStrategy(GroupingConstants.Artist, albums => GroupByName(albums, a => a.ArtistName, a => a.ArtistName));
        RegisterStrategy(GroupingConstants.Album, albums => GroupByName(albums, a => a.Name, a => a.Name));
        RegisterStrategy(GroupingConstants.CreatDate, albums => GroupByCreatDate(albums, a => a.CreatDate));
        RegisterStrategy(GroupingConstants.LastListen, albums => SortByLastListen(albums, a => a.LastListen));
        RegisterStrategy(GroupingConstants.ListenCount, albums => SortByListenCount(albums, a => a.ListenCount));
        RegisterStrategy(GroupingConstants.Country, albums => GroupByCountry(albums, a => a.CountryCode, a => a.Name));
    }
}
