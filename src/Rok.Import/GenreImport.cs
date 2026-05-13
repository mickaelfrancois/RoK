using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Repositories;
using Rok.Domain.Entities;
using Rok.Import.Models;
using Rok.Shared.Extensions;

namespace Rok.Import;

public class GenreImport(IGenreRepository _genreRepository, TimeProvider _timeProvider)
{
    public int CreatedCount { get; private set; } = 0;

    public int CountInCache => _cache.Count;


    private readonly Dictionary<string, GenreCacheItem> _cache = new(StringComparer.InvariantCultureIgnoreCase);


    public async Task LoadCacheAsync(CancellationToken cancellationToken = default)
    {
        _cache.Clear();

        IEnumerable<GenreEntity> genres = await _genreRepository.GetAllAsync(RepositoryConnectionKind.Background);

        foreach (GenreEntity genre in genres)
        {
            string key = GetKey(genre.Name);

            _cache.TryAdd(key, new GenreCacheItem
            {
                Id = genre.Id,
                Name = genre.Name
            });
        }
    }


    public GenreCacheItem? GetFromCache(string genreName)
    {
        if (string.IsNullOrEmpty(genreName))
            return null;

        string key = GetKey(genreName);

        _cache.TryGetValue(key, out GenreCacheItem? genre);

        return genre;
    }


    public async Task<GenreCacheItem?> CreateAsync(string genreName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(genreName))
            return null;

        GenreEntity genre = new()
        {
            Name = genreName.Capitalize(),
            CreatDate = _timeProvider.GetLocalNow().DateTime,
            ArtistCount = 1
        };

        long id = await _genreRepository.AddAsync(genre, RepositoryConnectionKind.Background);

        string key = GetKey(genre.Name);
        GenreCacheItem cacheItem = new()
        {
            Id = id,
            Name = genre.Name,
        };
        _cache.Add(key, cacheItem);
        CreatedCount++;

        return cacheItem;
    }


    private static string GetKey(string genreName)
    {
        return genreName.ToUpperInvariant();
    }
}
