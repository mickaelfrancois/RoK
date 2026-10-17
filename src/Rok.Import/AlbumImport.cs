using System.Text.RegularExpressions;
using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Repositories;
using Rok.Application.Tag;
using Rok.Domain.Entities;
using Rok.Domain.Enums;
using Rok.Import.Models;
using Rok.Import.Services;
using Rok.Shared.Extensions;

namespace Rok.Import;

public class AlbumImport(IAlbumRepository _albumRepository, TimeProvider _timeProvider)
{
    public int CreatedCount { get; private set; } = 0;

    public int CountInCache => _cache.Count;

    private readonly Dictionary<string, AlbumCacheItem> _cache = new(StringComparer.Ordinal);

    private readonly Dictionary<long, AlbumCacheItem> _byId = new();

    private static readonly Regex _liveRegex = new(@"\b(live|concert|performance)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private const int MaxAlbumsApiEnrichment = 100;

    private readonly List<long> _newlyCreatedIds = new();

    public IReadOnlyList<long> NewlyCreatedIds => _newlyCreatedIds;


    /// <summary>
    /// Asynchronously loads album data into the cache.
    /// </summary>
    /// <remarks>This method clears the existing cache and repopulates it with album data retrieved from the
    /// repository. Each album is stored in the cache with a unique key generated from its name, compilation status, and
    /// artist ID.</remarks>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task LoadCacheAsync(CancellationToken cancellationToken = default)
    {
        _cache.Clear();
        _byId.Clear();
        _newlyCreatedIds.Clear();

        IEnumerable<AlbumEntity> albums = await _albumRepository.GetAllAsync(RepositoryConnectionKind.Background);

        foreach (AlbumEntity album in albums)
        {
            string key = GetKey(album.Name, album.IsCompilation, album.ArtistId, album.AlbumPath);

            AlbumCacheItem item = new()
            {
                Id = album.Id,
                Name = album.Name,
                ArtistId = album.ArtistId,
                GenreId = album.GenreId,
                IsCompilation = album.IsCompilation,
                CompilationSource = album.CompilationSource,
                AlbumPath = album.AlbumPath
            };

            _cache.TryAdd(key, item);
            _byId.TryAdd(album.Id, item);
        }
    }


    /// <summary>
    /// Retrieves an album from the cache by its identifier.
    /// </summary>
    /// <param name="albumId">The album identifier.</param>
    /// <returns>The cached album, or <see langword="null"/> when the identifier is unknown.</returns>
    public AlbumCacheItem? GetById(long albumId)
    {
        _byId.TryGetValue(albumId, out AlbumCacheItem? album);

        return album;
    }


    /// <summary>
    /// Updates the compilation flag and the artist of a cached album, then re-indexes it under its new key.
    /// </summary>
    /// <remarks>The previous key is removed only when it points to this album. The new key never replaces another album.</remarks>
    /// <param name="album">The cached album to update.</param>
    /// <param name="isCompilation">The new compilation flag.</param>
    /// <param name="artistId">The new artist identifier, or <see langword="null"/> when not applicable.</param>
    /// <param name="source">The origin of the new compilation status.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task UpdateCompilationAsync(AlbumCacheItem album, bool isCompilation, long? artistId, ECompilationSource source)
    {
        await _albumRepository.UpdateCompilationAsync(album.Id, isCompilation, artistId, source, RepositoryConnectionKind.Background);

        string oldKey = GetKey(album.Name, album.IsCompilation, album.ArtistId, album.AlbumPath);

        if (_cache.TryGetValue(oldKey, out AlbumCacheItem? indexed) && indexed.Id == album.Id)
            _cache.Remove(oldKey);

        album.IsCompilation = isCompilation;
        album.CompilationSource = source;
        album.ArtistId = artistId;

        _cache.TryAdd(GetKey(album.Name, album.IsCompilation, album.ArtistId, album.AlbumPath), album);
    }


    /// <summary>
    /// Retrieves an album from the cache based on the specified parameters.
    /// </summary>
    /// <param name="albumName">The name of the album to retrieve. Cannot be null or empty.</param>
    /// <param name="isCompilation">A value indicating whether the album is a compilation.</param>
    /// <param name="artistId">The identifier of the artist associated with the album, or <see langword="null"/> if not applicable.</param>
    /// <param name="albumFolder">The folder of the track. A compilation is identified by its name and this folder (a disc subfolder counts as its parent).</param>
    /// <returns>An <see cref="AlbumCacheItem"/> representing the cached album if found; otherwise, <see langword="null"/>.</returns>
    public AlbumCacheItem? GetFromCache(string albumName, bool isCompilation, long? artistId, string? albumFolder = null)
    {
        if (string.IsNullOrEmpty(albumName))
            return null;

        string key = GetKey(albumName, isCompilation, artistId, albumFolder);

        _cache.TryGetValue(key, out AlbumCacheItem? album);

        return album;
    }


    /// <summary>
    /// Asynchronously creates a new album entry based on the provided track information and optional artist and genre
    /// identifiers.
    /// </summary>
    /// <remarks>This method creates an album entry with the album name as tagged and setting additional
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
    public async Task<AlbumCacheItem?> CreateAsync(TrackFile track, long? artistId, long? genreId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(track.Album))
            return null;
        if (string.IsNullOrEmpty(track.FullPath))
            return null;

        string[] bestOfMask = new[] { "best of", "best hits", "greatest hits", "bestof" };
        bool isBestOf = bestOfMask.Any(mask => track.Album.Contains(mask, StringComparison.InvariantCultureIgnoreCase));

        bool isLive = _liveRegex.IsMatch(track.Album);

        AlbumEntity album = new()
        {
            Name = track.Album,
            ArtistId = artistId,
            GenreId = genreId,
            Year = track.Year,
            IsCompilation = track.IsCompilation,
            CompilationSource = track.IsTaggedCompilation ? ECompilationSource.Tag : ECompilationSource.Derived,
            IsBestOf = isBestOf,
            IsLive = isLive,
            AlbumPath = Path.GetDirectoryName(track.FullPath)!,
            CreatDate = _timeProvider.GetLocalNow().DateTime
        };

        TagMetadataMapper.ApplyAlbumMetadata(album, track, MetadataWritePolicy.Mirror);

        long id = await _albumRepository.AddAsync(album, RepositoryConnectionKind.Background);

        string key = GetKey(album.Name, album.IsCompilation, artistId, album.AlbumPath);
        AlbumCacheItem cacheItem = new()
        {
            Id = id,
            Name = album.Name,
            ArtistId = artistId,
            GenreId = genreId,
            IsCompilation = album.IsCompilation,
            CompilationSource = album.CompilationSource,
            AlbumPath = album.AlbumPath
        };
        _cache.Add(key, cacheItem);
        _byId[id] = cacheItem;
        CreatedCount++;

        if (_newlyCreatedIds.Count < MaxAlbumsApiEnrichment)
            _newlyCreatedIds.Add(id);

        return cacheItem;
    }

    private static string GetKey(string albumName, bool isCompilation, long? artistId, string? albumFolder)
    {
        string normalized = albumName.ToIndexKey();

        if (isCompilation)
            return $"{normalized}@@{AlbumFolderKey.Of(albumFolder).ToUpperInvariant()}";

        if (!artistId.HasValue)
            return normalized;

        return $"{artistId}___{normalized}";
    }
}