using Rok.Application.Interfaces;
using Rok.Shared.Extensions;

namespace Rok.Application.Services.Grouping;

public class TracksGroupCategory(IResourceService resourceLoader)
    : GroupCategoryService<IGroupableTrack, TrackGroupResult>(resourceLoader)
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
        RegisterStrategy(GroupingConstants.None, tracks => GroupByName(tracks, t => t.Title, t => t.Title));

        RegisterStrategy(GroupingConstants.Title, tracks => GroupByName(tracks, t => t.Title, t => t.Title));
        RegisterStrategy(GroupingConstants.Artist, GroupByArtist);
        RegisterStrategy(GroupingConstants.Album, GroupByAlbum);
        RegisterStrategy(GroupingConstants.Genre, GroupByGenre);
        RegisterStrategy(GroupingConstants.Score, GroupByScore);
        RegisterStrategy(GroupingConstants.CreatDate, tracks => GroupByCreatDate(tracks, t => t.CreatDate));
        RegisterStrategy(GroupingConstants.LastListen, tracks => SortByLastListen(tracks, t => t.LastListen));
        RegisterStrategy(GroupingConstants.ListenCount, tracks => SortByListenCount(tracks, t => t.ListenCount));
        RegisterStrategy(GroupingConstants.Country, tracks => GroupByCountry(tracks, t => t.CountryCode, t => t.Title));
    }

    private IEnumerable<TrackGroupResult> GroupByAlbum(List<IGroupableTrack> tracks)
    {
        IEnumerable<TrackGroupResult> selectedItems = tracks
            .GroupBy(x => StringExtensions.GetNameFirstLetter(x.AlbumName))
            .Select(x => new TrackGroupResult
            {
                Title = x.Key,
                Items = x.OrderBy(c => c.AlbumName)
                         .ThenBy(c => c.TrackNumber)
                         .ToList()
            });

        return BuildGroupedCollection(selectedItems, orderByDescending: false);
    }

    private IEnumerable<TrackGroupResult> GroupByArtist(List<IGroupableTrack> tracks)
    {
        IEnumerable<TrackGroupResult> selectedItems = tracks
            .GroupBy(x => StringExtensions.GetNameFirstLetter(x.ArtistName))
            .Select(x => new TrackGroupResult
            {
                Title = x.Key,
                Items = x.OrderBy(c => c.ArtistName)
                         .ThenBy(c => c.AlbumName)
                         .ThenBy(c => c.TrackNumber)
                         .ToList()
            });

        return BuildGroupedCollection(selectedItems, orderByDescending: false);
    }

    private IEnumerable<TrackGroupResult> GroupByGenre(List<IGroupableTrack> tracks)
    {
        IEnumerable<TrackGroupResult> selectedItems = tracks
            .GroupBy(x => string.IsNullOrEmpty(x.GenreName) ? "#123" : x.GenreName)
            .Select(x => new TrackGroupResult
            {
                Title = x.Key,
                Items = x.OrderBy(c => c.GenreName)
                         .ThenBy(c => c.ArtistName)
                         .ThenBy(c => c.AlbumName)
                         .ThenBy(c => c.TrackNumber)
                         .ToList()
            });

        return BuildGroupedCollection(selectedItems, orderByDescending: false);
    }

    private IEnumerable<TrackGroupResult> GroupByScore(List<IGroupableTrack> tracks)
    {
        IEnumerable<TrackGroupResult> selectedItems = tracks
            .GroupBy(x => x.Score.ToString())
            .Select(x => new TrackGroupResult
            {
                Title = x.Key,
                Items = x.OrderByDescending(c => c.Score)
                         .ThenBy(c => c.ArtistName)
                         .ThenBy(c => c.AlbumName)
                         .ThenBy(c => c.TrackNumber)
                         .ToList()
            });

        return BuildGroupedCollection(selectedItems, orderByDescending: true);
    }
}
