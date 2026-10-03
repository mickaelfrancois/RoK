using Rok.Application.Dto;
using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Repositories;
using Rok.Application.Player.Mix;
using Rok.Domain.Entities;

namespace Rok.MetadataTool;

internal enum TrackScanStatus
{
    Skipped,
    Analysed,
    MissingFile,
    Failed,
    Interrupted,
}

internal sealed record TrackScanResult(TrackDto Track, TrackScanStatus Status, TrackAnalysisEntity? Row, string? Reason);

internal sealed record MixScanProgress(int Processed, int Total, TimeSpan Elapsed);

/// <summary>
/// Runs the app's own <see cref="MixAnalysisService"/> over a list of tracks, intro then outro, so the rows
/// written are the ones the app would write. Each worker owns its decoder and its service.
/// </summary>
internal sealed class MixScanner(
    ITrackAnalysisRepository repository,
    Func<WarningCollector, IAudioEnvelopeReader> readerFactory,
    Func<string, bool> fileExists,
    TimeProvider time)
{
    public async Task<IReadOnlyList<TrackScanResult>> RunAsync(IReadOnlyList<TrackDto> tracks, int parallel, IProgress<MixScanProgress>? progress, CancellationToken ct)
    {
        var results = new TrackScanResult?[tracks.Count];
        var workers = Math.Min(Math.Max(1, parallel), tracks.Count);
        var started = time.GetTimestamp();
        var next = -1;
        var processed = 0;

        async Task WorkAsync()
        {
            var collector = new WarningCollector();
            using var counter = new CountingEnvelopeReader(readerFactory(collector));
            using var service = new MixAnalysisService(counter, repository, new MainsPowerStateProvider(), new CollectingLogger<MixAnalysisService>(collector));

            while (!ct.IsCancellationRequested)
            {
                var index = Interlocked.Increment(ref next);

                if (index >= tracks.Count)
                    return;

                results[index] = await ScanAsync(tracks[index], service, counter, collector, ct).ConfigureAwait(false);

                var done = Interlocked.Increment(ref processed);
                progress?.Report(new MixScanProgress(done, tracks.Count, time.GetElapsedTime(started)));
            }
        }

        var tasks = Enumerable.Range(0, workers).Select(_ => Task.Run(WorkAsync, CancellationToken.None)).ToArray();

        await Task.WhenAll(tasks).ConfigureAwait(false);

        return results.OfType<TrackScanResult>().ToList();
    }

    private async Task<TrackScanResult> ScanAsync(TrackDto track, MixAnalysisService service, CountingEnvelopeReader counter, WarningCollector collector, CancellationToken ct)
    {
        if (!fileExists(track.MusicFile))
            return new TrackScanResult(track, TrackScanStatus.MissingFile, null, "file not found");

        collector.Drain();
        counter.Reset();

        try
        {
            await service.GetIntroAsync(track, ct).ConfigureAwait(false);

            if (!ct.IsCancellationRequested)
                await service.GetOutroAsync(track, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new TrackScanResult(track, TrackScanStatus.Failed, null, ex.Message);
        }

        var row = await ReadRowAsync(track).ConfigureAwait(false);

        if (ct.IsCancellationRequested && row is not { IntroTempoAnalysed: true, OutroTempoAnalysed: true })
            return new TrackScanResult(track, TrackScanStatus.Interrupted, row, null);

        var warnings = collector.Drain();

        if (warnings.Count > 0)
            return new TrackScanResult(track, TrackScanStatus.Failed, row, warnings[0]);

        if (counter.NullReads > 0)
            return new TrackScanResult(track, TrackScanStatus.Failed, row, "the file could not be decoded");

        return new TrackScanResult(track, counter.Reads == 0 ? TrackScanStatus.Skipped : TrackScanStatus.Analysed, row, null);
    }

    private async Task<TrackAnalysisEntity?> ReadRowAsync(TrackDto track)
    {
        try
        {
            return await repository.GetAsync(track.Id, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception)
        {
            return null;
        }
    }
}