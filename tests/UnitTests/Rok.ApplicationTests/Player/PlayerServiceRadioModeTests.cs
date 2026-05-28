using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Dto;
using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Pictures;
using Rok.Application.Player;

namespace Rok.ApplicationTests.Player;

public class PlayerServiceRadioModeTests
{
    private static (PlayerService Service, Mock<IPlayerEngine> Engine) CreateService()
    {
        Mock<IPlayerEngine> engine = new();
        Mock<IAppOptions> appOptions = new();
        Mock<ICallDetectionService> callDetection = new();
        Mock<IAlbumPicture> albumPicture = new();

        appOptions.SetupGet(o => o.CrossFade).Returns(false);

        PlayerService service = new(
            callDetection.Object,
            engine.Object,
            appOptions.Object,
            discordService: null,
            smtcService: null,
            albumPicture: albumPicture.Object,
            timeProvider: TimeProvider.System,
            messenger: new Messenger(),
            logger: NullLogger<PlayerService>.Instance);

        return (service, engine);
    }

    [Fact(DisplayName = "PlayRadioStation should switch mode to Radio and clear the playlist")]
    public void PlayRadioStation_ShouldSwitchToRadio_AndClearPlaylist()
    {
        // Arrange
        (PlayerService service, Mock<IPlayerEngine> engine) = CreateService();
        engine.Setup(e => e.SetStream(It.IsAny<RadioStationDto>())).Returns(true);
        service.LoadPlaylist([new TrackDto { Id = 1, Title = "T", MusicFile = "C:\\tmp.mp3" }], null);
        RadioStationDto station = new(0, "Nova", "http://stream/nova.mp3", null, DateTime.UtcNow, null);

        // Act
        service.PlayRadioStation(station);

        // Assert
        Assert.Equal(EPlaybackMode.Radio, service.Mode);
        Assert.Empty(service.Playlist);
        Assert.Null(service.CurrentTrack);
        Assert.Equal(station, service.CurrentStation);
        engine.Verify(e => e.SetStream(station), Times.Once);
    }

    [Fact(DisplayName = "Next should be a no-op when in radio mode")]
    public void Next_ShouldBeNoOp_WhenInRadioMode()
    {
        // Arrange
        (PlayerService service, Mock<IPlayerEngine> engine) = CreateService();
        engine.Setup(e => e.SetStream(It.IsAny<RadioStationDto>())).Returns(true);
        service.PlayRadioStation(new RadioStationDto(0, "N", "http://s/", null, DateTime.UtcNow, null));

        // Act
        service.Next();

        // Assert
        Assert.Equal(EPlaybackMode.Radio, service.Mode);
        Assert.False(service.CanNext);
    }

    [Fact(DisplayName = "Starting music should stop active radio and switch mode to Music")]
    public void Start_ShouldStopRadio_AndSwitchToMusic()
    {
        // Arrange
        (PlayerService service, Mock<IPlayerEngine> engine) = CreateService();
        engine.Setup(e => e.SetStream(It.IsAny<RadioStationDto>())).Returns(true);
        engine.Setup(e => e.SetTrack(It.IsAny<TrackDto>())).Returns(true);
        service.PlayRadioStation(new RadioStationDto(0, "N", "http://s/", null, DateTime.UtcNow, null));

        // Act
        service.LoadPlaylist([new TrackDto { Id = 1, Title = "T", MusicFile = "C:\\tmp.mp3" }], null);
        service.Start();

        // Assert
        Assert.Equal(EPlaybackMode.Music, service.Mode);
        Assert.Null(service.CurrentStation);
    }

    [Fact(DisplayName = "Position setter should be a no-op when in radio mode")]
    public void Position_ShouldBeNoOp_WhenInRadioMode()
    {
        // Arrange
        (PlayerService service, Mock<IPlayerEngine> engine) = CreateService();
        engine.Setup(e => e.SetStream(It.IsAny<RadioStationDto>())).Returns(true);
        service.PlayRadioStation(new RadioStationDto(0, "N", "http://s/", null, DateTime.UtcNow, null));

        // Act
        service.Position = 42;

        // Assert
        engine.Verify(e => e.SetPosition(It.IsAny<double>()), Times.Never);
    }
}
