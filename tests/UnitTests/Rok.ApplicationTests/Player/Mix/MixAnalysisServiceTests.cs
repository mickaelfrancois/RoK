using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Interfaces.Repositories;
using Rok.Application.Player.Mix;
using Rok.Domain.Entities;
using Rok.Domain.Enums;

namespace Rok.ApplicationTests.Player.Mix;

public class MixAnalysisServiceTests
{
    private readonly Mock<IAudioEnvelopeReader> _reader = new();
    private readonly Mock<ITrackAnalysisRepository> _repository = new();
    private readonly Mock<IPowerStateProvider> _power = new();
    private readonly MixAnalysisService _service;

    private static readonly DateTime FileDate = new(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
    private const long FileSize = 1000;
    private const int MonoRate = 11025;

    private TrackAnalysisEntity? _written;

    public MixAnalysisServiceTests()
    {
        _service = new MixAnalysisService(_reader.Object, _repository.Object, _power.Object, NullLogger<MixAnalysisService>.Instance);
    }

    private static TrackDto Track(long id, string file = "track.mp3", int? bpm = null) =>
        new() { Id = id, Title = $"T{id}", MusicFile = file, Bpm = bpm, FileDate = FileDate, Size = FileSize };

    private static TrackAnalysisEntity Row(Action<TrackAnalysisEntity>? configure = null)
    {
        var row = new TrackAnalysisEntity
        {
            TrackId = 1,
            AlgorithmVersion = TrackAnalysisVersion.Current,
            FileModifiedUtc = FileDate,
            FileSize = FileSize,
        };

        configure?.Invoke(row);

        return row;
    }

    private static float[] Mono120() => SyntheticSignal.Clicks(120, 20, MonoRate, 0.137);

    private void SetupStored(TrackAnalysisEntity? row) =>
        _repository.Setup(r => r.GetAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync(row);

    private void CaptureUpsert() =>
        _repository.Setup(r => r.UpsertAsync(It.IsAny<TrackAnalysisEntity>(), It.IsAny<CancellationToken>()))
            .Callback((TrackAnalysisEntity e, CancellationToken _) => _written = e)
            .Returns(Task.CompletedTask);

    private void SetupTailWithMono(float[]? mono)
    {
        var envelope = Tail();
        _reader.Setup(r => r.ReadAsync(It.IsAny<string>(), EAudioEdge.Tail, It.IsAny<TimeSpan>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, EAudioEdge _, TimeSpan _, bool include, CancellationToken _) =>
                new AudioEdgeSignal(envelope, include ? mono : null, include && mono is not null ? MonoRate : 0, envelope.StartSeconds));
    }

    private void VerifyReads(Times times) =>
        _reader.Verify(r => r.ReadAsync(It.IsAny<string>(), It.IsAny<EAudioEdge>(), It.IsAny<TimeSpan>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), times);

    private static RmsEnvelope Tail() => SyntheticSignal.Envelope(SyntheticSignal.Concat(SyntheticSignal.Sine(25), SyntheticSignal.Silence(5)), 170, 200);

    private static RmsEnvelope Head() => SyntheticSignal.Envelope(SyntheticSignal.Concat(SyntheticSignal.Silence(3), SyntheticSignal.Sine(27)), 0, 200);

    private void SetupReader(EAudioEdge edge, RmsEnvelope? envelope)
    {
        _reader.Setup(r => r.ReadAsync(It.IsAny<string>(), edge, TimeSpan.FromSeconds(MixThresholds.AnalysisWindowSeconds), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(envelope is null ? null : new AudioEdgeSignal(envelope, null, 0, envelope.StartSeconds));
    }

    [Fact(DisplayName = "outro_is_detected_from_the_tail_envelope")]
    public async Task GetOutroAsync_ReadsTailAndDetects()
    {
        // Arrange
        SetupReader(EAudioEdge.Tail, Tail());

        // Act
        var cues = await _service.GetOutroAsync(Track(1), CancellationToken.None);

        // Assert
        Assert.NotNull(cues);
        Assert.Equal(195, cues.MusicEndSeconds, 0.1);
    }

    [Fact(DisplayName = "intro_is_detected_from_the_head_envelope")]
    public async Task GetIntroAsync_ReadsHeadAndDetects()
    {
        // Arrange
        SetupReader(EAudioEdge.Head, Head());

        // Act
        var cues = await _service.GetIntroAsync(Track(1), CancellationToken.None);

        // Assert
        Assert.NotNull(cues);
        Assert.Equal(2.9, cues.MusicStartSeconds, 0.1);
    }

    [Theory(DisplayName = "empty_music_file_returns_null_without_reading")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetOutroAsync_EmptyFile_ReturnsNullWithoutReading(string file)
    {
        // Act
        var outro = await _service.GetOutroAsync(Track(1, file), CancellationToken.None);
        var intro = await _service.GetIntroAsync(Track(1, file), CancellationToken.None);

        // Assert
        Assert.Null(outro);
        Assert.Null(intro);
        _reader.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "analysis_failure_returns_null")]
    public async Task GetOutroAsync_ReaderThrows_ReturnsNull()
    {
        // Arrange
        _reader.Setup(r => r.ReadAsync(It.IsAny<string>(), It.IsAny<EAudioEdge>(), It.IsAny<TimeSpan>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("boom"));

        // Act
        var outro = await _service.GetOutroAsync(Track(1), CancellationToken.None);
        var intro = await _service.GetIntroAsync(Track(1), CancellationToken.None);

        // Assert
        Assert.Null(outro);
        Assert.Null(intro);
    }

    [Fact(DisplayName = "cancelled_analysis_returns_null")]
    public async Task GetOutroAsync_Cancelled_ReturnsNull()
    {
        // Arrange
        _reader.Setup(r => r.ReadAsync(It.IsAny<string>(), It.IsAny<EAudioEdge>(), It.IsAny<TimeSpan>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act
        var outro = await _service.GetOutroAsync(Track(1), new CancellationToken(true));

        // Assert
        Assert.Null(outro);
    }

    [Fact(DisplayName = "unreadable_file_returns_null_and_is_not_cached")]
    public async Task GetOutroAsync_ReaderReturnsNull_IsNotCached()
    {
        // Arrange
        SetupReader(EAudioEdge.Tail, null);

        // Act
        var first = await _service.GetOutroAsync(Track(1), CancellationToken.None);
        var second = await _service.GetOutroAsync(Track(1), CancellationToken.None);

        // Assert
        Assert.Null(first);
        Assert.Null(second);
        _reader.Verify(r => r.ReadAsync(It.IsAny<string>(), EAudioEdge.Tail, It.IsAny<TimeSpan>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact(DisplayName = "analysis_results_are_cached")]
    public async Task GetOutroAsync_SameTrack_ReadsOnce()
    {
        // Arrange
        SetupReader(EAudioEdge.Tail, Tail());

        // Act
        var first = await _service.GetOutroAsync(Track(1), CancellationToken.None);
        var second = await _service.GetOutroAsync(Track(1), CancellationToken.None);

        // Assert
        Assert.NotNull(first);
        Assert.Same(first, second);
        _reader.Verify(r => r.ReadAsync(It.IsAny<string>(), EAudioEdge.Tail, It.IsAny<TimeSpan>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "head_and_tail_are_cached_separately")]
    public async Task GetCues_SameTrackBothEdges_ReadsEachEdgeOnce()
    {
        // Arrange
        SetupReader(EAudioEdge.Tail, Tail());
        SetupReader(EAudioEdge.Head, Head());

        // Act
        await _service.GetOutroAsync(Track(1), CancellationToken.None);
        await _service.GetIntroAsync(Track(1), CancellationToken.None);
        await _service.GetOutroAsync(Track(1), CancellationToken.None);
        await _service.GetIntroAsync(Track(1), CancellationToken.None);

        // Assert
        _reader.Verify(r => r.ReadAsync(It.IsAny<string>(), It.IsAny<EAudioEdge>(), It.IsAny<TimeSpan>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact(DisplayName = "cache_evicts_the_oldest_entry")]
    public async Task GetOutroAsync_NinthTrack_EvictsTheFirst()
    {
        // Arrange
        SetupReader(EAudioEdge.Tail, Tail());

        for (var id = 1; id <= 9; id++)
            await _service.GetOutroAsync(Track(id), CancellationToken.None);

        _reader.Invocations.Clear();

        // Act
        await _service.GetOutroAsync(Track(1), CancellationToken.None);
        await _service.GetOutroAsync(Track(9), CancellationToken.None);

        // Assert
        _reader.Verify(r => r.ReadAsync(It.IsAny<string>(), EAudioEdge.Tail, It.IsAny<TimeSpan>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "cache_keeps_recently_used_entries")]
    public async Task GetOutroAsync_RecentlyUsedEntry_SurvivesEviction()
    {
        // Arrange
        SetupReader(EAudioEdge.Tail, Tail());

        for (var id = 1; id <= 8; id++)
            await _service.GetOutroAsync(Track(id), CancellationToken.None);

        await _service.GetOutroAsync(Track(1), CancellationToken.None);
        await _service.GetOutroAsync(Track(9), CancellationToken.None);
        _reader.Invocations.Clear();

        // Act
        await _service.GetOutroAsync(Track(1), CancellationToken.None);

        // Assert
        _reader.Verify(r => r.ReadAsync(It.IsAny<string>(), It.IsAny<EAudioEdge>(), It.IsAny<TimeSpan>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "concurrent_calls_never_throw")]
    public async Task GetOutroAsync_ConcurrentCalls_Succeed()
    {
        // Arrange
        SetupReader(EAudioEdge.Tail, Tail());

        // Act
        var results = await Task.WhenAll(Enumerable.Range(0, 200).Select(i => Task.Run(() => _service.GetOutroAsync(Track(i % 12), CancellationToken.None))));

        // Assert
        Assert.All(results, Assert.NotNull);
    }

    [Fact(DisplayName = "valid_stored_row_is_served_without_decoding")]
    public async Task GetOutroAsync_ValidRow_DoesNotDecode()
    {
        // Arrange
        SetupStored(Row(r =>
        {
            r.MusicEndSeconds = 195;
            r.FadeOutSeconds = 4;
            r.OutroTempoAnalysed = true;
            r.OutroBeatPhase = 170.2;
            r.Bpm = 120;
            r.BpmConfidence = 0.8;
            r.BpmSource = BpmSource.Tag;
        }));

        // Act
        var cues = await _service.GetOutroAsync(Track(1), CancellationToken.None);

        // Assert
        Assert.NotNull(cues);
        Assert.Equal(195, cues.MusicEndSeconds);
        Assert.Equal(4, cues.FadeOutSeconds);
        Assert.NotNull(cues.Beats);
        Assert.Equal(120, cues.Beats.Bpm);
        Assert.Equal(170.2, cues.Beats.FirstBeatSeconds);
        Assert.Equal(BpmSource.Tag, cues.Beats.Source);
        VerifyReads(Times.Never());
    }

    [Theory(DisplayName = "stale_stored_row_is_decoded_again_and_rewritten_with_the_new_fingerprint")]
    [InlineData("version")]
    [InlineData("date")]
    [InlineData("size")]
    public async Task GetOutroAsync_StaleRow_DecodesAndRewrites(string changed)
    {
        // Arrange
        SetupStored(Row(r =>
        {
            r.MusicEndSeconds = 195;
            r.FadeOutSeconds = 0;
            r.OutroTempoAnalysed = true;
            r.OutroBeatPhase = 170.2;
            r.Bpm = 90;
            r.BpmSource = BpmSource.Detected;

            if (changed == "version")
                r.AlgorithmVersion = TrackAnalysisVersion.Current + 1;
            else if (changed == "date")
                r.FileModifiedUtc = FileDate.AddDays(-1);
            else
                r.FileSize = FileSize + 1;
        }));
        SetupTailWithMono(null);
        CaptureUpsert();

        // Act
        var cues = await _service.GetOutroAsync(Track(1), CancellationToken.None);

        // Assert
        Assert.NotNull(cues);
        VerifyReads(Times.Once());
        Assert.NotNull(_written);
        Assert.Equal(TrackAnalysisVersion.Current, _written.AlgorithmVersion);
        Assert.Equal(FileDate, _written.FileModifiedUtc);
        Assert.Equal(FileSize, _written.FileSize);
        Assert.Null(_written.Bpm);
    }

    [Fact(DisplayName = "tag_bpm_is_used_as_is_and_flagged_as_tag")]
    public async Task GetOutroAsync_TagBpm_UsesTagAndOnlyDetectsPhase()
    {
        // Arrange
        SetupStored(null);
        SetupTailWithMono(Mono120());
        CaptureUpsert();

        // Act
        var cues = await _service.GetOutroAsync(Track(1, bpm: 120), CancellationToken.None);

        // Assert
        Assert.NotNull(cues?.Beats);
        Assert.Equal(120, cues.Beats.Bpm);
        Assert.Equal(BpmSource.Tag, cues.Beats.Source);
        Assert.True(cues.Beats.FirstBeatSeconds >= 170);
        Assert.NotNull(_written);
        Assert.Equal(120, _written.Bpm);
        Assert.Equal(BpmSource.Tag, _written.BpmSource);
        Assert.True(_written.OutroTempoAnalysed);
        Assert.Equal(cues.Beats.FirstBeatSeconds, _written.OutroBeatPhase);
    }

    [Fact(DisplayName = "tag_bpm_outside_the_plausible_range_is_ignored")]
    public async Task GetOutroAsync_AbsurdTag_FallsBackToDetection()
    {
        // Arrange
        SetupStored(null);
        SetupTailWithMono(Mono120());

        // Act
        var cues = await _service.GetOutroAsync(Track(1, bpm: 999), CancellationToken.None);

        // Assert
        Assert.NotNull(cues?.Beats);
        Assert.Equal(BpmSource.Detected, cues.Beats.Source);
        Assert.Equal(120, cues.Beats.Bpm, 1.2);
    }

    [Fact(DisplayName = "second_window_reuses_the_detected_bpm_of_the_row")]
    public async Task GetOutroAsync_RowWithDetectedBpm_OnlyDetectsPhase()
    {
        // Arrange
        SetupStored(Row(r =>
        {
            r.MusicStartSeconds = 0;
            r.IntroTempoAnalysed = true;
            r.IntroBeatPhase = 0.137;
            r.Bpm = 120.0;
            r.BpmConfidence = 0.7;
            r.BpmSource = BpmSource.Detected;
        }));
        SetupTailWithMono(Mono120());
        CaptureUpsert();

        // Act
        var cues = await _service.GetOutroAsync(Track(1), CancellationToken.None);

        // Assert
        Assert.NotNull(cues?.Beats);
        Assert.Equal(120.0, cues.Beats.Bpm);
        Assert.Equal(BpmSource.Detected, cues.Beats.Source);
        Assert.NotNull(_written);
        Assert.Equal(120.0, _written.Bpm);
        Assert.Equal(0.137, _written.IntroBeatPhase);
        Assert.True(_written.OutroTempoAnalysed);
    }

    [Fact(DisplayName = "on_battery_only_the_cues_are_computed_and_the_tempo_flag_stays_off")]
    public async Task GetOutroAsync_OnBattery_SkipsTempo()
    {
        // Arrange
        _power.SetupGet(p => p.IsOnBattery).Returns(true);
        SetupStored(null);
        SetupTailWithMono(Mono120());
        CaptureUpsert();

        // Act
        var cues = await _service.GetOutroAsync(Track(1, bpm: 120), CancellationToken.None);

        // Assert
        Assert.NotNull(cues);
        Assert.Null(cues.Beats);
        Assert.Equal(195, cues.MusicEndSeconds, 0.1);
        _reader.Verify(r => r.ReadAsync(It.IsAny<string>(), EAudioEdge.Tail, It.IsAny<TimeSpan>(), false, It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(_written);
        Assert.False(_written.OutroTempoAnalysed);
        Assert.NotNull(_written.MusicEndSeconds);
        Assert.Null(_written.Bpm);
    }

    [Fact(DisplayName = "on_battery_a_row_with_cues_but_no_tempo_is_served_without_decoding")]
    public async Task GetOutroAsync_OnBatteryRowWithoutTempo_DoesNotDecode()
    {
        // Arrange
        _power.SetupGet(p => p.IsOnBattery).Returns(true);
        SetupStored(Row(r =>
        {
            r.MusicEndSeconds = 195;
            r.FadeOutSeconds = 0;
        }));

        // Act
        var cues = await _service.GetOutroAsync(Track(1), CancellationToken.None);

        // Assert
        Assert.NotNull(cues);
        Assert.Null(cues.Beats);
        VerifyReads(Times.Never());
    }

    [Fact(DisplayName = "back_on_mains_the_same_row_is_decoded_with_mono_and_gets_its_grid")]
    public async Task GetOutroAsync_BackOnMains_AddsTheGrid()
    {
        // Arrange
        SetupStored(Row(r =>
        {
            r.MusicEndSeconds = 195;
            r.FadeOutSeconds = 0;
        }));
        SetupTailWithMono(Mono120());
        _power.SetupGet(p => p.IsOnBattery).Returns(true);
        var onBattery = await _service.GetOutroAsync(Track(1, bpm: 120), CancellationToken.None);
        _reader.Invocations.Clear();
        _power.SetupGet(p => p.IsOnBattery).Returns(false);

        // Act
        var onMains = await _service.GetOutroAsync(Track(1, bpm: 120), CancellationToken.None);

        // Assert
        Assert.Null(onBattery?.Beats);
        Assert.NotNull(onMains?.Beats);
        _reader.Verify(r => r.ReadAsync(It.IsAny<string>(), EAudioEdge.Tail, It.IsAny<TimeSpan>(), true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "same_track_id_with_another_music_file_is_a_cache_miss")]
    public async Task GetOutroAsync_SameIdOtherFile_ReadsAgain()
    {
        // Arrange
        SetupReader(EAudioEdge.Tail, Tail());

        // Act
        await _service.GetOutroAsync(Track(1, "a.mp3"), CancellationToken.None);
        await _service.GetOutroAsync(Track(1, "b.mp3"), CancellationToken.None);

        // Assert
        VerifyReads(Times.Exactly(2));
    }

    [Fact(DisplayName = "repository_failures_never_hide_the_cues")]
    public async Task GetOutroAsync_RepositoryThrows_StillReturnsCues()
    {
        // Arrange
        _repository.Setup(r => r.GetAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("locked"));
        _repository.Setup(r => r.UpsertAsync(It.IsAny<TrackAnalysisEntity>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("fk"));
        SetupReader(EAudioEdge.Tail, Tail());

        // Act
        var cues = await _service.GetOutroAsync(Track(1), CancellationToken.None);

        // Assert
        Assert.NotNull(cues);
        Assert.Equal(195, cues.MusicEndSeconds, 0.1);
    }

    [Fact(DisplayName = "cancellation_after_decoding_returns_null_and_writes_nothing")]
    public async Task GetOutroAsync_CancelledAfterDecode_DoesNotWrite()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var envelope = Tail();
        _reader.Setup(r => r.ReadAsync(It.IsAny<string>(), EAudioEdge.Tail, It.IsAny<TimeSpan>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns((string _, EAudioEdge _, TimeSpan _, bool _, CancellationToken _) =>
            {
                cts.Cancel();

                return Task.FromResult<AudioEdgeSignal?>(new AudioEdgeSignal(envelope, null, 0, envelope.StartSeconds));
            });

        // Act
        var cues = await _service.GetOutroAsync(Track(1), cts.Token);

        // Assert
        Assert.Null(cues);
        _repository.Verify(r => r.UpsertAsync(It.IsAny<TrackAnalysisEntity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "track_without_a_valid_id_is_analysed_without_touching_the_repository")]
    public async Task GetOutroAsync_NoId_SkipsRepository()
    {
        // Arrange
        SetupReader(EAudioEdge.Tail, Tail());

        // Act
        var cues = await _service.GetOutroAsync(Track(0), CancellationToken.None);

        // Assert
        Assert.NotNull(cues);
        _repository.VerifyNoOtherCalls();
    }
}