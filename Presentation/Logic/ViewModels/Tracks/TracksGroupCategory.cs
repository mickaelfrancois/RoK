namespace Rok.Logic.ViewModels.Tracks;

public class TracksGroupCategory(ResourceLoader _resourceLoader)
{
    public const string KGroupByYear = "YEAR";
    public const string KGroupByTitle = "TITLE";
    public const string KGroupByCreatDate = "CREATDATE";
    public const string KGroupByDecade = "DECADE";
    public const string KGroupByCountry = "COUNTRY";
    public const string KGroupByAlbum = "ALBUMNAME";
    public const string KGroupByArtist = "ARTISTNAME";
    public const string KGroupByGenre = "GENRENAME";
    public const string KGroupByLastListen = "LASTLISTEN";
    public const string KGroupByListenCount = "LISTENCOUNT";

    public string GetGroupByLabel(string groupBy)
    {
        return groupBy switch
        {
            "TITLE" => _resourceLoader.GetString("tracksViewGroupByTitle"),
            "COUNTRY" => _resourceLoader.GetString("tracksViewGroupByCountry"),
            "CREATDATE" => _resourceLoader.GetString("tracksViewGroupByCreatDate"),
            "LASTLISTEN" => _resourceLoader.GetString("tracksViewGroupByLastListen"),
            "LISTENCOUNT" => _resourceLoader.GetString("tracksViewGroupByListenCount"),
            "ARTISTNAME" => _resourceLoader.GetString("tracksViewGroupByArtist"),
            "ALBUMNAME" => _resourceLoader.GetString("tracksViewGroupByAlbum"),
            "GENRENAME" => _resourceLoader.GetString("tracksViewGroupByGenre"),
            _ => groupBy,
        };
    }


    public static IEnumerable<TracksGroupCategoryViewModel> GetGroupedItems(string groupBy, List<TrackViewModel> tracks)
    {
        IEnumerable<TracksGroupCategoryViewModel> groupedTracks = groupBy switch
        {
            KGroupByTitle => GroupByTitle(tracks),
            KGroupByLastListen => GroupByLastListen(tracks),
            KGroupByListenCount => GroupByListenCount(tracks),
            KGroupByCountry => GroupByCountry(tracks),
            KGroupByCreatDate => GroupByCreatDate(tracks),
            KGroupByAlbum => GroupByAlbum(tracks),
            KGroupByArtist => GroupByArtist(tracks),
            KGroupByGenre => GroupByGenre(tracks),

            _ => throw new ArgumentOutOfRangeException(nameof(groupBy), $"Unknown track group: '{groupBy}'"),
        };

        return groupedTracks;
    }


    private static IEnumerable<TracksGroupCategoryViewModel> GroupByAlbum(List<TrackViewModel> tracks)
    {
        IEnumerable<TracksGroupCategoryViewModel> selectedItems = tracks.GroupBy(x => StringExtensions.GetNameFirstLetter(x.Track.AlbumName))
                     .Select(x => new TracksGroupCategoryViewModel { Title = x.Key, Items = x.OrderBy(c => c.Track.AlbumName).ThenBy(c => c.TrackNumber).ToList() });

        return BuildGroupedCollection(selectedItems, orderByDescending: false);
    }


    private static IEnumerable<TracksGroupCategoryViewModel> GroupByArtist(List<TrackViewModel> tracks)
    {
        IEnumerable<TracksGroupCategoryViewModel> selectedItems = tracks.GroupBy(x => StringExtensions.GetNameFirstLetter(x.Track.ArtistName))
                     .Select(x => new TracksGroupCategoryViewModel { Title = x.Key, Items = x.OrderBy(c => c.Track.ArtistName).ThenBy(c => c.AlbumName).ThenBy(c => c.TrackNumber).ToList() });

        return BuildGroupedCollection(selectedItems, orderByDescending: false);
    }


