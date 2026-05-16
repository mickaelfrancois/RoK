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
        _engine.Setup(o => o.SetTrack(It.IsAny<TrackDto>())).Returns(true);
        _engine.Setup(o => o.CrossfadeToAsync(It.IsAny<TrackDto>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _appOptions.SetupGet(o => o.CrossFade).Returns(true);
    }

    private PlayerService BuildService() => new(_callDetection.Object, _engine.Object, _appOptions.Object, null, null, _albumPicture.Object, TimeProvider.System, new Messenger(), _logger.Object);

    private static TrackDto BuildTrack(long id, bool isLive = false) => new() { Id = id, Title = $"t{id}", IsAlbumLive = isLive };

    private void SetEnginePosition(double position, double length)
    {
        _engine.SetupGet(o => o.Position).Returns(position);
        _engine.SetupGet(o => o.Length).Returns(length);
    }

    private void SetCrossfadeDelay(int seconds) => _engine.SetupGet(o => o.CrossfadeDelay).Returns(seconds);

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
        _engine.Verify(o => o.CrossfadeToAsync(It.Is<TrackDto>(t => t.Id == 2), 5, It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "OnMediaAboutToEnd should fall back to Next when both current and next tracks are live albums")]
    public void OnMediaAboutToEnd_ShouldFallBackToNext_WhenBothTracksAreLiveAlbums()
    {
        // Arrange
        SetEnginePosition(position: 100, length: 100);
        SetCrossfadeDelay(5);
        PlayerService sut = BuildService();
        sut.LoadPlaylist(new List<TrackDto> { BuildTrack(1, isLive: true), BuildTrack(2, isLive: true) });
        _engine.Invocations.Clear();

        // Act
        RaiseMediaAboutToEnd();

        // Assert
        _engine.Verify(o => o.CrossfadeToAsync(It.IsAny<TrackDto>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Never);
        _engine.Verify(o => o.SetTrack(It.Is<TrackDto>(t => t.Id == 2)), Times.Once);
    }

    [Fact(DisplayName = "OnMediaAboutToEnd should fall back to Next when player is muted")]
    public void OnMediaAboutToEnd_ShouldFallBackToNext_WhenMuted()
    {
        // Arrange
        SetEnginePosition(position: 100, length: 100);
        SetCrossfadeDelay(5);
        PlayerService sut = BuildService();
        sut.LoadPlaylist(new List<TrackDto> { BuildTrack(1), BuildTrack(2) });
        sut.IsMuted = true;
        _engine.Invocations.Clear();

        // Act
        RaiseMediaAboutToEnd();

        // Assert
        _engine.Verify(o => o.CrossfadeToAsync(It.IsAny<TrackDto>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Never);
        _engine.Verify(o => o.SetTrack(It.Is<TrackDto>(t => t.Id == 2)), Times.Once);
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
        _engine.Verify(o => o.CrossfadeToAsync(It.IsAny<TrackDto>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Never);
        _engine.Verify(o => o.SetTrack(It.IsAny<TrackDto>()), Times.Never);
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
        _engine.Verify(o => o.CrossfadeToAsync(It.Is<TrackDto>(t => t.Id == 1), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Once);
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
        _engine.Verify(o => o.CrossfadeToAsync(It.IsAny<TrackDto>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Never);
        _engine.Verify(o => o.SetTrack(It.Is<TrackDto>(t => t.Id == 2)), Times.Once);
    }

    [Fact(DisplayName = "Crossfade completion should advance current track and fire playing state")]
    public async Task Crossfade_OnCompletion_ShouldAdvanceCurrentTrackAndPlay()
    {
        // Arrange
        SetEnginePosition(position: 95, length: 100);
        SetCrossfadeDelay(5);
        TaskCompletionSource<bool> crossfadeCompleted = new();
        _engine.Setup(o => o.CrossfadeToAsync(It.IsAny<TrackDto>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
               .Returns<TrackDto, double, double, CancellationToken>((t, d, v, ct) => { crossfadeCompleted.SetResult(true); return Task.CompletedTask; });
        PlayerService sut = BuildService();
        sut.LoadPlaylist(new List<TrackDto> { BuildTrack(1), BuildTrack(2) });

        // Act
        RaiseMediaAboutToEnd();
        await crossfadeCompleted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await Task.Yield();

        // Assert
        Assert.Equal(2, sut.CurrentTrack?.Id);
    }
}
