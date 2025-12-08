namespace Rok.Logic.ViewModels.Artists;

public class ArtistsGroupCategory(ResourceLoader resourceLoader) : GroupCategoryService<ArtistViewModel, ArtistsGroupCategoryViewModel>(resourceLoader)
{
    public const string KGroupByYear = "YEAR";
    public const string KGroupByArtist = "ARTISTNAME";
    public const string KGroupByCreatDate = "CREATDATE";
    public const string KGroupByDecade = "DECADE";
    public const string KGroupByCountry = "COUNTRY";
    public const string KGroupByLastListen = "LASTLISTEN";
    public const string KGroupByListenCount = "LISTENCOUNT";

    public override string GetGroupByLabel(string groupBy)
    {
        return groupBy switch
        {
            KGroupByYear => ResourceLoader.GetString("artistsViewGroupByYear"),
            KGroupByDecade => ResourceLoader.GetString("artistsViewGroupByDecade"),
            KGroupByCountry => ResourceLoader.GetString("artistsViewGroupByCountry"),
            KGroupByCreatDate => ResourceLoader.GetString("artistsViewGroupByCreatDate"),
            KGroupByArtist => ResourceLoader.GetString("artistsViewGroupByArtist"),
            KGroupByLastListen => ResourceLoader.GetString("artistsViewGroupByLastListen"),
            KGroupByListenCount => ResourceLoader.GetString("artistsViewGroupByListenCount"),
            _ => groupBy,
        };
    }

    protected override void RegisterGroupingStrategies()
    {
        RegisterStrategy(KGroupByDecade, artists =>
            GroupByDecade(artists, a => a.Artist.YearMini, a => a.Artist.Name));

        RegisterStrategy(KGroupByYear, artists =>
            GroupByDecade(artists, a => a.Artist.YearMini, a => a.Artist.Name));

        RegisterStrategy(KGroupByArtist, artists =>
            GroupByName(artists, a => a.Artist.Name, a => a.Artist.Name));

        RegisterStrategy(KGroupByCreatDate, artists =>
            GroupByCreatDate(artists, a => a.Artist.CreatDate));

        RegisterStrategy(KGroupByLastListen, artists =>
            GroupByLastListen(artists, a => a.Artist.LastListen));

        RegisterStrategy(KGroupByListenCount, artists =>
            GroupByListenCount(artists, a => a.Artist.ListenCount));

        RegisterStrategy(KGroupByCountry, artists =>
            GroupByCountry(artists, a => a.Artist.CountryCode, a => a.Artist.Name));
    }
}