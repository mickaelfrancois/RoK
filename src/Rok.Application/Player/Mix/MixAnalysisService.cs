using Microsoft.Extensions.Logging;
using Rok.Application.Dto;
using Rok.Application.Interfaces;

namespace Rok.Application.Player.Mix;

/// <summary>
/// Measures the Mix mode cues of tracks and keeps the latest results in a small LRU cache.
/// Failures and cancellations give <c>null</c> and are never cached.
/// </summary>
public sealed class MixAnalysisService : IMixCueProvider
{
    internal const int CacheCapacity = 8;

    private readonly IAudioEnvelopeReader _reader;
    private readonly ILogger<MixAnalysisService> _logger;
    private readonly object _cacheLock = new();
    private readonly Dictionary<(long TrackId, EAudioEdge Edge), LinkedListNode<CacheEntry>> _index = [];
    private readonly LinkedList<CacheEntry> _order = new();

    public MixAnalysisService(IAudioEnvelopeReader reader, ILogger<MixAnalysisService> logger)
    {
        _reader = reader;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<OutroCues?> GetOutroAsync(TrackDto track, CancellationToken ct)
    {
        var result = await AnalyseAsync(track, EAudioEdge.Tail, MixCueDetector.DetectOutro, ct).ConfigureAwait(false);

        return result as OutroCues;
    }

    /// <inheritdoc />
    public async Task<IntroCues?> GetIntroAsync(TrackDto track, CancellationToken ct)
    {
        var result = await AnalyseAsync(track, EAudioEdge.Head, MixCueDetector.DetectIntro, ct).ConfigureAwait(false);

        return result as IntroCues;
    }

    private async Task<object?> AnalyseAsync(TrackDto track, EAudioEdge edge, Func<RmsEnvelope, object?> detect, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(track.MusicFile))
            return null;

        var key = (track.Id, edge);

        if (TryGetCached(key, out var cached))
            return cached;

        try
        {
            var envelope = await _reader.ReadAsync(track.MusicFile, edge, TimeSpan.FromSeconds(MixThresholds.AnalysisWindowSeconds), ct).ConfigureAwait(false);

            if (envelope is null)
                return null;

            var cues = detect(envelope);
            Store(key, cues);

            return cues;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Mix: analysis of the {Edge} of {Track} failed", edge, track.Title);

            return null;
        }
    }

    private bool TryGetCached((long TrackId, EAudioEdge Edge) key, out object? cues)
    {
        lock (_cacheLock)
        {
            if (!_index.TryGetValue(key, out var node))
            {
                cues = null;

                return false;
            }

            _order.Remove(node);
            _order.AddFirst(node);
            cues = node.Value.Cues;

            return true;
        }
    }

    private void Store((long TrackId, EAudioEdge Edge) key, object? cues)
    {
        lock (_cacheLock)
        {
            if (_index.TryGetValue(key, out var existing))
            {
                _order.Remove(existing);
                _index.Remove(key);
            }

            var node = _order.AddFirst(new CacheEntry(key, cues));
            _index[key] = node;

            while (_order.Count > CacheCapacity)
            {
                var last = _order.Last!;
                _order.RemoveLast();
                _index.Remove(last.Value.Key);
            }
        }
    }

    private sealed record CacheEntry((long TrackId, EAudioEdge Edge) Key, object? Cues);
}