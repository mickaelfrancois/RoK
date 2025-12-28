using Rok.Application.Interfaces;
using Rok.Application.Tag;
using Rok.Domain.Entities;
using Rok.Import.Models;
using Rok.Shared.Extensions;

namespace Rok.Import;

public class ArtistImport(IArtistRepository _artistRepository)
{
    public int CreatedCount { get; private set; } = 0;

    public int CountInCache => _cache.Count;

    private readonly Dictionary<string, ArtistCacheItem> _cache = new(StringComparer.InvariantCultureIgnoreCase);


    /// <summary>
    /// Asynchronously loads artist data into the cache.
    /// </summary>
    /// <remarks>This method retrieves all artist entities from the repository and populates the cache with
    /// them. Each artist is stored in the cache using a key derived from the artist's name.</remarks>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task LoadCacheAsync()
    {
        _cache.Clear();

        IEnumerable<ArtistEntity> artists = await _artistRepository.GetAllAsync(RepositoryConnectionKind.Background);

        foreach (ArtistEntity artist in artists)
        {
            string key = GetKey(artist.Name);

            _cache.TryAdd(key, new ArtistCacheItem
            {
                Id = artist.Id,
                Name = artist.Name,
                GenreId = artist.GenreId
            });
        }
    }


    /// <summary>
    /// Retrieves an <see cref="ArtistCacheItem"/> from the cache based on the specified artist name.
    /// </summary>
    /// <param name="artistName">The name of the artist to retrieve from the cache. Cannot be null or empty.</param>
    /// <returns>An <see cref="ArtistCacheItem"/> associated with the specified artist name if found; otherwise, <see
    /// langword="null"/>.</returns>
    public ArtistCacheItem? GetFromCache(string artistName)
    {
        if (string.IsNullOrEmpty(artistName))
            return null;

        string key = GetKey(artistName);

        _cache.TryGetValue(key, out ArtistCacheItem? artist);

        return artist;
    }


    /// <summary>
    /// Asynchronously creates a new artist entry based on the provided track information and optional genre identifier.
    /// </summary>
    /// <remarks>This method creates an artist entry by capitalizing the artist's name and associating it with
    /// the specified genre. It also updates the artist's data from an external API and caches the result. The method
    /// increments the created count for each successful entry.</remarks>
    /// <param name="track">The track file containing artist information. The <see cref="TrackFile.Artist"/> and <see
    /// cref="TrackFile.FullPath"/> properties must not be null or empty.</param>
    /// <param name="genreId">An optional genre identifier to associate with the artist. Can be null if no genre is specified.</param>
    /// <returns>An <see cref="ArtistCacheItem"/> representing the newly created artist entry, or <see langword="null"/> if the
    /// artist information is incomplete.</returns>
    public async Task<ArtistCacheItem?> CreateAsync(TrackFile track, long? genreId)
    {
        if (string.IsNullOrEmpty(track.Artist))
            return null;
        if (string.IsNullOrEmpty(track.FullPath))
            return null;

        ArtistEntity artist = new()
        {
            Name = track.Artist.Capitalize(),
            GenreId = genreId,
            MusicBrainzID = track.MusicbrainzAlbumID,
            CreatDate = DateTime.Now,
            AlbumCount = track.IsCompilation ? 0 : 1,
            CompilationCount = track.IsCompilation ? 1 : 0,
        };

        long id = await _artistRepository.AddAsync(artist, RepositoryConnectionKind.Background);

        string key = GetKey(artist.Name);
        ArtistCacheItem cacheItem = new()
        {
            Id = id,
            Name = artist.Name,
            GenreId = genreId,
        };
        _cache.Add(key, cacheItem);
        CreatedCount++;

        return cacheItem;
    }


    private static string GetKey(string artistName)
    {
        return artistName.ToUpperInvariant();
    }
}
