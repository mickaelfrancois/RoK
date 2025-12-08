namespace Rok.Logic.ViewModels.Tracks;

public class TracksGroupCategory(ResourceLoader resourceLoader) : GroupCategoryService<TrackViewModel, TracksGroupCategoryViewModel>(resourceLoader)
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

    public override string GetGroupByLabel(string groupBy)
    {
        return groupBy switch
        {
            KGroupByTitle => ResourceLoader.GetString("tracksViewGroupByTitle"),
            KGroupByCountry => ResourceLoader.GetString("tracksViewGroupByCountry"),
            KGroupByCreatDate => ResourceLoader.GetString("tracksViewGroupByCreatDate"),
            KGroupByLastListen => ResourceLoader.GetString("tracksViewGroupByLastListen"),
            KGroupByListenCount => ResourceLoader.GetString("tracksViewGroupByListenCount"),
            KGroupByArtist => ResourceLoader.GetString("tracksViewGroupByArtist"),
            KGroupByAlbum => ResourceLoader.GetString("tracksViewGroupByAlbum"),
            KGroupByGenre => ResourceLoader.GetString("tracksViewGroupByGenre"),
            _ => groupBy,
        };
    }

    protected override void RegisterGroupingStrategies()
    {
        RegisterStrategy(KGroupByTitle, tracks =>
            GroupByName(tracks, t => t.Track.Title, t => t.Track.Title));

        RegisterStrategy(KGroupByArtist, GroupByArtist);

        RegisterStrategy(KGroupByAlbum, GroupByAlbum);

        RegisterStrategy(KGroupByGenre, GroupByGenre);

        RegisterStrategy(KGroupByCreatDate, tracks =>
            GroupByCreatDate(tracks, t => t.Track.CreatDate));

        RegisterStrategy(KGroupByLastListen, tracks =>
            GroupByLastListen(tracks, t => t.Track.LastListen));

        RegisterStrategy(KGroupByListenCount, tracks =>
            GroupByListenCount(tracks, t => t.Track.ListenCount));

        RegisterStrategy(KGroupByCountry, tracks =>
            GroupByCountry(tracks, t => t.Track.CountryCode, t => t.Track.Title));
    }


    private IEnumerable<TracksGroupCategoryViewModel> GroupByAlbum(List<TrackViewModel> tracks)
    {
        IEnumerable<TracksGroupCategoryViewModel> selectedItems = tracks
            .GroupBy(x => StringExtensions.GetNameFirstLetter(x.Track.AlbumName))
            .Select(x => new TracksGroupCategoryViewModel
            {
                Title = x.Key,
                Items = x.OrderBy(c => c.Track.AlbumName)
                         .ThenBy(c => c.TrackNumber)
                         .ToList()
            });

        return BuildGroupedCollection(selectedItems, orderByDescending: false);
    }

    private IEnumerable<TracksGroupCategoryViewModel> GroupByArtist(List<TrackViewModel> tracks)
    {
        IEnumerable<TracksGroupCategoryViewModel> selectedItems = tracks
            .GroupBy(x => StringExtensions.GetNameFirstLetter(x.Track.ArtistName))
            .Select(x => new TracksGroupCategoryViewModel
            {
                Title = x.Key,
                Items = x.OrderBy(c => c.Track.ArtistName)
                         .ThenBy(c => c.AlbumName)
                         .ThenBy(c => c.TrackNumber)
                         .ToList()
            });

        return BuildGroupedCollection(selectedItems, orderByDescending: false);
    }

    private IEnumerable<TracksGroupCategoryViewModel> GroupByGenre(List<TrackViewModel> tracks)
    {
        IEnumerable<TracksGroupCategoryViewModel> selectedItems = tracks
            .GroupBy(x => string.IsNullOrEmpty(x.Track.GenreName) ? "#123" : x.Track.GenreName)
            .Select(x => new TracksGroupCategoryViewModel
            {
                Title = x.Key,
                Items = x.OrderBy(c => c.Track.GenreName)
                         .ThenBy(c => c.Track.ArtistName)
                         .ThenBy(c => c.AlbumName)
                         .ThenBy(c => c.TrackNumber)
                         .ToList()
            });

        return BuildGroupedCollection(selectedItems, orderByDescending: false);
    }
}