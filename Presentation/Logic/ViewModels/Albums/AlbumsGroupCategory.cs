namespace Rok.Logic.ViewModels.Albums;

public class AlbumsGroupCategory(ResourceLoader resourceLoader) : GroupCategoryService<AlbumViewModel, AlbumsGroupCategoryViewModel>(resourceLoader)
{
    public const string KGroupByYear = "YEAR";
    public const string KGroupByArtist = "ARTISTNAME";
    public const string KGroupByAlbum = "ALBUMNAME";
    public const string KGroupByCreatDate = "CREATDATE";
    public const string KGroupByDecade = "DECADE";
    public const string KGroupByCountry = "COUNTRY";
    public const string KGroupByLastListen = "LASTLISTEN";
    public const string KGroupByListenCount = "LISTENCOUNT";

    public override string GetGroupByLabel(string groupBy)
    {
        return groupBy switch
        {
            KGroupByDecade => ResourceLoader.GetString("albumsViewGroupByYear"),
            KGroupByYear => ResourceLoader.GetString("albumsViewGroupByYear"),
            KGroupByCountry => ResourceLoader.GetString("albumsViewGroupByCountry"),
            KGroupByCreatDate => ResourceLoader.GetString("albumsViewGroupByCreatDate"),
            KGroupByArtist => ResourceLoader.GetString("albumsViewGroupByArtist"),
            KGroupByAlbum => ResourceLoader.GetString("albumsViewGroupByAlbum"),
            KGroupByLastListen => ResourceLoader.GetString("albumsViewGroupByLastListen"),
            KGroupByListenCount => ResourceLoader.GetString("albumsViewGroupByListenCount"),
            _ => groupBy,
        };
    }

    protected override void RegisterGroupingStrategies()
    {
        RegisterStrategy(KGroupByDecade, albums =>
            GroupByDecade(albums, a => a.Album.Year, a => a.Album.Name));

        RegisterStrategy(KGroupByYear, albums =>
            GroupByYear(albums, a => a.Album.Year, a => a.Album.Name));

        RegisterStrategy(KGroupByArtist, albums =>
            GroupByName(albums, a => a.Album.ArtistName, a => a.Album.ArtistName));

        RegisterStrategy(KGroupByAlbum, albums =>
            GroupByName(albums, a => a.Album.Name, a => a.Album.Name));

        RegisterStrategy(KGroupByCreatDate, albums =>
            GroupByCreatDate(albums, a => a.Album.CreatDate));

        RegisterStrategy(KGroupByLastListen, albums =>
            GroupByLastListen(albums, a => a.Album.LastListen));

        RegisterStrategy(KGroupByListenCount, albums =>
            GroupByListenCount(albums, a => a.Album.ListenCount));

        RegisterStrategy(KGroupByCountry, albums =>
            GroupByCountry(albums, a => a.Album.CountryCode, a => a.Album.Name));
    }
}