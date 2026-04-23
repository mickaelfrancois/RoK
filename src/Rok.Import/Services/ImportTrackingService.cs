namespace Rok.Import.Services;

public class ImportTrackingService
{
    private readonly Dictionary<EntityType, HashSet<long>> _updatedEntities = new();

    public enum EntityType
    {
        Track,
        Artist,
        Genre,
        Album
    }

    public void TrackRead(long trackId)
    {
        MarkAsUpdated(EntityType.Track, trackId);
    }

    public void ArtistUpdated(long? artistId)
    {
        if (artistId.HasValue)
            MarkAsUpdated(EntityType.Artist, artistId.Value);
    }

    public void GenreUpdated(long? genreId)
    {
        if (genreId.HasValue)
            MarkAsUpdated(EntityType.Genre, genreId.Value);
    }

    public void AlbumUpdated(long? albumId)
    {
        if (albumId.HasValue)
            MarkAsUpdated(EntityType.Album, albumId.Value);
    }

    public IEnumerable<long> GetTrackedIds()
    {
        return GetUpdatedEntities(EntityType.Track);
    }

    public IEnumerable<long> GetUpdatedArtists()
    {
        return GetUpdatedEntities(EntityType.Artist);
    }

    public IEnumerable<long> GetUpdatedGenres()
    {
        return GetUpdatedEntities(EntityType.Genre);
    }

    public IEnumerable<long> GetUpdatedAlbums()
    {
        return GetUpdatedEntities(EntityType.Album);
    }

    public void Clear()
    {
        _updatedEntities.Clear();
    }

    private void MarkAsUpdated(EntityType entityType, long entityId)
    {
        if (!_updatedEntities.ContainsKey(entityType))
            _updatedEntities[entityType] = [];

        _updatedEntities[entityType].Add(entityId);
    }

    private IEnumerable<long> GetUpdatedEntities(EntityType entityType)
    {
        return _updatedEntities.TryGetValue(entityType, out HashSet<long>? ids)
            ? ids
            : [];
    }
}