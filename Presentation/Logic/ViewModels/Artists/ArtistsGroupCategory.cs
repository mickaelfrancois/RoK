namespace Rok.Logic.ViewModels.Artists;

public class ArtistsGroupCategory(ResourceLoader _resourceLoader)
{
    public const string KGroupByYear = "YEAR";
    public const string KGroupByArtist = "ARTISTNAME";
    public const string KGroupByCreatDate = "CREATDATE";
    public const string KGroupByDecade = "DECADE";
    public const string KGroupByCountry = "COUNTRY";
    public const string KGroupByLastListen = "LASTLISTEN";
    public const string KGroupByListenCount = "LISTENCOUNT";

    public string GetGroupByLabel(string groupBy)
    {
        return groupBy switch
        {
            KGroupByYear => _resourceLoader.GetString("artistsViewGroupByYear"),
            KGroupByDecade => _resourceLoader.GetString("artistsViewGroupByDecade"),
            KGroupByCountry => _resourceLoader.GetString("artistsViewGroupByCountry"),
            KGroupByCreatDate => _resourceLoader.GetString("artistsViewGroupByCreatDate"),
            KGroupByArtist => _resourceLoader.GetString("artistsViewGroupByArtist"),
            KGroupByLastListen => _resourceLoader.GetString("artistsViewGroupByLastListen"),
            KGroupByListenCount => _resourceLoader.GetString("artistsViewGroupByListenCount"),
            _ => groupBy,
        };
    }


    public static IEnumerable<ArtistsGroupCategoryViewModel> GetGroupedItems(string groupBy, List<ArtistViewModel> artists)
    {
        IEnumerable<ArtistsGroupCategoryViewModel> groupedArtists = groupBy switch
        {
            KGroupByDecade => GroupByDecade(artists),
            KGroupByYear => GroupByYear(artists),
            KGroupByArtist => GroupByArtist(artists),
            KGroupByCreatDate => GroupByCreatDate(artists),
            KGroupByLastListen => GroupByLastListen(artists),
            KGroupByListenCount => GroupByListenCount(artists),
            KGroupByCountry => GroupByCountry(artists),
            _ => throw new ArgumentOutOfRangeException(nameof(groupBy), $"Unknown artist group: '{groupBy}'"),
        };

        return groupedArtists;
    }


    private static IEnumerable<ArtistsGroupCategoryViewModel> GroupByDecade(List<ArtistViewModel> artists)
    {
        IOrderedEnumerable<ArtistsGroupCategoryViewModel> selectedItems = artists
            .Where(c => c.Artist.YearMini.HasValue)
            .GroupBy(x => (Math.Floor((decimal)x.Artist.YearMini!.Value / 10) * 10).ToString())
            .Select(x => new ArtistsGroupCategoryViewModel { Title = x.Key, Items = x.OrderBy(c => c.Artist.Name).ToList() })
            .OrderByDescending(c => c.Title);

        return BuildGroupedCollection(selectedItems, orderByDescending: true);
    }

    private static IEnumerable<ArtistsGroupCategoryViewModel> GroupByYear(List<ArtistViewModel> artists)
    {
        IOrderedEnumerable<ArtistsGroupCategoryViewModel> selectedItems = artists
        .Where(c => c.Artist.YearMini.HasValue)
        .GroupBy(x => (Math.Floor((decimal)x.Artist.YearMini!.Value / 10) * 10).ToString())
        .Select(x => new ArtistsGroupCategoryViewModel { Title = x.Key ?? string.Empty, Items = x.OrderBy(c => c.Artist.Name).ToList() })
        .OrderByDescending(c => c.Title);

        return BuildGroupedCollection(selectedItems, orderByDescending: true);
    }

    private static IEnumerable<ArtistsGroupCategoryViewModel> GroupByArtist(List<ArtistViewModel> artists)
    {
        IEnumerable<ArtistsGroupCategoryViewModel> selectedItems = artists.GroupBy(x => StringExtensions.GetNameFirstLetter(x.Artist.Name))
                  .Select(x => new ArtistsGroupCategoryViewModel { Title = x.Key, Items = x.OrderBy(c => c.Artist.Name).ToList() });

        return BuildGroupedCollection(selectedItems, orderByDescending: false);
    }

    private static List<ArtistsGroupCategoryViewModel> GroupByCreatDate(List<ArtistViewModel> artists)
    {
        DateTime minDate = DateTime.Now.AddYears(-1);

        IEnumerable<ArtistsGroupCategoryViewModel> selectedItems = artists.OrderByDescending(c => c.Artist.CreatDate).GroupBy(x => x.Artist.CreatDate > minDate ? x.Artist.CreatDate.ToString("y") : $"< {minDate:y}")
                    .Select(x => new ArtistsGroupCategoryViewModel { Title = x.Key, Items = x.OrderByDescending(c => c.Artist.CreatDate).ToList() });

        return BuildGroupedCollection(selectedItems);
    }

    private static IEnumerable<ArtistsGroupCategoryViewModel> GroupByLastListen(List<ArtistViewModel> artists)
    {
        DateTime minDate = DateTime.Now.AddDays(-15);

        IEnumerable<ArtistsGroupCategoryViewModel> selectedItems = artists.OrderByDescending(c => c.Artist.LastListen).GroupBy(x =>
        {
            return x.Artist.LastListen > minDate ? x.Artist.LastListen.Value.ToString("m") : $"< {minDate:m}";
        }).Select(x => new ArtistsGroupCategoryViewModel { Title = x.Key, Items = x.OrderByDescending(c => c.Artist.LastListen).ToList() });

        return BuildGroupedCollection(selectedItems, orderByDescending: true);
    }

    private static List<ArtistsGroupCategoryViewModel> GroupByListenCount(List<ArtistViewModel> artists)
    {
        IEnumerable<ArtistsGroupCategoryViewModel> selectedItems = artists.GroupBy(x => x.Artist.ListenCount.ToString())
                  .Select(x => new ArtistsGroupCategoryViewModel { Title = x.Key, Items = x.ToList() });

        return BuildGroupedCollection(selectedItems.OrderByDescending(c => Int32.Parse(c.Title)));
    }

    private static IEnumerable<ArtistsGroupCategoryViewModel> GroupByCountry(List<ArtistViewModel> artists)
    {
        IEnumerable<ArtistsGroupCategoryViewModel> selectedItems = artists.GroupBy(x => string.IsNullOrEmpty(x.Artist.CountryCode) ? "#123" : x.Artist.CountryCode)
                     .Select(x => new ArtistsGroupCategoryViewModel { Title = x.Key, Items = x.OrderBy(c => c.Artist.Name).ToList() });

        return BuildGroupedCollection(selectedItems, orderByDescending: false);
    }


    private static List<ArtistsGroupCategoryViewModel> BuildGroupedCollection(IEnumerable<ArtistsGroupCategoryViewModel> artists)
    {
        List<ArtistsGroupCategoryViewModel> groupedItems = [];

        artists.ToList().ForEach(c => groupedItems.Add(c));

        return groupedItems;
    }


    private static IEnumerable<ArtistsGroupCategoryViewModel> BuildGroupedCollection(IEnumerable<ArtistsGroupCategoryViewModel> artists, bool orderByDescending)
    {
        if (orderByDescending)
            return artists.OrderByDescending(c => c.Title);
        else
            return artists.OrderBy(c => c.Title);
    }
}
