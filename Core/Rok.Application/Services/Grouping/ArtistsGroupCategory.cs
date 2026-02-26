using Rok.Application.Interfaces;

namespace Rok.Application.Services.Grouping;

public class ArtistsGroupCategory(IResourceService resourceLoader)
    : GroupCategoryService<IGroupableArtist, ArtistGroupResult>(resourceLoader)
{
    public override string GetGroupByLabel(string groupBy)
    {
        return groupBy switch
        {
            GroupingConstants.Year => ResourceLoader.GetString("artistsViewGroupByYear"),
            GroupingConstants.Decade => ResourceLoader.GetString("artistsViewGroupByDecade"),
            GroupingConstants.Country => ResourceLoader.GetString("artistsViewGroupByCountry"),
            GroupingConstants.CreatDate => ResourceLoader.GetString("artistsViewGroupByCreatDate"),
            GroupingConstants.Artist => ResourceLoader.GetString("artistsViewGroupByArtist"),
            GroupingConstants.LastListen => ResourceLoader.GetString("artistsViewGroupByLastListen"),
            GroupingConstants.ListenCount => ResourceLoader.GetString("artistsViewGroupByListenCount"),
            _ => groupBy,
        };
    }

    protected override void RegisterGroupingStrategies()
    {
        RegisterStrategy(GroupingConstants.None, artists => GroupByName(artists, a => a.Name, a => a.Name));

        RegisterStrategy(GroupingConstants.Decade, artists => GroupByDecade(artists, a => a.YearMini, a => a.Name));
        RegisterStrategy(GroupingConstants.Year, artists => GroupByDecade(artists, a => a.YearMini, a => a.Name));
        RegisterStrategy(GroupingConstants.Artist, artists => GroupByName(artists, a => a.Name, a => a.Name));
        RegisterStrategy(GroupingConstants.CreatDate, artists => GroupByCreatDate(artists, a => a.CreatDate));
        RegisterStrategy(GroupingConstants.LastListen, artists => SortByLastListen(artists, a => a.LastListen));
        RegisterStrategy(GroupingConstants.ListenCount, artists => SortByListenCount(artists, a => a.ListenCount));
        RegisterStrategy(GroupingConstants.Country, artists => GroupByCountry(artists, a => a.CountryCode, a => a.Name));
    }
}