    private static IEnumerable<TracksGroupCategoryViewModel> GroupByGenre(List<TrackViewModel> tracks)
    {
        IEnumerable<TracksGroupCategoryViewModel> selectedItems = tracks.GroupBy(x => string.IsNullOrEmpty(x.Track.GenreName) ? "#123" : x.Track.GenreName)
                     .Select(x => new TracksGroupCategoryViewModel { Title = x.Key, Items = x.OrderBy(c => c.Track.GenreName).ThenBy(c => c.Track.ArtistName).ThenBy(c => c.AlbumName).ThenBy(c => c.TrackNumber).ToList() });

        return BuildGroupedCollection(selectedItems, orderByDescending: false);
    }


    private static IEnumerable<TracksGroupCategoryViewModel> GroupByCountry(List<TrackViewModel> tracks)
    {
        IEnumerable<TracksGroupCategoryViewModel> selectedItems = tracks.GroupBy(x => string.IsNullOrEmpty(x.Track.CountryCode) ? "#123" : x.Track.CountryCode)
                     .Select(x => new TracksGroupCategoryViewModel { Title = x.Key, Items = x.OrderBy(c => c.Track.Title).ToList() });

        return BuildGroupedCollection(selectedItems, orderByDescending: false);
    }


    private static IEnumerable<TracksGroupCategoryViewModel> GroupByLastListen(List<TrackViewModel> tracks)
    {
        DateTime minDate = DateTime.Now.AddDays(-15);

        IEnumerable<TracksGroupCategoryViewModel> selectedItems = tracks.OrderByDescending(c => c.Track.LastListen).GroupBy(x =>
        {
            return x.Track.LastListen > minDate ? x.Track.LastListen.Value.ToString("m") : $"< {minDate:m}";
        }).Select(x => new TracksGroupCategoryViewModel { Title = x.Key, Items = x.OrderByDescending(c => c.Track.LastListen).ToList() });

        return BuildGroupedCollection(selectedItems, orderByDescending: true);
    }


    private static List<TracksGroupCategoryViewModel> GroupByListenCount(List<TrackViewModel> tracks)
    {
        IEnumerable<TracksGroupCategoryViewModel> selectedItems = tracks.GroupBy(x => x.Track.ListenCount.ToString())
                  .Select(x => new TracksGroupCategoryViewModel { Title = x.Key, Items = x.ToList() });

        return BuildGroupedCollection(selectedItems.OrderByDescending(c => Int32.Parse(c.Title)));
    }


    private static List<TracksGroupCategoryViewModel> GroupByCreatDate(List<TrackViewModel> tracks)
    {
        DateTime minDate = DateTime.Now.AddYears(-1);

        IEnumerable<TracksGroupCategoryViewModel> selectedItems = tracks.OrderByDescending(c => c.Track.CreatDate).GroupBy(x => x.Track.CreatDate > minDate ? x.Track.CreatDate.ToString("y") : $"< {minDate:y}")
                    .Select(x => new TracksGroupCategoryViewModel { Title = x.Key, Items = x.OrderByDescending(c => c.Track.CreatDate).ToList() });

        return BuildGroupedCollection(selectedItems);
    }


    private static IEnumerable<TracksGroupCategoryViewModel> GroupByTitle(List<TrackViewModel> tracks)
    {
        IEnumerable<TracksGroupCategoryViewModel> selectedItems = tracks.GroupBy(x => StringExtensions.GetNameFirstLetter(x.Track.Title))
                  .Select(x => new TracksGroupCategoryViewModel { Title = x.Key, Items = x.OrderBy(c => c.Track.Title).ThenBy(c => c.Track.AlbumName).ToList() });

        return BuildGroupedCollection(selectedItems, orderByDescending: false);
    }


    private static List<TracksGroupCategoryViewModel> BuildGroupedCollection(IEnumerable<TracksGroupCategoryViewModel> tracks)
    {
        List<TracksGroupCategoryViewModel> groupedItems = [];

        tracks.ToList().ForEach(c => groupedItems.Add(c));

        return groupedItems;
    }


    private static IEnumerable<TracksGroupCategoryViewModel> BuildGroupedCollection(IEnumerable<TracksGroupCategoryViewModel> tracks, bool orderByDescending)
    {
        if (orderByDescending)
            return tracks.OrderByDescending(c => c.Title);
        else
            return tracks.OrderBy(c => c.Title);
    }
}
