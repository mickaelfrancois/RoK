using Rok.Application.Interfaces;
using Rok.Application.Tag;
using Rok.Domain.Entities;
using Rok.Import.Models;
using Rok.Shared.Extensions;

namespace Rok.Import;

public class AlbumImport(IAlbumRepository _albumRepository)
{
    public int CreatedCount { get; private set; } = 0;

    public int CountInCache => _cache.Count;

    private readonly Dictionary<string, AlbumCacheItem> _cache = new(StringComparer.InvariantCultureIgnoreCase);


    /// <summary>
    /// Asynchronously loads album data into the cache.
    /// </summary>
    /// <remarks>This method clears the existing cache and repopulates it with album data retrieved from the
    /// repository. Each album is stored in the cache with a unique key generated from its name, compilation status, and
    /// artist ID.</remarks>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task LoadCacheAsync()
    {
        _cache.Clear();

        IEnumerable<AlbumEntity> albums = await _albumRepository.GetAllAsync(RepositoryConnectionKind.Background);

        foreach (AlbumEntity album in albums)
        {
            string key = GetKey(album.Name, album.IsCompilation, album.ArtistId);

            _cache.TryAdd(key, new AlbumCacheItem
            {
                Id = album.Id,
                Name = album.Name,
                ArtistId = album.ArtistId,
                IsCompilation = album.IsCompilation,
                AlbumPath = album.AlbumPath
            });
        }
    }


    /// <summary>
    /// Retrieves an album from the cache based on the specified parameters.
    /// </summary>
    /// <param name="albumName">The name of the album to retrieve. Cannot be null or empty.</param>
    /// <param name="isCompilation">A value indicating whether the album is a compilation.</param>
    /// <param name="artistId">The identifier of the artist associated with the album, or <see langword="null"/> if not applicable.</param>
    /// <returns>An <see cref="AlbumCacheItem"/> representing the cached album if found; otherwise, <see langword="null"/>.</returns>
    public AlbumCacheItem? GetFromCache(string albumName, bool isCompilation, long? artistId)
    {
        if (string.IsNullOrEmpty(albumName))
            return null;

        string key = GetKey(albumName, isCompilation, artistId);

        _cache.TryGetValue(key, out AlbumCacheItem? album);

        return album;
    }


    /// <summary>
    /// Asynchronously creates a new album entry based on the provided track information and optional artist and genre
    /// identifiers.
    /// </summary>
    /// <remarks>This method creates an album entry by capitalizing the album name and setting additional
    /// properties from the provided track. It also completes the album data using an external API and caches the result
    /// for future retrieval.</remarks>
    /// <param name="track">The track file containing album details. The <see cref="TrackFile.Album"/> and <see cref="TrackFile.FullPath"/>
    /// properties must not be null or empty.</param>
    /// <param name="artistId">The optional identifier of the artist associated with the album. Can be null if the artist is unknown or not
    /// applicable.</param>
    /// <param name="genreId">The optional identifier of the genre associated with the album. Can be null if the genre is unknown or not
    /// applicable.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains an <see cref="AlbumCacheItem"/>
    /// representing the created album, or <see langword="null"/> if the album name or path is not provided.</returns>
    public async Task<AlbumCacheItem?> CreateAsync(TrackFile track, long? artistId, long? genreId)
    {
        if (string.IsNullOrEmpty(track.Album))
            return null;
        if (string.IsNullOrEmpty(track.FullPath))
            return null;

        AlbumEntity album = new()
        {
            Name = track.Album.Capitalize(),
            ArtistId = artistId,
            GenreId = genreId,
            Year = track.Year,
            IsCompilation = track.IsCompilation,
            AlbumPath = Path.GetDirectoryName(track.FullPath)!,
            MusicBrainzID = track.MusicbrainzAlbumID,
            CreatDate = DateTime.Now
        };

        long id = await _albumRepository.AddAsync(album, RepositoryConnectionKind.Background);

        string key = GetKey(album.Name, album.IsCompilation, artistId);
        AlbumCacheItem cacheItem = new()
        {
            Id = id,
            Name = album.Name,
            ArtistId = artistId,
            IsCompilation = album.IsCompilation,
            AlbumPath = album.AlbumPath
        };
        _cache.Add(key, cacheItem);
        CreatedCount++;

        return cacheItem;
    }

    private static string GetKey(string albumName, bool isCompilation, long? artistId)
    {
        if (isCompilation || !artistId.HasValue)
            return albumName;
        else
            return $"{artistId}___{albumName}";
    }
}
