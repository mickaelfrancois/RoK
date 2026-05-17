using CleanArch.DevKit.Guards;
using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Repositories;
using Rok.Domain.Entities;

namespace Rok.Import;

public class TrackImport(ITrackRepository _trackRepository)
{
    public int CreatedCount { get; private set; } = 0;

    public int CountInCache => _cache.Count;

    private readonly Dictionary<string, TrackEntity> _cache = new(StringComparer.InvariantCultureIgnoreCase);


    public async Task LoadCacheAsync(CancellationToken cancellationToken = default)
    {
        _cache.Clear();

        IEnumerable<TrackEntity> tracks = await _trackRepository.GetAllAsync(RepositoryConnectionKind.Background);

        foreach (TrackEntity track in tracks)
        {
            string key = GetKey(track.MusicFile);

            _cache.TryAdd(key, track);
        }
    }


    public TrackEntity? GetFromCache(string musicFile)
    {
        if (string.IsNullOrEmpty(musicFile))
            return null;

        string key = GetKey(musicFile);

        _cache.TryGetValue(key, out TrackEntity? track);

        return track;
    }


    public async Task<TrackEntity?> CreateAsync(TrackEntity track, CancellationToken cancellationToken = default)
    {
        await _trackRepository.AddAsync(track, RepositoryConnectionKind.Background);

        string key = GetKey(track.MusicFile);

        _cache.Add(key, track);
        CreatedCount++;

        return track;
    }


    public Task UpdateTrackAsync(TrackEntity track)
    {
        return _trackRepository.UpdateAsync(track, RepositoryConnectionKind.Background);
    }


    public Task UpdateTrackFileDateAsync(long trackId, DateTime fileDate)
    {
        Guard.NotNegativeOrZero(trackId);

        return _trackRepository.UpdateFileDateAsync(trackId, fileDate, RepositoryConnectionKind.Background);
    }


    private static string GetKey(string musicFile)
    {
        return musicFile.ToUpperInvariant();
    }
}