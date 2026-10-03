using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Repositories;
using Rok.Application.Player.Mix;
using Rok.Domain.Entities;

namespace Rok.MetadataTool;

/// <summary>Power state of a scan: always on mains, so the tempo is always analysed.</summary>
internal sealed class MainsPowerStateProvider : IPowerStateProvider
{
    public bool IsOnBattery => false;
}

/// <summary>Keeps the written rows in memory and never writes to the inner repository.</summary>
internal sealed class DryRunTrackAnalysisRepository(ITrackAnalysisRepository inner) : ITrackAnalysisRepository
{
    private readonly ConcurrentDictionary<long, TrackAnalysisEntity> _rows = new();

    public Task<TrackAnalysisEntity?> GetAsync(long trackId, CancellationToken ct) =>
        _rows.TryGetValue(trackId, out var row) ? Task.FromResult<TrackAnalysisEntity?>(row) : inner.GetAsync(trackId, ct);

    public Task UpsertAsync(TrackAnalysisEntity entity, CancellationToken ct)
    {
        _rows[entity.TrackId] = entity;

        return Task.CompletedTask;
    }
}

/// <summary>Counts the decodes asked of the inner reader, and those that gave nothing.</summary>
internal sealed class CountingEnvelopeReader(IAudioEnvelopeReader inner) : IAudioEnvelopeReader, IDisposable
{
    private int _reads;
    private int _nullReads;

    public int Reads => Volatile.Read(ref _reads);

    public int NullReads => Volatile.Read(ref _nullReads);

    public void Reset()
    {
        Volatile.Write(ref _reads, 0);
        Volatile.Write(ref _nullReads, 0);
    }

    public async Task<AudioEdgeSignal?> ReadAsync(string path, EAudioEdge edge, TimeSpan span, bool includeMonoSamples, CancellationToken ct)
    {
        Interlocked.Increment(ref _reads);

        var signal = await inner.ReadAsync(path, edge, span, includeMonoSamples, ct).ConfigureAwait(false);

        if (signal is null)
            Interlocked.Increment(ref _nullReads);

        return signal;
    }

    public void Dispose() => (inner as IDisposable)?.Dispose();
}

/// <summary>Collects the warnings logged while a track is analysed.</summary>
internal sealed class WarningCollector
{
    private readonly ConcurrentQueue<string> _warnings = new();

    public void Add(string warning) => _warnings.Enqueue(warning);

    public IReadOnlyList<string> Drain()
    {
        var drained = new List<string>();

        while (_warnings.TryDequeue(out var warning))
            drained.Add(warning);

        return drained;
    }
}

/// <summary>Logger that keeps warnings and above in a <see cref="WarningCollector"/>.</summary>
internal sealed class CollectingLogger<T>(WarningCollector collector) : ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var message = formatter(state, exception);

        collector.Add(exception is null ? message : $"{message}: {exception.Message}");
    }
}