using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Player.Mix;

namespace Rok.ApplicationTests.Player.Mix;

public class MixAnalysisServiceTests
{
    private readonly Mock<IAudioEnvelopeReader> _reader = new();
    private readonly MixAnalysisService _service;

    public MixAnalysisServiceTests()
    {
        _service = new MixAnalysisService(_reader.Object, NullLogger<MixAnalysisService>.Instance);
    }

    private static TrackDto Track(long id, string file = "track.mp3") => new() { Id = id, Title = $"T{id}", MusicFile = file };

    private static RmsEnvelope Tail() => SyntheticSignal.Envelope(SyntheticSignal.Concat(SyntheticSignal.Sine(25), SyntheticSignal.Silence(5)), 170, 200);

    private static RmsEnvelope Head() => SyntheticSignal.Envelope(SyntheticSignal.Concat(SyntheticSignal.Silence(3), SyntheticSignal.Sine(27)), 0, 200);

    private void SetupReader(EAudioEdge edge, RmsEnvelope? envelope)
    {
        _reader.Setup(r => r.ReadAsync(It.IsAny<string>(), edge, TimeSpan.FromSeconds(MixThresholds.AnalysisWindowSeconds), It.IsAny<CancellationToken>()))
            .ReturnsAsync(envelope);
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
        _reader.Setup(r => r.ReadAsync(It.IsAny<string>(), It.IsAny<EAudioEdge>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
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
        _reader.Setup(r => r.ReadAsync(It.IsAny<string>(), It.IsAny<EAudioEdge>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
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
        _reader.Verify(r => r.ReadAsync(It.IsAny<string>(), EAudioEdge.Tail, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
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
        _reader.Verify(r => r.ReadAsync(It.IsAny<string>(), EAudioEdge.Tail, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
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
        _reader.Verify(r => r.ReadAsync(It.IsAny<string>(), It.IsAny<EAudioEdge>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
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
        _reader.Verify(r => r.ReadAsync(It.IsAny<string>(), EAudioEdge.Tail, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
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
        _reader.Verify(r => r.ReadAsync(It.IsAny<string>(), It.IsAny<EAudioEdge>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
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
}