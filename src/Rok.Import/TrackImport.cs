using CleanArch.DevKit.Guards;
using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Repositories;
using Rok.Domain.Entities;
using Rok.Domain.Enums;

namespace Rok.Import;

public class TrackImport(ITrackRepository _trackRepository)
{
    public int CreatedCount { get; private set; } = 0;

    public int CountInCache => _cache.Count;

    private readonly Dictionary<string, TrackEntity> _cache = new(StringComparer.InvariantCultureIgnoreCase);

    private readonly Dictionary<long, ETagWriteFields> _pendingFields = [];

    public async Task LoadCacheAsync(CancellationToken cancellationToken = default)
    {
        _cache.Clear();

        IEnumerable<TrackEntity> tracks = await _trackRepository.GetAllAsync(RepositoryConnectionKind.Background);

        foreach (TrackEntity track in tracks)
        {
            string key = GetKey(track.MusicFile);

            _cache.TryAdd(key, track);
        }

        await LoadPendingFieldsAsync();
    }


    /// <summary>
    /// Gets the tag fields of a track whose file has not been rewritten yet after an album edit or merge.
    /// </summary>
    /// <param name="trackId">The track identifier.</param>
    /// <returns>The pending fields, or <see cref="ETagWriteFields.None"/> when the file is up to date.</returns>
    public ETagWriteFields GetPendingFields(long trackId)
    {
        return _pendingFields.GetValueOrDefault(trackId, ETagWriteFields.None);
    }


    public TrackEntity? GetFromCache(string musicFile)
    {
        if (string.IsNullOrEmpty(musicFile))
            return null;

        string key = GetKey(musicFile);

        _cache.TryGetValue(key, out TrackEntity? track);

        return track;
    }


    public IEnumerable<TrackEntity> GetCachedTracksOfAlbum(long albumId)
    {
        return _cache.Values.Where(track => track.AlbumId == albumId);
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


    private async Task LoadPendingFieldsAsync()
    {
        _pendingFields.Clear();

        IEnumerable<PendingTagWrite>? pending = await _trackRepository.GetPendingTagWritesAsync(RepositoryConnectionKind.Background);

        if (pending is null)
            return;

        foreach (PendingTagWrite write in pending)
        {
            _pendingFields[write.TrackId] = write.Fields;
        }
    }


    private static string GetKey(string musicFile)
    {
        return musicFile.ToUpperInvariant();
    }
}