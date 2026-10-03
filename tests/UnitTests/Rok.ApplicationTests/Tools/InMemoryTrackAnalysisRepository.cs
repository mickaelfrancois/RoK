using System.Collections.Concurrent;
using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Repositories;
using Rok.Application.Player.Mix;
using Rok.Domain.Entities;

namespace Rok.ApplicationTests.Tools;

internal sealed class InMemoryTrackAnalysisRepository : ITrackAnalysisRepository
{
    private readonly ConcurrentDictionary<long, TrackAnalysisEntity> _rows = new();

    public bool FailOnUpsert { get; set; }

    public Action<int>? AfterUpsert { get; set; }

    public int Upserts => _upserts;

    private int _upserts;

    public TrackAnalysisEntity? Find(long trackId) => _rows.GetValueOrDefault(trackId);

    public Task<TrackAnalysisEntity?> GetAsync(long trackId, CancellationToken ct) => Task.FromResult(_rows.GetValueOrDefault(trackId));

    public Task UpsertAsync(TrackAnalysisEntity entity, CancellationToken ct)
    {
        if (FailOnUpsert)
            throw new IOException("disk full");

        var count = Interlocked.Increment(ref _upserts);
        _rows[entity.TrackId] = entity;
        AfterUpsert?.Invoke(count);

        return Task.CompletedTask;
    }
}

internal sealed class ScriptedEnvelopeReader : IAudioEnvelopeReader
{
    public const int MonoRate = 11025;

    private int _reads;

    public int Reads => Volatile.Read(ref _reads);

    public Func<string, EAudioEdge, Task>? Before { get; init; }

    public Func<string, bool>? Unreadable { get; init; }

    public RmsEnvelope Head { get; init; } = SyntheticSignalEdges.Head();

    public RmsEnvelope Tail { get; init; } = SyntheticSignalEdges.Tail();

    public float[]? Mono { get; init; } = SyntheticSignalEdges.Mono120();

    public async Task<AudioEdgeSignal?> ReadAsync(string path, EAudioEdge edge, TimeSpan span, bool includeMonoSamples, CancellationToken ct)
    {
        Interlocked.Increment(ref _reads);

        if (Before is not null)
            await Before(path, edge).ConfigureAwait(false);

        if (Unreadable?.Invoke(path) == true)
            return null;

        var envelope = edge == EAudioEdge.Head ? Head : Tail;

        return new AudioEdgeSignal(envelope, includeMonoSamples ? Mono : null, includeMonoSamples && Mono is not null ? MonoRate : 0, envelope.StartSeconds);
    }
}

internal static class SyntheticSignalEdges
{
    public static RmsEnvelope Head() => Player.Mix.SyntheticSignal.Envelope(Player.Mix.SyntheticSignal.Concat(Player.Mix.SyntheticSignal.Silence(3), Player.Mix.SyntheticSignal.Sine(27)), 0, 200);

    public static RmsEnvelope Tail() => Player.Mix.SyntheticSignal.Envelope(Player.Mix.SyntheticSignal.Concat(Player.Mix.SyntheticSignal.Sine(25), Player.Mix.SyntheticSignal.Silence(5)), 170, 200);

    public static RmsEnvelope Silent() => Player.Mix.SyntheticSignal.Envelope(Player.Mix.SyntheticSignal.Silence(30), 0, 200);

    public static float[] Mono120() => Player.Mix.SyntheticSignal.Clicks(120, 20, ScriptedEnvelopeReader.MonoRate, 0.137);
}