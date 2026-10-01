using Microsoft.Extensions.Logging;
using Moq;
using Rok.Application.Dto;
using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Pictures;
using Rok.Application.Player;

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
        _engine.Setup(o => o.CrossfadeToAsync(It.IsAny<TrackDto>(), It.IsAny<float>(), It.IsAny<double>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _appOptions.SetupGet(o => o.CrossFade).Returns(true);
    }

    private PlayerService BuildService() => new(_callDetection.Object, _engine.Object, _appOptions.Object, null, null, _albumPicture.Object, TimeProvider.System, new Messenger(), _logger.Object);

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
               .Returns<TrackDto, float, double, CancellationToken>((t, g, d, ct) => { crossfadeCompleted.SetResult(true); return Task.CompletedTask; });
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
               .Returns<TrackDto, float, double, CancellationToken>((_, _, _, token) =>
               {
                   entered.TrySetResult(token);
                   return Task.Delay(Timeout.Infinite, token);
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
}