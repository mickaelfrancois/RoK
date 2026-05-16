using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Rok.Application.Interfaces.Pictures;
using Rok.Application.Player;

namespace Rok.ApplicationTests.Player;

public class PlayerServiceSmtcTests
{
    [Fact(DisplayName = "when_track_changes_smtc_receives_track_info_with_cover_path")]
    public void When_track_changes_smtc_receives_track_info_with_cover_path()
    {
        // Arrange
        Mock<ISystemMediaTransportControlsService> smtc = new();
        Mock<IAlbumPicture> picture = new();
        Mock<IPlayerEngine> engine = new();
        Mock<ICallDetectionService> callMock = new();
        Mock<IAppOptions> optionsMock = new();
        optionsMock.SetupGet(o => o.CrossFade).Returns(false);
        engine.Setup(o => o.SetTrack(It.IsAny<TrackDto>())).Returns(true);
        FakeTimeProvider time = new();

        const string albumDir = @"C:\Music\Album";
        const string coverFile = @"C:\Music\Album\cover.jpg";
        TrackDto track = new()
        {
            Id = 1,
            Title = "T",
            ArtistName = "A",
            AlbumName = "Alb",
            MusicFile = @"C:\Music\Album\01.mp3"
        };

        picture.Setup(p => p.PictureFileExists(albumDir)).Returns(true);
        picture.Setup(p => p.GetPictureFile(albumDir)).Returns(coverFile);

        PlayerService sut = new(
            callMock.Object,
            engine.Object,
            optionsMock.Object,
            discordService: null,
            smtcService: smtc.Object,
            albumPicture: picture.Object,
            timeProvider: time,
            messenger: new Messenger(),
            logger: NullLogger<PlayerService>.Instance);

        // Act
        sut.LoadPlaylist(new List<TrackDto> { track });

        // Assert
        smtc.Verify(s => s.UpdateTrackInfoAsync(track, coverFile), Times.AtLeastOnce);
    }

    [Fact(DisplayName = "when_track_changes_and_no_cover_file_smtc_receives_null_cover_path")]
    public void When_track_changes_and_no_cover_file_smtc_receives_null_cover_path()
    {
        // Arrange
        Mock<ISystemMediaTransportControlsService> smtc = new();
        Mock<IAlbumPicture> picture = new();
        Mock<IPlayerEngine> engine = new();
        Mock<ICallDetectionService> callMock = new();
        Mock<IAppOptions> optionsMock = new();
        optionsMock.SetupGet(o => o.CrossFade).Returns(false);
        engine.Setup(o => o.SetTrack(It.IsAny<TrackDto>())).Returns(true);
        FakeTimeProvider time = new();

        const string albumDir = @"C:\Music\Album";
        TrackDto track = new()
        {
            Id = 1,
            Title = "T",
            ArtistName = "A",
            AlbumName = "Alb",
            MusicFile = @"C:\Music\Album\01.mp3"
        };

        picture.Setup(p => p.PictureFileExists(albumDir)).Returns(false);

        PlayerService sut = new(
            callMock.Object,
            engine.Object,
            optionsMock.Object,
            discordService: null,
            smtcService: smtc.Object,
            albumPicture: picture.Object,
            timeProvider: time,
            messenger: new Messenger(),
            logger: NullLogger<PlayerService>.Instance);

        // Act
        sut.LoadPlaylist(new List<TrackDto> { track });

        // Assert
        smtc.Verify(s => s.UpdateTrackInfoAsync(track, null), Times.AtLeastOnce);
        picture.Verify(p => p.GetPictureFile(It.IsAny<string>()), Times.Never);
    }

    [Fact(DisplayName = "when_queue_ends_smtc_receives_playback_state_stopped")]
    public void When_queue_ends_smtc_receives_playback_state_stopped()
    {
        // Arrange
        Mock<ISystemMediaTransportControlsService> smtc = new();
        Mock<IPlayerEngine> engine = new();
        Mock<ICallDetectionService> callMock = new();
        Mock<IAppOptions> optionsMock = new();
        optionsMock.SetupGet(o => o.CrossFade).Returns(false);
        Mock<IAlbumPicture> picture = new();
        FakeTimeProvider time = new();

        TrackDto onlyTrack = new()
        {
            Title = "T",
            ArtistName = "A",
            AlbumName = "Alb",
            MusicFile = @"C:\Music\01.mp3"
        };

        PlayerService sut = new(
            callMock.Object, engine.Object, optionsMock.Object,
            discordService: null, smtcService: smtc.Object, albumPicture: picture.Object,
            timeProvider: time, messenger: new Messenger(), logger: NullLogger<PlayerService>.Instance);

        sut.LoadPlaylist(new List<TrackDto> { onlyTrack });
        sut.IsLoopingEnabled = false;

        smtc.Invocations.Clear();

        // Act
        sut.Next();

        // Assert
        smtc.Verify(s => s.UpdatePlaybackState(PlaybackStatus.Stopped), Times.AtLeastOnce);
    }

    [Fact(DisplayName = "when_playing_timeline_updates_at_one_hz")]
    public void When_playing_timeline_updates_at_one_hz()
    {
        // Arrange
        Mock<ISystemMediaTransportControlsService> smtc = new();
        Mock<IPlayerEngine> engine = new();
        engine.Setup(o => o.SetTrack(It.IsAny<TrackDto>())).Returns(true);
        engine.SetupGet(e => e.Position).Returns(10d);
        engine.SetupGet(e => e.Length).Returns(180d);
        Mock<ICallDetectionService> callMock = new();
        Mock<IAppOptions> optionsMock = new();
        optionsMock.SetupGet(o => o.CrossFade).Returns(false);
        Mock<IAlbumPicture> picture = new();
        FakeTimeProvider time = new();

        TrackDto track = new()
        {
            Title = "T",
            ArtistName = "A",
            AlbumName = "Alb",
            MusicFile = @"C:\Music\01.mp3",
            Duration = 180_000
        };

        PlayerService sut = new(
            callMock.Object, engine.Object, optionsMock.Object,
            discordService: null, smtcService: smtc.Object, albumPicture: picture.Object,
            timeProvider: time, messenger: new Messenger(), logger: NullLogger<PlayerService>.Instance);

        sut.LoadPlaylist(new List<TrackDto> { track });
        smtc.Invocations.Clear();

        // Act
        time.Advance(TimeSpan.FromSeconds(3.5));

        // Assert
        smtc.Verify(s => s.UpdateTimeline(It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>()), Times.Exactly(3));
    }
}
