namespace Rok.Import.Services;

public class ImportTrackingService
{
    private readonly List<long> _trackIdsRead = new();
    private readonly List<long> _artistsUpdated = new();
    private readonly List<long> _genresUpdated = new();
    private readonly List<long> _albumsUpdated = new();

    public void TrackRead(long trackId)
    {
        _trackIdsRead.Add(trackId);
    }

    public void ArtistUpdated(long? artistId)
    {
        if (artistId.HasValue)
            _artistsUpdated.Add(artistId.Value);
    }

    public void GenreUpdated(long? genreId)
    {
        if (genreId.HasValue)
            _genresUpdated.Add(genreId.Value);
    }

    public void AlbumUpdated(long? albumId)
    {
        if (albumId.HasValue)
            _albumsUpdated.Add(albumId.Value);
    }

    public IEnumerable<long> GetTrackedIds()
    {
        return _trackIdsRead.ToList();
    }

    public IEnumerable<long> GetUpdatedArtists()
    {
        return _artistsUpdated.Distinct();
    }

    public IEnumerable<long> GetUpdatedGenres()
    {
        return _genresUpdated.Distinct();
    }

    public IEnumerable<long> GetUpdatedAlbums()
    {
        return _albumsUpdated.Distinct();
    }

    public void Clear()
    {
        _trackIdsRead.Clear();
        _artistsUpdated.Clear();
        _genresUpdated.Clear();
        _albumsUpdated.Clear();
    }
}