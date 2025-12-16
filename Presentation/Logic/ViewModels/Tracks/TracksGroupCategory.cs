namespace Rok.Logic.ViewModels.Tracks;

public class TracksGroupCategory(ResourceLoader resourceLoader) : GroupCategoryService<TrackViewModel, TracksGroupCategoryViewModel>(resourceLoader)
{
    public override string GetGroupByLabel(string groupBy)
    {
        return groupBy switch
        {
            GroupingConstants.Title => ResourceLoader.GetString("tracksViewGroupByTitle"),
            GroupingConstants.Country => ResourceLoader.GetString("tracksViewGroupByCountry"),
            GroupingConstants.CreatDate => ResourceLoader.GetString("tracksViewGroupByCreatDate"),
            GroupingConstants.LastListen => ResourceLoader.GetString("tracksViewGroupByLastListen"),
            GroupingConstants.ListenCount => ResourceLoader.GetString("tracksViewGroupByListenCount"),
            GroupingConstants.Artist => ResourceLoader.GetString("tracksViewGroupByArtist"),
            GroupingConstants.Album => ResourceLoader.GetString("tracksViewGroupByAlbum"),
            GroupingConstants.Genre => ResourceLoader.GetString("tracksViewGroupByGenre"),
            GroupingConstants.Score => ResourceLoader.GetString("tracksViewGroupByScore"),
            _ => groupBy,
        };
    }

    protected override void RegisterGroupingStrategies()
    {
        RegisterStrategy(GroupingConstants.None, tracks => GroupByName(tracks, t => t.Track.Title, t => t.Track.Title));

        RegisterStrategy(GroupingConstants.Title, tracks => GroupByName(tracks, t => t.Track.Title, t => t.Track.Title));
        RegisterStrategy(GroupingConstants.Artist, GroupByArtist);
        RegisterStrategy(GroupingConstants.Album, GroupByAlbum);
        RegisterStrategy(GroupingConstants.Genre, GroupByGenre);
        RegisterStrategy(GroupingConstants.Score, GroupByScore);
        RegisterStrategy(GroupingConstants.CreatDate, tracks => GroupByCreatDate(tracks, t => t.Track.CreatDate));
        RegisterStrategy(GroupingConstants.LastListen, tracks => SortByLastListen(tracks, t => t.Track.LastListen));
        RegisterStrategy(GroupingConstants.ListenCount, tracks => SortByListenCount(tracks, t => t.Track.ListenCount));
        RegisterStrategy(GroupingConstants.Country, tracks => GroupByCountry(tracks, t => t.Track.CountryCode, t => t.Track.Title));
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

    private IEnumerable<TracksGroupCategoryViewModel> GroupByScore(List<TrackViewModel> tracks)
    {
        IEnumerable<TracksGroupCategoryViewModel> selectedItems = tracks
            .GroupBy(x => x.Track.Score.ToString())
            .Select(x => new TracksGroupCategoryViewModel
            {
                Title = x.Key,
                Items = x.OrderByDescending(c => c.Track.Score)
                         .ThenBy(c => c.Track.ArtistName)
                         .ThenBy(c => c.AlbumName)
                         .ThenBy(c => c.TrackNumber)
                         .ToList()
            });

        return BuildGroupedCollection(selectedItems, orderByDescending: true);
    }
}