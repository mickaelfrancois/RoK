namespace Rok.Logic.ViewModels.Artists;

public class ArtistsGroupCategory(ResourceLoader resourceLoader) : GroupCategoryService<ArtistViewModel, ArtistsGroupCategoryViewModel>(resourceLoader)
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
        RegisterStrategy(GroupingConstants.Decade, artists => GroupByDecade(artists, a => a.Artist.YearMini, a => a.Artist.Name));

        RegisterStrategy(GroupingConstants.Year, artists => GroupByDecade(artists, a => a.Artist.YearMini, a => a.Artist.Name));

        RegisterStrategy(GroupingConstants.Artist, artists => GroupByName(artists, a => a.Artist.Name, a => a.Artist.Name));

        RegisterStrategy(GroupingConstants.CreatDate, artists => GroupByCreatDate(artists, a => a.Artist.CreatDate));

        RegisterStrategy(GroupingConstants.LastListen, artists => GroupByLastListen(artists, a => a.Artist.LastListen));

        RegisterStrategy(GroupingConstants.ListenCount, artists => GroupByListenCount(artists, a => a.Artist.ListenCount));

        RegisterStrategy(GroupingConstants.Country, artists => GroupByCountry(artists, a => a.Artist.CountryCode, a => a.Artist.Name));
    }
}