using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Interfaces.Pictures;
using Rok.Application.Messages;
using Rok.Application.Player;

namespace Rok.ApplicationTests.Player;

public class PlayerServiceReplayGainTests
{
    private const float Tolerance = 1e-4f;

    private readonly Mock<IPlayerEngine> _engine = new();
    private readonly Mock<IAppOptions> _appOptions = new();
    private readonly Mock<ICallDetectionService> _callDetection = new();
    private readonly Mock<IAlbumPicture> _albumPicture = new();
    private readonly Messenger _messenger = new();

    public PlayerServiceReplayGainTests()
    {
        _engine.Setup(o => o.SetTrack(It.IsAny<TrackDto>(), It.IsAny<float>())).Returns(true);
        _engine.Setup(o => o.QueueNextTrack(It.IsAny<TrackDto>(), It.IsAny<float>())).Returns(true);
        _engine.Setup(o => o.CrossfadeToAsync(It.IsAny<TrackDto>(), It.IsAny<float>(), It.IsAny<double>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _engine.SetupGet(o => o.Position).Returns(95);
        _engine.SetupGet(o => o.Length).Returns(100);
        _appOptions.SetupGet(o => o.CrossfadeDurationSeconds).Returns(5);
        _appOptions.SetupProperty(o => o.ReplayGainMode, EReplayGainMode.Track);
        _appOptions.SetupProperty(o => o.ReplayGainPreampDb, 0);
    }

    private PlayerService BuildService(bool crossfade = false)
    {
        _appOptions.SetupGet(o => o.CrossFade).Returns(crossfade);

        return new(_callDetection.Object, _engine.Object, _appOptions.Object, null, null, _albumPicture.Object, TimeProvider.System, _messenger, NullLogger<PlayerService>.Instance);
    }

    private static TrackDto BuildTrack(long id, double? trackGain = null, double? albumGain = null, long? albumId = null, int? trackNumber = null) =>
        new() { Id = id, Title = $"t{id}", AlbumId = albumId, TrackNumber = trackNumber, ReplayGainTrackGain = trackGain, ReplayGainAlbumGain = albumGain };

    private static float Linear(double db) => (float)Math.Pow(10, db / 20);

    private void RaiseMediaAboutToEnd() => _engine.Raise(m => m.OnMediaAboutToEnd += null, _engine.Object, EventArgs.Empty);

    [Fact(DisplayName = "set_track_receives_the_resolved_track_gain")]
    public void SetTrack_ReceivesResolvedTrackGain()
    {
        // Arrange
        PlayerService sut = BuildService();

        // Act
        sut.LoadPlaylist([BuildTrack(1, trackGain: -6), BuildTrack(2)]);

        // Assert
        _engine.Verify(o => o.SetTrack(It.Is<TrackDto>(t => t.Id == 1), It.Is<float>(g => Math.Abs(g - Linear(-6)) < Tolerance)), Times.Once);
    }

    [Fact(DisplayName = "set_track_receives_unity_when_replay_gain_is_off")]
    public void SetTrack_ReceivesUnity_WhenReplayGainIsOff()
    {
        // Arrange
        _appOptions.Object.ReplayGainMode = EReplayGainMode.Off;
        PlayerService sut = BuildService();

        // Act
        sut.LoadPlaylist([BuildTrack(1, trackGain: -6)]);

        // Assert
        _engine.Verify(o => o.SetTrack(It.IsAny<TrackDto>(), 1f), Times.Once);
    }

    [Fact(DisplayName = "auto_mode_gives_the_first_track_of_an_album_played_in_order_its_album_gain")]
    public void AutoMode_GivesFirstTrackOfAlbumInOrderItsAlbumGain()
    {
        // Arrange
        _appOptions.Object.ReplayGainMode = EReplayGainMode.Auto;
        PlayerService sut = BuildService();

        // Act
        sut.LoadPlaylist([BuildTrack(1, trackGain: -6, albumGain: -3, albumId: 7, trackNumber: 1), BuildTrack(2, albumId: 7, trackNumber: 2)]);

        // Assert
        _engine.Verify(o => o.SetTrack(It.Is<TrackDto>(t => t.Id == 1), It.Is<float>(g => Math.Abs(g - Linear(-3)) < Tolerance)), Times.Once);
    }

    [Fact(DisplayName = "queue_next_track_receives_the_gain_of_the_next_track")]
    public void QueueNextTrack_ReceivesGainOfNextTrack()
    {
        // Arrange
        PlayerService sut = BuildService(crossfade: false);
        sut.LoadPlaylist([BuildTrack(1, trackGain: -6), BuildTrack(2, trackGain: -9)]);

        // Act
        RaiseMediaAboutToEnd();

        // Assert
        _engine.Verify(o => o.QueueNextTrack(It.Is<TrackDto>(t => t.Id == 2), It.Is<float>(g => Math.Abs(g - Linear(-9)) < Tolerance)), Times.Once);
    }

    [Fact(DisplayName = "crossfade_receives_the_gain_of_the_incoming_track")]
    public void Crossfade_ReceivesGainOfIncomingTrack()
    {
        // Arrange
        PlayerService sut = BuildService(crossfade: true);
        sut.LoadPlaylist([BuildTrack(1, trackGain: -6), BuildTrack(2, trackGain: -2)]);

        // Act
        RaiseMediaAboutToEnd();

        // Assert
        _engine.Verify(o => o.CrossfadeToAsync(It.Is<TrackDto>(t => t.Id == 2), It.Is<float>(g => Math.Abs(g - Linear(-2)) < Tolerance), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "options_change_updates_the_gain_of_the_current_and_next_tracks")]
    public void OptionsChange_UpdatesGainOfCurrentAndNextTracks()
    {
        // Arrange
        PlayerService sut = BuildService();
        sut.LoadPlaylist([BuildTrack(1, trackGain: -6), BuildTrack(2, trackGain: -9)]);
        _appOptions.Object.ReplayGainPreampDb = 3;

        // Act
        _messenger.Send(new ReplayGainOptionsChanged());

        // Assert
        _engine.Verify(o => o.UpdateReplayGain(1, It.Is<float>(g => Math.Abs(g - Linear(-3)) < Tolerance)), Times.Once);
        _engine.Verify(o => o.UpdateReplayGain(2, It.Is<float>(g => Math.Abs(g - Linear(-6)) < Tolerance)), Times.Once);
    }

    [Fact(DisplayName = "options_change_does_nothing_without_a_current_track")]
    public void OptionsChange_DoesNothing_WithoutCurrentTrack()
    {
        // Arrange
        BuildService();

        // Act
        _messenger.Send(new ReplayGainOptionsChanged());

        // Assert
        _engine.Verify(o => o.UpdateReplayGain(It.IsAny<long>(), It.IsAny<float>()), Times.Never);
    }

    [Fact(DisplayName = "options_change_does_nothing_in_radio_mode")]
    public void OptionsChange_DoesNothing_InRadioMode()
    {
        // Arrange
        _engine.Setup(o => o.SetStream(It.IsAny<RadioStationDto>())).Returns(true);
        PlayerService sut = BuildService();
        sut.LoadPlaylist([BuildTrack(1, trackGain: -6)]);
        sut.PlayRadioStation(new RadioStationDto(Id: 0, Name: "Nova", StreamUrl: "http://stream/nova.mp3", HomepageUrl: null, StationUuid: null, FaviconUrl: null, CountryCode: null, Codec: null, Bitrate: null, AddedAt: DateTime.UtcNow, LastListen: null));

        // Act
        _messenger.Send(new ReplayGainOptionsChanged());

        // Assert
        _engine.Verify(o => o.UpdateReplayGain(It.IsAny<long>(), It.IsAny<float>()), Times.Never);
    }

    [Fact(DisplayName = "options_change_is_ignored_after_dispose")]
    public void OptionsChange_IsIgnoredAfterDispose()
    {
        // Arrange
        PlayerService sut = BuildService();
        sut.LoadPlaylist([BuildTrack(1, trackGain: -6)]);
        sut.Dispose();

        // Act
        _messenger.Send(new ReplayGainOptionsChanged());

        // Assert
        _engine.Verify(o => o.UpdateReplayGain(It.IsAny<long>(), It.IsAny<float>()), Times.Never);
    }
}