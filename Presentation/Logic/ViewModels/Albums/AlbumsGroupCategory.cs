namespace Rok.Logic.ViewModels.Albums;

public class AlbumsGroupCategory(ResourceLoader _resourceLoader)
{
    public const string KGroupByYear = "YEAR";
    public const string KGroupByArtist = "ARTISTNAME";
    public const string KGroupByAlbum = "ALBUMNAME";
    public const string KGroupByCreatDate = "CREATDATE";
    public const string KGroupByDecade = "DECADE";
    public const string KGroupByCountry = "COUNTRY";
    public const string KGroupByLastListen = "LASTLISTEN";
    public const string KGroupByListenCount = "LISTENCOUNT";

    public string GetGroupByLabel(string groupBy)
    {
        return groupBy switch
        {
            KGroupByDecade => _resourceLoader.GetString("albumsViewGroupByYear"),
            KGroupByYear => _resourceLoader.GetString("albumsViewGroupByYear"),
            KGroupByCountry => _resourceLoader.GetString("albumsViewGroupByCountry"),
            KGroupByCreatDate => _resourceLoader.GetString("albumsViewGroupByCreatDate"),
            KGroupByArtist => _resourceLoader.GetString("albumsViewGroupByArtist"),
            KGroupByAlbum => _resourceLoader.GetString("albumsViewGroupByAlbum"),
            KGroupByLastListen => _resourceLoader.GetString("albumsViewGroupByLastListen"),
            KGroupByListenCount => _resourceLoader.GetString("albumsViewGroupByListenCount"),
            _ => groupBy,
        };
    }


    public static IEnumerable<AlbumsGroupCategoryViewModel> GetGroupedItems(string groupBy, List<AlbumViewModel> albums)
    {
        IEnumerable<AlbumsGroupCategoryViewModel> groupedAlbums = groupBy switch
        {
            KGroupByDecade => GroupByDecade(albums),
            KGroupByYear => GroupByYear(albums),
            KGroupByArtist => GroupByArtist(albums),
            KGroupByAlbum => GroupByAlbum(albums),
            KGroupByCreatDate => GroupByCreatDate(albums),
            KGroupByLastListen => GroupByLastListen(albums),
            KGroupByListenCount => GroupByListenCount(albums),
            KGroupByCountry => GroupByCountry(albums),
            _ => throw new ArgumentOutOfRangeException(nameof(groupBy), $"Unknown album group: '{groupBy}'"),
        };

        return groupedAlbums;
    }


    private static IEnumerable<AlbumsGroupCategoryViewModel> GroupByDecade(List<AlbumViewModel> albums)
    {
        IOrderedEnumerable<AlbumsGroupCategoryViewModel> selectedItems = albums
            .Where(c => c.Album.Year.HasValue)
            .GroupBy(x => (Math.Floor((decimal)x.Album.Year!.Value / 10) * 10).ToString())
            .Select(x => new AlbumsGroupCategoryViewModel { Title = x.Key ?? string.Empty, Items = x.OrderBy(c => c.Album.Name).ToList() })
            .OrderByDescending(c => c.Title);

        return BuildGroupedCollection(selectedItems, orderByDescending: true);
    }

    private static IEnumerable<AlbumsGroupCategoryViewModel> GroupByYear(List<AlbumViewModel> albums)
    {
        IOrderedEnumerable<AlbumsGroupCategoryViewModel> selectedItems = albums
            .Where(c => c.Album.Year.HasValue)
            .GroupBy(x => x.Album.Year?.ToString() ?? string.Empty)
            .Select(x => new AlbumsGroupCategoryViewModel { Title = x.Key ?? string.Empty, Items = x.OrderBy(c => c.Album.Name).ToList() })
            .OrderByDescending(c => c.Title);

        return BuildGroupedCollection(selectedItems, orderByDescending: true);
    }

