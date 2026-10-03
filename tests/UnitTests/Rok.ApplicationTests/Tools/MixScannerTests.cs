using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Dto;
using Rok.Application.Interfaces;
using Rok.Application.Player.Mix;
using Rok.Domain.Entities;
using Rok.Domain.Enums;
using Rok.MetadataTool;

namespace Rok.ApplicationTests.Tools;

public class MixScannerTests
{
    private static readonly DateTime FileDate = new(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
    private const long FileSize = 1000;

    private readonly InMemoryTrackAnalysisRepository _repository = new();

    private static TrackDto Track(long id, int? bpm = null, string? file = null) =>
        new() { Id = id, Title = $"T{id}", MusicFile = file ?? $"track{id}.mp3", Bpm = bpm, FileDate = FileDate, Size = FileSize };

    private MixScanner Scanner(Func<ScriptedEnvelopeReader> reader, Func<string, bool>? fileExists = null) =>
        new(_repository, _ => reader(), fileExists ?? (_ => true), TimeProvider.System);

    private static async Task<TrackAnalysisEntity?> AppRowAsync(TrackDto track, ScriptedEnvelopeReader reader)
    {
        var repository = new InMemoryTrackAnalysisRepository();
        var power = new Mock<IPowerStateProvider>();
        power.SetupGet(p => p.IsOnBattery).Returns(false);

        using var service = new MixAnalysisService(reader, repository, power.Object, NullLogger<MixAnalysisService>.Instance);

        await service.GetIntroAsync(track, CancellationToken.None);
        await service.GetOutroAsync(track, CancellationToken.None);

        return repository.Find(track.Id);
    }

    private static void AssertSameRow(TrackAnalysisEntity? expected, TrackAnalysisEntity? actual)
    {
        Assert.NotNull(expected);
        Assert.NotNull(actual);

        foreach (var property in typeof(TrackAnalysisEntity).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            Assert.True(Equals(property.GetValue(expected), property.GetValue(actual)), $"{property.Name} differs: {property.GetValue(expected)} vs {property.GetValue(actual)}");
    }

    [Theory(DisplayName = "scan_row_equals_the_row_the_app_writes")]
    [InlineData(120, false)]
    [InlineData(null, false)]
    [InlineData(null, true)]
    public async Task RunAsync_Row_EqualsTheAppRow(int? bpm, bool silentIntro)
    {
        // Arrange
        var track = Track(1, bpm);
        ScriptedEnvelopeReader NewReader() => silentIntro ? new ScriptedEnvelopeReader { Head = SyntheticSignalEdges.Silent() } : new ScriptedEnvelopeReader();
        var scanner = Scanner(NewReader);

        // Act
        var results = await scanner.RunAsync([track], 1, null, CancellationToken.None);
        var expected = await AppRowAsync(track, NewReader());

        // Assert
        var result = Assert.Single(results);
        Assert.Equal(TrackScanStatus.Analysed, result.Status);
        AssertSameRow(expected, _repository.Find(1));
        AssertSameRow(expected, result.Row);

        if (bpm is null)
        {
            Assert.Equal(BpmSource.Detected, expected!.BpmSource);
            Assert.Equal(TrackAnalysisVersion.Current, expected.AlgorithmVersion);
        }

        if (silentIntro)
            Assert.Null(expected!.MusicStartSeconds);
    }

    [Fact(DisplayName = "valid_complete_row_is_skipped_without_decoding")]
    public async Task RunAsync_CompleteRow_IsSkipped()
    {
        // Arrange
        var track = Track(1, 120);
        await Scanner(() => new ScriptedEnvelopeReader()).RunAsync([track], 1, null, CancellationToken.None);
        var upserts = _repository.Upserts;
        var reader = new ScriptedEnvelopeReader();

        // Act
        var results = await Scanner(() => reader).RunAsync([track], 1, null, CancellationToken.None);

        // Assert
        Assert.Equal(TrackScanStatus.Skipped, Assert.Single(results).Status);
        Assert.Equal(0, reader.Reads);
        Assert.Equal(upserts, _repository.Upserts);
        Assert.NotNull(results[0].Row);
    }

    [Fact(DisplayName = "valid_row_without_tempo_is_completed")]
    public async Task RunAsync_RowWithoutTempo_IsCompleted()
    {
        // Arrange
        var track = Track(1);
        await _repository.UpsertAsync(
            new TrackAnalysisEntity
            {
                TrackId = 1,
                AlgorithmVersion = TrackAnalysisVersion.Current,
                FileModifiedUtc = FileDate,
                FileSize = FileSize,
                MusicStartSeconds = 2.9,
                MusicEndSeconds = 195,
                FadeOutSeconds = 5,
            },
            CancellationToken.None);
        var reader = new ScriptedEnvelopeReader();

        // Act
        var results = await Scanner(() => reader).RunAsync([track], 1, null, CancellationToken.None);

        // Assert
        var result = Assert.Single(results);
        Assert.Equal(TrackScanStatus.Analysed, result.Status);
        Assert.True(reader.Reads > 0);
        Assert.True(result.Row!.IntroTempoAnalysed);
        Assert.True(result.Row.OutroTempoAnalysed);
        Assert.NotNull(result.Row.Bpm);
    }

    [Theory(DisplayName = "stale_version_or_changed_file_is_reanalysed")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RunAsync_StaleRow_IsReanalysed(bool staleVersion)
    {
        // Arrange
        var track = Track(1, 120);
        await Scanner(() => new ScriptedEnvelopeReader()).RunAsync([track], 1, null, CancellationToken.None);
        var stored = _repository.Find(1)!;

        if (staleVersion)
            stored.AlgorithmVersion = TrackAnalysisVersion.Current - 1;
        else
            stored.FileSize = FileSize + 1;

        var reader = new ScriptedEnvelopeReader();

        // Act
        var results = await Scanner(() => reader).RunAsync([track], 1, null, CancellationToken.None);

        // Assert
        Assert.Equal(TrackScanStatus.Analysed, Assert.Single(results).Status);
        Assert.Equal(2, reader.Reads);
        Assert.Equal(TrackAnalysisVersion.Current, _repository.Find(1)!.AlgorithmVersion);
        Assert.Equal(FileSize, _repository.Find(1)!.FileSize);
    }

    [Fact(DisplayName = "cancellation_keeps_written_rows_and_rerun_skips_them")]
    public async Task RunAsync_Cancelled_KeepsWrittenRowsAndRerunSkipsThem()
    {
        // Arrange
        TrackDto[] tracks = [Track(1, 120), Track(2, 120), Track(3, 120), Track(4, 120), Track(5, 120)];
        using var cts = new CancellationTokenSource();
        var reads = 0;
        var cancelling = new ScriptedEnvelopeReader
        {
            Before = (_, _) =>
            {
                if (Interlocked.Increment(ref reads) == 5)
                    cts.Cancel();

                return Task.CompletedTask;
            },
        };

        // Act
        var interrupted = await Scanner(() => cancelling).RunAsync(tracks, 1, null, cts.Token);
        var rowAfterCancel = _repository.Find(3);
        var resumed = new ScriptedEnvelopeReader();
        var rerun = await Scanner(() => resumed).RunAsync(tracks, 1, null, CancellationToken.None);

        // Assert
        Assert.Equal(3, interrupted.Count);
        Assert.Equal([TrackScanStatus.Analysed, TrackScanStatus.Analysed, TrackScanStatus.Interrupted], interrupted.Select(r => r.Status));
        Assert.DoesNotContain(interrupted, r => r.Status == TrackScanStatus.Failed);
        Assert.Null(rowAfterCancel);
        Assert.NotNull(_repository.Find(2));
        Assert.Equal(6, resumed.Reads);
        Assert.Equal(
            [TrackScanStatus.Skipped, TrackScanStatus.Skipped, TrackScanStatus.Analysed, TrackScanStatus.Analysed, TrackScanStatus.Analysed],
            rerun.Select(r => r.Status));
    }

    [Fact(DisplayName = "track_fully_analysed_before_the_cancellation_is_not_counted_as_interrupted")]
    public async Task RunAsync_CancelledAfterBothEdgesWritten_IsAnalysed()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        _repository.AfterUpsert = count =>
        {
            if (count == 2)
                cts.Cancel();
        };

        // Act
        var results = await Scanner(() => new ScriptedEnvelopeReader()).RunAsync([Track(1, 120), Track(2, 120)], 1, null, cts.Token);

        // Assert
        var result = Assert.Single(results);
        Assert.Equal(TrackScanStatus.Analysed, result.Status);
        Assert.NotNull(_repository.Find(1));
        Assert.Null(_repository.Find(2));
    }

    [Fact(DisplayName = "missing_file_is_counted_and_the_scan_continues")]
    public async Task RunAsync_MissingFile_ContinuesWithOtherTracks()
    {
        // Arrange
        TrackDto[] tracks = [Track(1, 120), Track(2, 120), Track(3, 120)];
        var reader = new ScriptedEnvelopeReader();

        // Act
        var results = await Scanner(() => reader, file => file != "track2.mp3").RunAsync(tracks, 1, null, CancellationToken.None);

        // Assert
        Assert.Equal([TrackScanStatus.Analysed, TrackScanStatus.MissingFile, TrackScanStatus.Analysed], results.Select(r => r.Status));
        Assert.Equal(4, reader.Reads);
        Assert.Null(_repository.Find(2));
    }

    [Fact(DisplayName = "unreadable_file_is_reported_as_failed")]
    public async Task RunAsync_UnreadableFile_IsFailed()
    {
        // Arrange
        TrackDto[] tracks = [Track(1, 120), Track(2, 120)];
        var reader = new ScriptedEnvelopeReader { Unreadable = file => file == "track1.mp3" };

        // Act
        var results = await Scanner(() => reader).RunAsync(tracks, 1, null, CancellationToken.None);

        // Assert
        Assert.Equal([TrackScanStatus.Failed, TrackScanStatus.Analysed], results.Select(r => r.Status));
        Assert.False(string.IsNullOrWhiteSpace(results[0].Reason));
    }

    [Fact(DisplayName = "storage_failure_is_reported_as_failed")]
    public async Task RunAsync_StorageFailure_IsFailed()
    {
        // Arrange
        _repository.FailOnUpsert = true;
        TrackDto[] tracks = [Track(1, 120), Track(2, 120)];

        // Act
        var results = await Scanner(() => new ScriptedEnvelopeReader()).RunAsync(tracks, 1, null, CancellationToken.None);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal(TrackScanStatus.Failed, r.Status));
        Assert.Contains("disk full", results[0].Reason);
    }

    [Fact(DisplayName = "parallel_creates_one_reader_per_worker_and_caps_concurrency")]
    public async Task RunAsync_Parallel_OneReaderPerWorkerAndBoundedConcurrency()
    {
        // Arrange
        const int workers = 3;
        var tracks = Enumerable.Range(1, 6).Select(i => Track(i, 120)).ToArray();
        var created = 0;
        var arrived = 0;
        var current = 0;
        var max = 0;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var scanner = new MixScanner(
            _repository,
            _ =>
            {
                Interlocked.Increment(ref created);

                return new ScriptedEnvelopeReader
                {
                    Before = async (_, _) =>
                    {
                        var now = Interlocked.Increment(ref current);
                        InterlockedMax(ref max, now);

                        if (Interlocked.Increment(ref arrived) == workers)
                            gate.TrySetResult();

                        await gate.Task.WaitAsync(TimeSpan.FromSeconds(30));
                        Interlocked.Decrement(ref current);
                    },
                };
            },
            _ => true,
            TimeProvider.System);

        // Act
        var results = await scanner.RunAsync(tracks, workers, null, CancellationToken.None);

        // Assert
        Assert.Equal(workers, created);
        Assert.Equal(workers, max);
        Assert.All(results, r => Assert.Equal(TrackScanStatus.Analysed, r.Status));
    }

    [Fact(DisplayName = "parallel_is_capped_by_the_number_of_tracks")]
    public async Task RunAsync_MoreWorkersThanTracks_CreatesOneReaderPerTrack()
    {
        // Arrange
        var created = 0;
        var scanner = new MixScanner(_repository, _ => { Interlocked.Increment(ref created); return new ScriptedEnvelopeReader(); }, _ => true, TimeProvider.System);

        // Act
        await scanner.RunAsync([Track(1, 120), Track(2, 120)], 10, null, CancellationToken.None);

        // Assert
        Assert.Equal(2, created);
    }

    [Fact(DisplayName = "progress_reports_each_processed_track")]
    public async Task RunAsync_Progress_ReportsEveryTrack()
    {
        // Arrange
        var reported = new List<MixScanProgress>();
        var progress = new SynchronousProgress(reported.Add);

        // Act
        await Scanner(() => new ScriptedEnvelopeReader()).RunAsync([Track(1, 120), Track(2, 120)], 1, progress, CancellationToken.None);

        // Assert
        Assert.Equal([1, 2], reported.Select(p => p.Processed));
        Assert.All(reported, p => Assert.Equal(2, p.Total));
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int snapshot;

        do
        {
            snapshot = Volatile.Read(ref target);

            if (value <= snapshot)
                return;
        }
        while (Interlocked.CompareExchange(ref target, value, snapshot) != snapshot);
    }

    private sealed class SynchronousProgress(Action<MixScanProgress> report) : IProgress<MixScanProgress>
    {
        public void Report(MixScanProgress value) => report(value);
    }
}