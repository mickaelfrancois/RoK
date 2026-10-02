using Microsoft.Extensions.Logging;
using Rok.Application.Dto;
using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Repositories;
using Rok.Application.Player.Mix.Tempo;
using Rok.Domain.Entities;
using Rok.Domain.Enums;

namespace Rok.Application.Player.Mix;

/// <summary>
/// Measures the Mix mode cues, tempo and beat grid of tracks. A window is served from the stored
/// analysis when it is valid, otherwise it is decoded once, analysed (stored tempo, then tag, then
/// detection) and written back. The latest results are also kept in a small LRU cache.
/// Failures and cancellations give <c>null</c> and are never cached; a storage failure never hides the result.
/// </summary>
public sealed class MixAnalysisService : IMixCueProvider, IDisposable
{
    internal const int CacheCapacity = 8;

    private readonly IAudioEnvelopeReader _reader;
    private readonly ITrackAnalysisRepository _repository;
    private readonly IPowerStateProvider _power;
    private readonly ILogger<MixAnalysisService> _logger;
    private readonly object _cacheLock = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly Dictionary<CacheKey, LinkedListNode<CacheEntry>> _index = [];
    private readonly LinkedList<CacheEntry> _order = new();

    private static readonly EdgeSpec<OutroCues> OutroSpec = new(
        EAudioEdge.Tail,
        MixCueDetector.DetectOutro,
        (row, beats) => row.MusicEndSeconds is { } end && row.FadeOutSeconds is { } fade ? new OutroCues(end, fade, beats, StoredMixPoint(row)) : null,
        (cues, beats) => cues with { Beats = beats },
        (cues, signal, curve, grid) => cues with { MixPoint = MixPointDetector.Detect(signal.Envelope, curve, signal.StartSeconds, grid, cues.MusicEndSeconds) },
        (row, cues, grid, analysed) =>
        {
            row.MusicEndSeconds = cues.MusicEndSeconds;
            row.FadeOutSeconds = cues.FadeOutSeconds;
            row.OutroBeatPhase = grid?.FirstBeatSeconds;
            row.OutroDownbeatSeconds = grid?.FirstDownbeatSeconds;
            row.OutroMixPointSeconds = cues.MixPoint?.Seconds;
            row.OutroMixPointScore = cues.MixPoint?.Score;
            row.OutroTempoAnalysed = analysed;
        },
        row => row.OutroTempoAnalysed,
        row => row.OutroBeatPhase,
        row => row.OutroDownbeatSeconds);

    private static readonly EdgeSpec<IntroCues> IntroSpec = new(
        EAudioEdge.Head,
        MixCueDetector.DetectIntro,
        (row, beats) => row.MusicStartSeconds is { } start ? new IntroCues(start, beats) : null,
        (cues, beats) => cues with { Beats = beats },
        (cues, _, _, _) => cues,
        (row, cues, grid, analysed) =>
        {
            row.MusicStartSeconds = cues.MusicStartSeconds;
            row.IntroBeatPhase = grid?.FirstBeatSeconds;
            row.IntroDownbeatSeconds = grid?.FirstDownbeatSeconds;
            row.IntroTempoAnalysed = analysed;
        },
        row => row.IntroTempoAnalysed,
        row => row.IntroBeatPhase,
        row => row.IntroDownbeatSeconds);