    private static IEnumerable<AlbumsGroupCategoryViewModel> GroupByArtist(List<AlbumViewModel> albums)
    {
        IEnumerable<AlbumsGroupCategoryViewModel> selectedItems = albums.GroupBy(x => StringExtensions.GetNameFirstLetter(x.Album.ArtistName))
                  .Select(x => new AlbumsGroupCategoryViewModel { Title = x.Key, Items = x.OrderBy(c => c.Album.ArtistName).ToList() });

        return BuildGroupedCollection(selectedItems, orderByDescending: false);
    }

    private static IEnumerable<AlbumsGroupCategoryViewModel> GroupByAlbum(List<AlbumViewModel> albums)
    {
        IEnumerable<AlbumsGroupCategoryViewModel> selectedItems = albums.GroupBy(x => StringExtensions.GetNameFirstLetter(x.Album.Name))
                  .Select(x => new AlbumsGroupCategoryViewModel { Title = x.Key, Items = x.OrderBy(c => c.Album.Name).ToList() });

        return BuildGroupedCollection(selectedItems, orderByDescending: false);
    }

    private static List<AlbumsGroupCategoryViewModel> GroupByCreatDate(List<AlbumViewModel> albums)
    {
        DateTime minDate = DateTime.Now.AddYears(-1);

        IEnumerable<AlbumsGroupCategoryViewModel> selectedItems = albums.OrderByDescending(c => c.Album.CreatDate).GroupBy(x => x.Album.CreatDate > minDate ? x.Album.CreatDate.ToString("y") : $"< {minDate:y}")
                    .Select(x => new AlbumsGroupCategoryViewModel { Title = x.Key, Items = x.OrderByDescending(c => c.Album.CreatDate).ToList() });

        return BuildGroupedCollection(selectedItems);
    }

    private static IEnumerable<AlbumsGroupCategoryViewModel> GroupByLastListen(List<AlbumViewModel> albums)
    {
        DateTime minDate = DateTime.Now.AddDays(-15);

        IEnumerable<AlbumsGroupCategoryViewModel> selectedItems = albums.OrderByDescending(c => c.Album.LastListen).GroupBy(x =>
        {
            return x.Album.LastListen > minDate ? x.Album.LastListen.Value.ToString("m") : $"< {minDate:m}";
        }).Select(x => new AlbumsGroupCategoryViewModel { Title = x.Key, Items = x.OrderByDescending(c => c.Album.LastListen).ToList() });

        return BuildGroupedCollection(selectedItems, orderByDescending: true);
    }

    private static List<AlbumsGroupCategoryViewModel> GroupByListenCount(List<AlbumViewModel> albums)
    {
        IEnumerable<AlbumsGroupCategoryViewModel> selectedItems = albums.GroupBy(x => x.Album.ListenCount.ToString())
                  .Select(x => new AlbumsGroupCategoryViewModel { Title = x.Key, Items = x.ToList() });

        return BuildGroupedCollection(selectedItems.OrderByDescending(c => int.Parse(c.Title)));
    }

    private static IEnumerable<AlbumsGroupCategoryViewModel> GroupByCountry(List<AlbumViewModel> albums)
    {
        IEnumerable<AlbumsGroupCategoryViewModel> selectedItems = albums.GroupBy(x => string.IsNullOrEmpty(x.Album.CountryCode) ? "#123" : x.Album.CountryCode)
                     .Select(x => new AlbumsGroupCategoryViewModel { Title = x.Key, Items = x.OrderBy(c => c.Album.Name).ToList() });

        return BuildGroupedCollection(selectedItems, orderByDescending: false);
    }


    private static List<AlbumsGroupCategoryViewModel> BuildGroupedCollection(IEnumerable<AlbumsGroupCategoryViewModel> albums)
    {
        List<AlbumsGroupCategoryViewModel> groupedItems = [];

        albums.ToList().ForEach(c => groupedItems.Add(c));

        return groupedItems;
    }


    private static IEnumerable<AlbumsGroupCategoryViewModel> BuildGroupedCollection(IEnumerable<AlbumsGroupCategoryViewModel> albums, bool orderByDescending)
    {
        if (orderByDescending)
            return albums.OrderByDescending(c => c.Title);
        else
            return albums.OrderBy(c => c.Title);
    }
}
