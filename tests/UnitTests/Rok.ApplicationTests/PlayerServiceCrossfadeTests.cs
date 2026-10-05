using Microsoft.Extensions.Logging;
using Moq;
using Rok.Application.Dto;
using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Pictures;
using Rok.Application.Messages;
using Rok.Application.Player;
using Rok.Application.Player.Mix;

namespace Rok.ApplicationTests;

public class PlayerServiceCrossfadeTests
{
    private readonly Mock<IPlayerEngine> _engine = new();
    private readonly Mock<IAppOptions> _appOptions = new();
    private readonly Mock<ICallDetectionService> _callDetection = new();
    private readonly Mock<IAlbumPicture> _albumPicture = new();
    private readonly Mock<ILogger<PlayerService>> _logger = new();

    public PlayerServiceCrossfadeTests()
    {
        _engine.Setup(o => o.SetTrack(It.IsAny<TrackDto>(), It.IsAny<float>())).Returns(true);
        _engine.Setup(o => o.CrossfadeToAsync(It.IsAny<TrackDto>(), It.IsAny<float>(), It.IsAny<double>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _appOptions.SetupGet(o => o.CrossFade).Returns(true);
    }

    private PlayerService BuildService() => new(_callDetection.Object, _engine.Object, _appOptions.Object, null, null, _albumPicture.Object, TimeProvider.System, new Messenger(), Mock.Of<IMixCueProvider>(), _logger.Object);

    private static TrackDto BuildTrack(long id, bool isLive = false) => new() { Id = id, Title = $"t{id}", IsAlbumLive = isLive };

    private void SetEnginePosition(double position, double length)
    {
        _engine.SetupGet(o => o.Position).Returns(position);
        _engine.SetupGet(o => o.Length).Returns(length);
    }

    private void SetCrossfadeDelay(int seconds) => _appOptions.SetupGet(o => o.CrossfadeDurationSeconds).Returns(seconds);

    private void RaiseMediaAboutToEnd() => _engine.Raise(m => m.OnMediaAboutToEnd += null, this, EventArgs.Empty);

    [Fact(DisplayName = "OnMediaAboutToEnd with crossfade enabled should call CrossfadeToAsync when conditions allow it")]
    public void OnMediaAboutToEnd_ShouldCallCrossfade_WhenConditionsAllow()
    {
        // Arrange
        SetEnginePosition(position: 95, length: 100);
        SetCrossfadeDelay(5);
        PlayerService sut = BuildService();
        sut.LoadPlaylist(new List<TrackDto> { BuildTrack(1), BuildTrack(2) });

        // Act
        RaiseMediaAboutToEnd();

        // Assert
        _engine.Verify(o => o.CrossfadeToAsync(It.Is<TrackDto>(t => t.Id == 2), It.IsAny<float>(), 5, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "OnMediaAboutToEnd should crossfade between two live tracks that are not consecutive in the same album")]
    public void OnMediaAboutToEnd_ShouldCrossfade_WhenLiveTracksAreNotConsecutive()
    {
        // Arrange
        SetEnginePosition(position: 95, length: 100);
        SetCrossfadeDelay(5);
        PlayerService sut = BuildService();
        sut.LoadPlaylist(new List<TrackDto> { BuildTrack(1, isLive: true), BuildTrack(2, isLive: true) });
        _engine.Invocations.Clear();

        // Act
        RaiseMediaAboutToEnd();

        // Assert
        _engine.Verify(o => o.CrossfadeToAsync(It.Is<TrackDto>(t => t.Id == 2), It.IsAny<float>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Once);
        _engine.Verify(o => o.QueueNextTrack(It.IsAny<TrackDto>(), It.IsAny<float>()), Times.Never);
    }

    [Fact(DisplayName = "OnMediaAboutToEnd should queue the next track gaplessly between consecutive tracks of a live album")]
    public void OnMediaAboutToEnd_ShouldQueueNextTrack_WhenLiveTracksAreConsecutive()
    {
        // Arrange
        SetEnginePosition(position: 95, length: 100);
        SetCrossfadeDelay(5);
        PlayerService sut = BuildService();
        TrackDto first = new() { Id = 1, Title = "t1", IsAlbumLive = true, AlbumId = 10, TrackNumber = 3 };
        TrackDto second = new() { Id = 2, Title = "t2", IsAlbumLive = true, AlbumId = 10, TrackNumber = 4 };
        sut.LoadPlaylist(new List<TrackDto> { first, second });
        _engine.Invocations.Clear();

        // Act
        RaiseMediaAboutToEnd();

        // Assert
        _engine.Verify(o => o.QueueNextTrack(It.Is<TrackDto>(t => t.Id == 2), It.IsAny<float>()), Times.Once);
        _engine.Verify(o => o.CrossfadeToAsync(It.IsAny<TrackDto>(), It.IsAny<float>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "OnMediaAboutToEnd should queue the next track gaplessly when player is muted")]
    public void OnMediaAboutToEnd_ShouldQueueNextTrack_WhenMuted()
    {
        // Arrange
        SetEnginePosition(position: 95, length: 100);
        SetCrossfadeDelay(5);
        PlayerService sut = BuildService();
        sut.LoadPlaylist(new List<TrackDto> { BuildTrack(1), BuildTrack(2) });
        sut.IsMuted = true;
        _engine.Invocations.Clear();

        // Act
        RaiseMediaAboutToEnd();

        // Assert
        _engine.Verify(o => o.QueueNextTrack(It.Is<TrackDto>(t => t.Id == 2), It.IsAny<float>()), Times.Once);
        _engine.Verify(o => o.CrossfadeToAsync(It.IsAny<TrackDto>(), It.IsAny<float>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Never);
        _engine.Verify(o => o.SetTrack(It.IsAny<TrackDto>(), It.IsAny<float>()), Times.Never);
    }

    [Fact(DisplayName = "OnMediaAboutToEnd should do nothing when playlist is at last track without looping")]
    public void OnMediaAboutToEnd_ShouldDoNothing_WhenAtEndOfPlaylistWithoutLooping()
    {
        // Arrange
        SetEnginePosition(position: 95, length: 100);
        SetCrossfadeDelay(5);
        PlayerService sut = BuildService();
        sut.LoadPlaylist(new List<TrackDto> { BuildTrack(1) });
        _engine.Invocations.Clear();

        // Act
        RaiseMediaAboutToEnd();

        // Assert
        _engine.Verify(o => o.CrossfadeToAsync(It.IsAny<TrackDto>(), It.IsAny<float>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Never);
        _engine.Verify(o => o.SetTrack(It.IsAny<TrackDto>(), It.IsAny<float>()), Times.Never);
    }

    [Fact(DisplayName = "OnMediaAboutToEnd should wrap to first track when looping is enabled")]
    public void OnMediaAboutToEnd_ShouldWrapToFirstTrack_WhenLoopingEnabled()
    {
        // Arrange
        SetEnginePosition(position: 95, length: 100);
        SetCrossfadeDelay(5);
        PlayerService sut = BuildService();
        sut.LoadPlaylist(new List<TrackDto> { BuildTrack(1), BuildTrack(2) });
        sut.IsLoopingEnabled = true;
        sut.Next();
        _engine.Invocations.Clear();

        // Act
        RaiseMediaAboutToEnd();

        // Assert
        _engine.Verify(o => o.CrossfadeToAsync(It.Is<TrackDto>(t => t.Id == 1), It.IsAny<float>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory(DisplayName = "crossfade_uses_the_duration_chosen_in_the_options")]
    [InlineData(8, 8)]
    [InlineData(30, 12)]
    public void OnMediaAboutToEnd_UsesOptionDuration(int storedSeconds, double expectedSeconds)
    {
        // Arrange
        SetEnginePosition(position: 100 - expectedSeconds, length: 100);
        SetCrossfadeDelay(storedSeconds);
        PlayerService sut = BuildService();
        sut.LoadPlaylist(new List<TrackDto> { BuildTrack(1), BuildTrack(2) });

        // Act
        RaiseMediaAboutToEnd();

        // Assert
        _engine.Verify(o => o.CrossfadeToAsync(It.Is<TrackDto>(t => t.Id == 2), It.IsAny<float>(), expectedSeconds, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "OnMediaAboutToEnd should fall back to Next when crossfade duration is zero")]
    public void OnMediaAboutToEnd_ShouldFallBackToNext_WhenCrossfadeDurationIsZero()
    {
        // Arrange
        SetEnginePosition(position: 100, length: 100);
        SetCrossfadeDelay(0);
        PlayerService sut = BuildService();
        sut.LoadPlaylist(new List<TrackDto> { BuildTrack(1), BuildTrack(2) });
        _engine.Invocations.Clear();

        // Act
        RaiseMediaAboutToEnd();

        // Assert
        _engine.Verify(o => o.CrossfadeToAsync(It.IsAny<TrackDto>(), It.IsAny<float>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Never);
        _engine.Verify(o => o.SetTrack(It.Is<TrackDto>(t => t.Id == 2), It.IsAny<float>()), Times.Once);
    }

    [Fact(DisplayName = "Crossfade completion should advance current track and fire playing state")]
    public async Task Crossfade_OnCompletion_ShouldAdvanceCurrentTrackAndPlay()
    {
        // Arrange
        SetEnginePosition(position: 95, length: 100);
        SetCrossfadeDelay(5);
        TaskCompletionSource<bool> crossfadeCompleted = new();
        _engine.Setup(o => o.CrossfadeToAsync(It.IsAny<TrackDto>(), It.IsAny<float>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
               .Returns<TrackDto, float, double, CancellationToken>((t, g, d, ct) => { crossfadeCompleted.SetResult(true); return Task.FromResult(true); });
        PlayerService sut = BuildService();
        sut.LoadPlaylist(new List<TrackDto> { BuildTrack(1), BuildTrack(2) });

        // Act
        RaiseMediaAboutToEnd();
        await crossfadeCompleted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await Task.Yield();

        // Assert
        Assert.Equal(2, sut.CurrentTrack?.Id);
    }

    private TaskCompletionSource<CancellationToken> KeepCrossfadeInFlight()
    {
        TaskCompletionSource<CancellationToken> entered = new();
        _engine.Setup(o => o.CrossfadeToAsync(It.IsAny<TrackDto>(), It.IsAny<float>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
               .Returns<TrackDto, float, double, CancellationToken>(async (_, _, _, token) =>
               {
                   entered.TrySetResult(token);
                   await Task.Delay(Timeout.Infinite, token);

                   return true;
               });

        return entered;
    }

    [Fact(DisplayName = "pausing_during_a_crossfade_cancels_it")]
    public async Task Pause_CancelsRunningCrossfade()
    {
        // Arrange
        SetEnginePosition(position: 95, length: 100);
        SetCrossfadeDelay(5);
        TaskCompletionSource<CancellationToken> entered = KeepCrossfadeInFlight();
        PlayerService sut = BuildService();
        sut.LoadPlaylist(new List<TrackDto> { BuildTrack(1), BuildTrack(2) });
        RaiseMediaAboutToEnd();
        CancellationToken token = await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        // Act
        sut.Pause();

        // Assert
        Assert.True(token.IsCancellationRequested);
    }

    [Fact(DisplayName = "end_of_the_outgoing_track_during_a_crossfade_is_ignored_even_if_crossfade_was_switched_off")]
    public async Task MediaEnded_DuringCrossfade_IsIgnored()
    {
        // Arrange
        SetEnginePosition(position: 95, length: 100);
        SetCrossfadeDelay(5);
        TaskCompletionSource<CancellationToken> entered = KeepCrossfadeInFlight();
        PlayerService sut = BuildService();
        sut.LoadPlaylist(new List<TrackDto> { BuildTrack(1), BuildTrack(2) });
        RaiseMediaAboutToEnd();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        _appOptions.SetupGet(o => o.CrossFade).Returns(false);
        _engine.Invocations.Clear();

        // Act
        _engine.Raise(m => m.OnMediaEnded += null, this, EventArgs.Empty);

        // Assert
        _engine.Verify(o => o.SetTrack(It.IsAny<TrackDto>(), It.IsAny<float>()), Times.Never);
    }

    private void SetupCrossfadeResult(Task<bool> result) => _engine.Setup(o => o.CrossfadeToAsync(It.IsAny<TrackDto>(), It.IsAny<float>(), It.IsAny<double>(), It.IsAny<CancellationToken>())).Returns(result);

    private void RaiseMediaEnded() => _engine.Raise(m => m.OnMediaEnded += null, this, EventArgs.Empty);

    private void VerifySetTrack(long id, Times times) => _engine.Verify(o => o.SetTrack(It.Is<TrackDto>(t => t.Id == id), It.IsAny<float>()), times);

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(2);

        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(10);
    }

    [Fact(DisplayName = "crossfade_failure_keeps_the_current_track")]
    public void Crossfade_failure_keeps_the_current_track()
    {
        // Arrange
        SetEnginePosition(position: 95, length: 100);
        SetCrossfadeDelay(5);
        SetupCrossfadeResult(Task.FromResult(false));
        Messenger messenger = new();
        int mediaChanged = 0;
        using IDisposable subscription = messenger.Subscribe<MediaChangedMessage>(_ => mediaChanged++);
        PlayerService sut = new(_callDetection.Object, _engine.Object, _appOptions.Object, null, null, _albumPicture.Object, TimeProvider.System, messenger, Mock.Of<IMixCueProvider>(), _logger.Object);
        sut.LoadPlaylist(new List<TrackDto> { BuildTrack(1), BuildTrack(2) });
        mediaChanged = 0;
        _engine.Invocations.Clear();

        // Act
        RaiseMediaAboutToEnd();

        // Assert
        Assert.Equal(1, sut.CurrentTrack?.Id);
        Assert.Equal(2, sut.GetQueue()[0].Id);
        Assert.Equal(0, mediaChanged);
        VerifySetTrack(2, Times.Never());
    }

    [Fact(DisplayName = "crossfade_failure_falls_back_to_next_at_the_end_of_the_track")]
    public void Crossfade_failure_falls_back_to_next_at_the_end_of_the_track()
    {
        // Arrange
        SetEnginePosition(position: 95, length: 100);
        SetCrossfadeDelay(5);
        SetupCrossfadeResult(Task.FromResult(false));
        PlayerService sut = BuildService();
        sut.LoadPlaylist(new List<TrackDto> { BuildTrack(1), BuildTrack(2) });
        RaiseMediaAboutToEnd();
        _engine.Invocations.Clear();

        // Act
        RaiseMediaEnded();

        // Assert
        VerifySetTrack(2, Times.Once());
        Assert.Equal(2, sut.CurrentTrack?.Id);
    }

    [Fact(DisplayName = "outgoing_end_during_a_failed_crossfade_still_advances")]
    public async Task Outgoing_end_during_a_failed_crossfade_still_advances()
    {
        // Arrange
        SetEnginePosition(position: 95, length: 100);
        SetCrossfadeDelay(5);
        TaskCompletionSource<bool> attempt = new();
        SetupCrossfadeResult(attempt.Task);
        PlayerService sut = BuildService();
        sut.LoadPlaylist(new List<TrackDto> { BuildTrack(1), BuildTrack(2) });
        RaiseMediaAboutToEnd();
        _engine.Invocations.Clear();
        RaiseMediaEnded();
        VerifySetTrack(2, Times.Never());

        // Act
        attempt.SetResult(false);
        await WaitUntilAsync(() => sut.CurrentTrack?.Id == 2);

        // Assert
        VerifySetTrack(2, Times.Once());
        Assert.Equal(2, sut.CurrentTrack?.Id);
    }

    [Fact(DisplayName = "failed_crossfade_fallback_skips_unreadable_next_track")]
    public void Failed_crossfade_fallback_skips_unreadable_next_track()
    {
        // Arrange
        SetEnginePosition(position: 95, length: 100);
        SetCrossfadeDelay(5);
        SetupCrossfadeResult(Task.FromResult(false));
        _engine.Setup(o => o.SetTrack(It.Is<TrackDto>(t => t.Id == 2), It.IsAny<float>())).Returns(false);
        PlayerService sut = BuildService();
        sut.LoadPlaylist(new List<TrackDto> { BuildTrack(1), BuildTrack(2), BuildTrack(3) });
        RaiseMediaAboutToEnd();

        // Act
        RaiseMediaEnded();

        // Assert
        VerifySetTrack(2, Times.Once());
        VerifySetTrack(3, Times.Once());
        _engine.Verify(o => o.CrossfadeToAsync(It.IsAny<TrackDto>(), It.IsAny<float>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(3, sut.CurrentTrack?.Id);
    }

    [Fact(DisplayName = "cancelled_failed_crossfade_does_not_call_next")]
    public async Task Cancelled_failed_crossfade_does_not_call_next()
    {
        // Arrange
        SetEnginePosition(position: 95, length: 100);
        SetCrossfadeDelay(5);
        TaskCompletionSource<bool> attempt = new();
        SetupCrossfadeResult(attempt.Task);
        PlayerService sut = BuildService();
        sut.LoadPlaylist(new List<TrackDto> { BuildTrack(1), BuildTrack(2) });
        RaiseMediaAboutToEnd();
        RaiseMediaEnded();
        _engine.Invocations.Clear();

        // Act
        sut.Pause();
        attempt.SetResult(false);
        await Task.Delay(100);

        // Assert
        VerifySetTrack(2, Times.Never());
        Assert.Equal(1, sut.CurrentTrack?.Id);
    }

    [Fact(DisplayName = "stopped_failed_crossfade_does_not_call_next")]
    public async Task Stopped_failed_crossfade_does_not_call_next()
    {
        // Arrange
        SetEnginePosition(position: 95, length: 100);
        SetCrossfadeDelay(5);
        TaskCompletionSource<bool> attempt = new();
        SetupCrossfadeResult(attempt.Task);
        PlayerService sut = BuildService();
        sut.LoadPlaylist(new List<TrackDto> { BuildTrack(1), BuildTrack(2) });
        RaiseMediaAboutToEnd();
        RaiseMediaEnded();
        _engine.Invocations.Clear();

        // Act
        sut.Stop(true);
        attempt.SetResult(false);
        await Task.Delay(100);

        // Assert
        VerifySetTrack(2, Times.Never());
        Assert.Equal(1, sut.CurrentTrack?.Id);
    }
}