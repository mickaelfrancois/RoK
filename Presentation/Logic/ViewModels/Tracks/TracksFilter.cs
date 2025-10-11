namespace Rok.Logic.ViewModels.Tracks;

public class TracksFilter(ResourceLoader _resourceLoader)
{
    public static IEnumerable<TrackViewModel> FilterByGenreId(long genreId, IEnumerable<TrackViewModel> tracks)
    {
        if (genreId == 0)
            return tracks;

        tracks = tracks.Where(track => track.Track.GenreId == genreId);

        return tracks;
    }


    public static IEnumerable<TrackViewModel> Filter(string filterBy, IEnumerable<TrackViewModel> tracks)
    {
        switch (filterBy)
        {
            case "ARTISTFAVORITE":
                tracks = tracks.Where(track => track.Track.IsArtistFavorite);
                break;

            case "GENREFAVORITE":
                tracks = tracks.Where(track => track.Track.IsGenreFavorite);
                break;

            case "ALBUMFAVORITE":
                tracks = tracks.Where(track => track.Track.IsAlbumFavorite);
                break;

            case "TRACKFAVORITE":
                tracks = tracks.Where(track => track.Track.Score > 0);
                break;

            case "NEVERLISTENED":
                tracks = tracks.Where(track => track.Track.ListenCount == 0);
                break;

            case "LIVE":
                tracks = tracks.Where(track => track.Track.IsLive);
                break;
        }

        return tracks;
    }

    public string GetLabel(string filterBy)
    {
        string label = filterBy switch
        {
            "ARTISTFAVORITE" => _resourceLoader.GetString("tracksViewFilterByFavoriteArtist"),
            "GENREFAVORITE" => _resourceLoader.GetString("tracksViewFilterByFavoriteGenre"),
            "ALBUMFAVORITE" => _resourceLoader.GetString("tracksViewFilterByFavoriteAlbum"),
            "TRACKFAVORITE" => _resourceLoader.GetString("tracksViewFilterByFavoriteTrack"),
            "NEVERLISTENED" => _resourceLoader.GetString("tracksViewFilterByNeverListened"),
            "LIVE" => _resourceLoader.GetString("tracksViewFilterByLive"),
            _ => _resourceLoader.GetString("tracksViewFilterNone"),
        };

        return label;
    }
}
