using Rok.Application.Interfaces;

namespace Rok.Application.Services.Filters;

public class TracksFilter(IResourceService resourceLoader) : FilterService<IFilterableTrack>(resourceLoader)
{
    public const string KFilterByArtistFavorite = "ARTISTFAVORITE";
    public const string KFilterByGenreFavorite = "GENREFAVORITE";
    public const string KFilterByAlbumFavorite = "ALBUMFAVORITE";
    public const string KFilterByTrackFavorite = "TRACKFAVORITE";
    public const string KFilterByNeverListened = "NEVERLISTENED";
    public const string KFilterByLive = "LIVE";

    protected override void RegisterFilterStrategies()
    {
        RegisterFilter(KFilterByArtistFavorite, tracks => tracks.Where(t => t.IsArtistFavorite));
        RegisterFilter(KFilterByGenreFavorite, tracks => tracks.Where(t => t.IsGenreFavorite));
        RegisterFilter(KFilterByAlbumFavorite, tracks => tracks.Where(t => t.IsAlbumFavorite));
        RegisterFilter(KFilterByTrackFavorite, tracks => tracks.Where(t => t.Score > 0));
        RegisterFilter(KFilterByNeverListened, tracks => tracks.Where(t => t.ListenCount == 0));
        RegisterFilter(KFilterByLive, tracks => tracks.Where(t => t.IsLive));
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