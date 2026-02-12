using Rok.ViewModels.Track;

namespace Rok.ViewModels.Tracks;

public class TracksFilter : FilterService<TrackViewModel>
{
    public const string KFilterByArtistFavorite = "ARTISTFAVORITE";
    public const string KFilterByGenreFavorite = "GENREFAVORITE";
    public const string KFilterByAlbumFavorite = "ALBUMFAVORITE";
    public const string KFilterByTrackFavorite = "TRACKFAVORITE";
    public const string KFilterByNeverListened = "NEVERLISTENED";
    public const string KFilterByLive = "LIVE";

    public TracksFilter(ResourceLoader resourceLoader) : base(resourceLoader)
    {
    }

    public IEnumerable<TrackViewModel> FilterByGenreId(long genreId, IEnumerable<TrackViewModel> tracks)
    {
        return FilterByGenreId(genreId, tracks, t => t.Track.GenreId);
    }

    protected override void RegisterFilterStrategies()
    {
        RegisterFilter(KFilterByArtistFavorite,
            tracks => FilterByFavorite(tracks, t => t.Track.IsArtistFavorite));

        RegisterFilter(KFilterByGenreFavorite,
            tracks => FilterByFavorite(tracks, t => t.Track.IsGenreFavorite));

        RegisterFilter(KFilterByAlbumFavorite,
            tracks => FilterByFavorite(tracks, t => t.Track.IsAlbumFavorite));

        RegisterFilter(KFilterByTrackFavorite,
            tracks => FilterByCondition(tracks, t => t.Track.Score > 0));

        RegisterFilter(KFilterByNeverListened,
            tracks => FilterByNeverListened(tracks, t => t.Track.ListenCount));

        RegisterFilter(KFilterByLive,
            tracks => FilterByCondition(tracks, t => t.Track.IsLive));
    }

    public override string GetLabel(string filterBy)
    {
        return filterBy switch
        {
            KFilterByArtistFavorite => ResourceLoader.GetString("tracksViewFilterByFavoriteArtist"),
            KFilterByGenreFavorite => ResourceLoader.GetString("tracksViewFilterByFavoriteGenre"),
            KFilterByAlbumFavorite => ResourceLoader.GetString("tracksViewFilterByFavoriteAlbum"),
            KFilterByTrackFavorite => ResourceLoader.GetString("tracksViewFilterByFavoriteTrack"),
            KFilterByNeverListened => ResourceLoader.GetString("tracksViewFilterByNeverListened"),
            KFilterByLive => ResourceLoader.GetString("tracksViewFilterByLive"),
            _ => ResourceLoader.GetString("tracksViewFilterNone"),
        };
    }
}