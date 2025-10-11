using MiF.Guard;
using Rok.Application.Interfaces;
using Rok.Domain.Entities;

namespace Rok.Import;

public class TrackImport(ITrackRepository _trackRepository)
{
    public int CreatedCount { get; private set; } = 0;

    private readonly Dictionary<string, TrackEntity> _cache = new(StringComparer.InvariantCultureIgnoreCase);


    public async Task LoadCacheAsync()
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


    public async Task<TrackEntity?> CreateAsync(TrackEntity track)
    {
        await _trackRepository.AddAsync(track, RepositoryConnectionKind.Background);

        string key = GetKey(track.MusicFile);

        _cache.Add(key, track);
        CreatedCount++;

        return track;
    }


    public async Task UpdateTrackAsync(TrackEntity track)
    {
        await _trackRepository.UpdateAsync(track, RepositoryConnectionKind.Background);
    }


    public async Task UpdateTrackFileDateAsync(long trackId, DateTime fileDate)
    {
        Guard.Against.NegativeOrZero(trackId);

        await _trackRepository.UpdateFileDateAsync(trackId, fileDate, RepositoryConnectionKind.Background);
    }


    private static string GetKey(string musicFile)
    {
        return musicFile.ToUpperInvariant();
    }
}