    /// <summary>Initializes a new instance of the <see cref="MixAnalysisService"/> class.</summary>
    /// <param name="reader">Decoder of track edges.</param>
    /// <param name="repository">Storage of the analysis.</param>
    /// <param name="power">Power state; tempo detection is skipped on battery.</param>
    /// <param name="logger">Logger.</param>
    public MixAnalysisService(IAudioEnvelopeReader reader, ITrackAnalysisRepository repository, IPowerStateProvider power, ILogger<MixAnalysisService> logger)
    {
        _reader = reader;
        _repository = repository;
        _power = power;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<OutroCues?> GetOutroAsync(TrackDto track, CancellationToken ct) => AnalyseAsync(track, OutroSpec, ct);

    /// <inheritdoc />
    public Task<IntroCues?> GetIntroAsync(TrackDto track, CancellationToken ct) => AnalyseAsync(track, IntroSpec, ct);

    /// <inheritdoc />
    public void Dispose() => _writeLock.Dispose();

    private async Task<TCues?> AnalyseAsync<TCues>(TrackDto track, EdgeSpec<TCues> spec, CancellationToken ct)
        where TCues : class
    {
        if (string.IsNullOrWhiteSpace(track.MusicFile))
            return null;

        var key = new CacheKey(track.Id, track.MusicFile, spec.Edge);
        var onBattery = IsOnBattery();
        var withTempo = !onBattery && track.Id > 0;

        if (TryGetCached(key, onBattery, out var cached))
            return cached as TCues;

        try
        {
            var stored = await TryGetStoredAsync(track, ct).ConfigureAwait(false);

            if (stored is not null && IsServable(stored, spec, onBattery))
            {
                var fromRow = spec.FromRow(stored, spec.IsTempoAnalysed(stored) ? BuildGrid(stored, spec.Phase(stored), spec.Downbeat(stored)) : null);

                if (fromRow is not null)
                {
                    Store(key, fromRow, tempoSkipped: !spec.IsTempoAnalysed(stored));

                    return fromRow;
                }
            }

            var signal = await _reader.ReadAsync(track.MusicFile, spec.Edge, TimeSpan.FromSeconds(MixThresholds.AnalysisWindowSeconds), includeMonoSamples: withTempo, ct).ConfigureAwait(false);

            if (signal is null)
                return null;

            ct.ThrowIfCancellationRequested();

            var cues = spec.Detect(signal.Envelope);

            if (cues is null)
            {
                Store(key, null, tempoSkipped: false);

                return null;
            }

            var tempo = withTempo ? DetectTempo(track, stored, signal) : null;
            var result = cues;

            if (tempo?.Grid is { } grid)
            {
                result = spec.WithBeats(cues, grid);

                if (tempo.Curve is { } curve)
                    result = spec.Enrich(result, signal, curve, grid);
            }

            if (track.Id > 0)
                await PersistAsync(track, spec, result, tempo, onBattery, ct).ConfigureAwait(false);

            Store(key, result, tempoSkipped: onBattery && track.Id > 0);

            return result;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Mix: analysis of the {Edge} of {Track} failed", spec.Edge, track.Title);

            return null;
        }
    }

    private bool IsOnBattery()
    {
        try
        {
            return _power.IsOnBattery;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Mix: could not read the power state");

            return false;
        }
    }

    private async Task<TrackAnalysisEntity?> TryGetStoredAsync(TrackDto track, CancellationToken ct)
    {
        if (track.Id <= 0)
            return null;

        try
        {
            var row = await _repository.GetAsync(track.Id, ct).ConfigureAwait(false);

            return row is not null && IsValid(row, track) ? row : null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Mix: could not read the stored analysis of {Track}", track.Title);

            return null;
        }
    }

    private static bool IsValid(TrackAnalysisEntity row, TrackDto track) =>
        row.AlgorithmVersion == TrackAnalysisVersion.Current
        && row.FileModifiedUtc == track.FileDate
        && row.FileSize == track.Size;

    private static bool IsServable<TCues>(TrackAnalysisEntity row, EdgeSpec<TCues> spec, bool onBattery)
        where TCues : class =>
        spec.IsTempoAnalysed(row) || onBattery;

    private static BeatGrid? BuildGrid(TrackAnalysisEntity row, double? phase, double? downbeat) =>
        row.Bpm is { } bpm && phase is { } first
            ? new BeatGrid(bpm, first, row.BpmConfidence ?? 1, row.BpmSource ?? BpmSource.Detected, downbeat)
            : null;

    private static MixPoint? StoredMixPoint(TrackAnalysisEntity row) =>
        row.OutroMixPointSeconds is { } seconds && row.OutroMixPointScore is { } score ? new MixPoint(seconds, score) : null;

    private static TempoOutcome? DetectTempo(TrackDto track, TrackAnalysisEntity? stored, AudioEdgeSignal signal)
    {
        var known = ChooseKnownBpm(track, stored);

        if (signal.MonoSamples is not { Length: > 0 } mono)
            return known is { } only ? new TempoOutcome(only.Bpm, only.Confidence, only.Source, null, null) : null;

        var curve = BeatGridDetector.IsLongEnough(mono.Length, signal.MonoSampleRate) ? OnsetCurve.Compute(mono, signal.MonoSampleRate) : null;
        var detection = curve is null ? TempoDetection.None : BeatGridDetector.Detect(curve, signal.StartSeconds, known?.Bpm);

        var bpm = known?.Bpm ?? detection.Bpm;

        if (bpm is not { } value)
            return null;

        var confidence = known?.Confidence ?? detection.BpmConfidence;
        var source = known?.Source ?? BpmSource.Detected;
        var grid = detection.FirstBeatSeconds is { } first ? new BeatGrid(value, first, confidence, source) : null;

        if (grid is not null && curve is not null)
            grid = grid with { FirstDownbeatSeconds = DownbeatEstimator.Estimate(curve, signal.StartSeconds, value, grid.FirstBeatSeconds) };

        return new TempoOutcome(value, confidence, source, grid, curve);
    }

    private static KnownBpm? ChooseKnownBpm(TrackDto track, TrackAnalysisEntity? stored)
    {
        if (track.Bpm is { } tag && tag >= MixThresholds.MinTagBpm && tag <= MixThresholds.MaxTagBpm)
            return new KnownBpm(tag, 1, BpmSource.Tag);

        if (stored?.Bpm is { } bpm)
            return new KnownBpm(bpm, stored.BpmConfidence ?? 1, stored.BpmSource ?? BpmSource.Detected);

        return null;
    }

    private async Task PersistAsync<TCues>(TrackDto track, EdgeSpec<TCues> spec, TCues cues, TempoOutcome? tempo, bool onBattery, CancellationToken ct)
        where TCues : class
    {
        await _writeLock.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            ct.ThrowIfCancellationRequested();

            var row = await TryGetStoredAsync(track, ct).ConfigureAwait(false) ?? new TrackAnalysisEntity
            {
                TrackId = track.Id,
                AlgorithmVersion = TrackAnalysisVersion.Current,
                FileModifiedUtc = track.FileDate,
                FileSize = track.Size,
            };

            spec.Apply(row, cues, tempo?.Grid, !onBattery);

            if (tempo is not null && (row.Bpm is null || (tempo.Source == BpmSource.Tag && row.BpmSource != BpmSource.Tag)))
            {
                row.Bpm = tempo.Bpm;
                row.BpmConfidence = tempo.Confidence;
                row.BpmSource = tempo.Source;
            }

            await _repository.UpsertAsync(row, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Mix: could not store the analysis of {Track}", track.Title);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private bool TryGetCached(CacheKey key, bool onBattery, out object? cues)
    {
        lock (_cacheLock)
        {
            if (!_index.TryGetValue(key, out var node) || (node.Value.TempoSkipped && !onBattery))
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

    private void Store(CacheKey key, object? cues, bool tempoSkipped)
    {
        lock (_cacheLock)
        {
            if (_index.TryGetValue(key, out var existing))
            {
                if (tempoSkipped && !existing.Value.TempoSkipped && existing.Value.Cues is not null)
                    return;

                _order.Remove(existing);
                _index.Remove(key);
            }

            var node = _order.AddFirst(new CacheEntry(key, cues, tempoSkipped));
            _index[key] = node;

            while (_order.Count > CacheCapacity)
            {
                var last = _order.Last!;
                _order.RemoveLast();
                _index.Remove(last.Value.Key);
            }
        }
    }

    private readonly record struct CacheKey(long TrackId, string MusicFile, EAudioEdge Edge);

    private sealed record CacheEntry(CacheKey Key, object? Cues, bool TempoSkipped);

    private sealed record KnownBpm(double Bpm, double Confidence, BpmSource Source);

    private sealed record TempoOutcome(double Bpm, double Confidence, BpmSource Source, BeatGrid? Grid, OnsetCurve? Curve);

    private sealed record EdgeSpec<TCues>(
        EAudioEdge Edge,
        Func<RmsEnvelope, TCues?> Detect,
        Func<TrackAnalysisEntity, BeatGrid?, TCues?> FromRow,
        Func<TCues, BeatGrid?, TCues> WithBeats,
        Func<TCues, AudioEdgeSignal, OnsetCurve, BeatGrid, TCues> Enrich,
        Action<TrackAnalysisEntity, TCues, BeatGrid?, bool> Apply,
        Func<TrackAnalysisEntity, bool> IsTempoAnalysed,
        Func<TrackAnalysisEntity, double?> Phase,
        Func<TrackAnalysisEntity, double?> Downbeat)
        where TCues : class;
}