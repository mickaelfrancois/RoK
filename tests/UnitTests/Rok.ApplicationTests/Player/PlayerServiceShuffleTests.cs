using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Pictures;
using Rok.Application.Messages;
using Rok.Application.Player;
using Rok.Application.Player.Mix;

namespace Rok.ApplicationTests.Player;

public class PlayerServiceShuffleTests
{
    private readonly Mock<IPlayerEngine> _engine = new();
    private readonly Mock<IAppOptions> _appOptions = new();
    private readonly Mock<ICallDetectionService> _callDetection = new();
    private readonly Mock<IAlbumPicture> _albumPicture = new();
    private readonly Messenger _messenger = new();

    public PlayerServiceShuffleTests()
    {
        _engine.Setup(o => o.SetTrack(It.IsAny<TrackDto>(), It.IsAny<float>())).Returns(true);
        _engine.SetupGet(o => o.Length).Returns(100);
        _appOptions.SetupGet(o => o.CrossFade).Returns(false);
        _appOptions.SetupGet(o => o.CrossfadeDurationSeconds).Returns(5);
    }

    private PlayerService BuildService() => new(
        _callDetection.Object,
        _engine.Object,
        _appOptions.Object,
        discordService: null,
        smtcService: null,
        albumPicture: _albumPicture.Object,
        timeProvider: TimeProvider.System,
        messenger: _messenger,
        mixCues: Mock.Of<IMixCueProvider>(),
        logger: NullLogger<PlayerService>.Instance);

    private static TrackDto BuildTrack(long id, string artist = "artist") => new() { Id = id, Title = $"t{id}", ArtistName = artist, AlbumName = "album" };

    private static List<TrackDto> BuildTracks(int count) => [.. Enumerable.Range(1, count).Select(i => BuildTrack(i, $"artist{i % 4}"))];

    private static List<long> Ids(IEnumerable<TrackDto> tracks) => [.. tracks.Select(t => t.Id)];

    private PlayerService BuildPlayingAt(List<TrackDto> tracks, int currentIndex)
    {
        PlayerService sut = BuildService();
        sut.LoadPlaylist(tracks, tracks[currentIndex]);

        return sut;
    }

    [Fact(DisplayName = "when_shuffle_is_enabled_then_disabled_the_original_order_is_restored_without_reloading_the_track")]
    public void Enabling_then_disabling_shuffle_restores_the_original_order()
    {
        // Arrange
        List<TrackDto> tracks = BuildTracks(20);
        PlayerService sut = BuildPlayingAt([.. tracks], 5);
        TrackDto current = sut.CurrentTrack!;
        _engine.Invocations.Clear();

        // Act
        sut.IsShuffleEnabled = true;
        sut.IsShuffleEnabled = false;

        // Assert
        Assert.Equal(Ids(tracks), Ids(sut.Playlist));
        Assert.Same(current, sut.CurrentTrack);
        Assert.Equal(Ids(tracks.Skip(6)), Ids(sut.GetQueue()));
        _engine.Verify(o => o.SetTrack(It.IsAny<TrackDto>(), It.IsAny<float>()), Times.Never);
    }

    [Fact(DisplayName = "when_shuffle_is_enabled_played_and_current_tracks_stay_and_upcoming_tracks_are_the_same_set")]
    public void Enabling_shuffle_keeps_past_and_current_tracks()
    {
        // Arrange
        List<TrackDto> tracks = BuildTracks(20);
        PlayerService sut = BuildPlayingAt([.. tracks], 5);
        List<ShuffleModeChanged> shuffleMessages = [];
        int playlistMessages = 0;
        using IDisposable shuffleSubscription = _messenger.Subscribe<ShuffleModeChanged>(shuffleMessages.Add);
        using IDisposable playlistSubscription = _messenger.Subscribe<PlaylistChanged>(_ => playlistMessages++);

        // Act
        sut.IsShuffleEnabled = true;

        // Assert
        Assert.Equal(Ids(tracks.Take(6)), Ids(sut.Playlist.Take(6)));
        Assert.Equal(Ids(tracks.Skip(6)).Order(), Ids(sut.GetQueue()).Order());
        Assert.True(sut.IsShuffleEnabled);
        Assert.True(Assert.Single(shuffleMessages).IsEnabled);
        Assert.Equal(1, playlistMessages);
    }

    [Fact(DisplayName = "when_shuffle_is_set_to_its_current_value_nothing_is_sent")]
    public void Setting_shuffle_to_its_current_value_sends_nothing()
    {
        // Arrange
        PlayerService sut = BuildService();
        List<ShuffleModeChanged> messages = [];
        using IDisposable subscription = _messenger.Subscribe<ShuffleModeChanged>(messages.Add);

        // Act
        sut.IsShuffleEnabled = false;

        // Assert
        Assert.Empty(messages);
    }

    [Fact(DisplayName = "when_tracks_are_added_under_shuffle_they_end_the_restored_order")]
    public void Tracks_added_under_shuffle_land_at_the_end_of_the_original_order()
    {
        // Arrange
        List<TrackDto> tracks = BuildTracks(10);
        PlayerService sut = BuildPlayingAt([.. tracks], 2);
        sut.IsShuffleEnabled = true;
        List<TrackDto> added = [BuildTrack(100), BuildTrack(101)];

        // Act
        sut.AddTracksToPlaylist(added);
        sut.IsShuffleEnabled = false;

        // Assert
        Assert.Equal([.. Ids(tracks), 100, 101], Ids(sut.Playlist));
    }

    [Fact(DisplayName = "when_tracks_are_inserted_after_the_current_one_under_shuffle_they_follow_it_in_the_restored_order")]
    public void Tracks_inserted_under_shuffle_follow_the_current_track_when_restored()
    {
        // Arrange
        List<TrackDto> tracks = BuildTracks(10);
        PlayerService sut = BuildPlayingAt([.. tracks], 2);
        sut.IsShuffleEnabled = true;

        // Act
        sut.InsertTracksToPlaylist([BuildTrack(100)]);
        sut.IsShuffleEnabled = false;

        // Assert
        List<long> expected = [.. Ids(tracks.Take(3)), 100, .. Ids(tracks.Skip(3))];
        Assert.Equal(expected, Ids(sut.Playlist));
    }

    [Fact(DisplayName = "when_upcoming_tracks_are_removed_under_shuffle_they_stay_absent_after_restore")]
    public void Removed_tracks_stay_absent_after_restore()
    {
        // Arrange
        List<TrackDto> tracks = BuildTracks(12);
        PlayerService sut = BuildPlayingAt([.. tracks], 0);
        sut.IsShuffleEnabled = true;

        // Act
        int removed = sut.RemoveUpcomingByTrack(7);
        sut.IsShuffleEnabled = false;

        // Assert
        Assert.Equal(1, removed);
        Assert.Equal(Ids(tracks.Where(t => t.Id != 7)), Ids(sut.Playlist));
    }

    [Fact(DisplayName = "when_a_playlist_is_loaded_under_shuffle_the_start_track_leads_and_the_caller_list_is_untouched")]
    public void Loading_a_playlist_under_shuffle_starts_on_the_start_track()
    {
        // Arrange
        List<TrackDto> tracks = BuildTracks(15);
        List<TrackDto> callerList = [.. tracks];
        PlayerService sut = BuildService();
        sut.IsShuffleEnabled = true;

        // Act
        sut.LoadPlaylist(callerList, tracks[9]);

        // Assert
        Assert.Same(tracks[9], sut.Playlist[0]);
        Assert.Equal(tracks[9].Id, sut.CurrentTrack!.Id);
        Assert.Equal(Ids(tracks), Ids(callerList));
        Assert.NotSame(callerList, sut.Playlist);

        sut.IsShuffleEnabled = false;

        Assert.Equal(Ids(tracks), Ids(sut.Playlist));
    }

    [Fact(DisplayName = "when_shuffle_is_off_loading_a_playlist_keeps_the_caller_list_reference")]
    public void Loading_a_playlist_without_shuffle_keeps_the_list_reference()
    {
        // Arrange
        List<TrackDto> tracks = BuildTracks(5);
        PlayerService sut = BuildService();

        // Act
        sut.LoadPlaylist(tracks);

        // Assert
        Assert.Same(tracks, sut.Playlist);
    }

    [Fact(DisplayName = "when_a_one_off_shuffle_runs_under_shuffle_the_pre_shuffle_order_is_still_restored")]
    public void One_off_shuffle_does_not_replace_the_original_order()
    {
        // Arrange
        List<TrackDto> tracks = BuildTracks(15);
        PlayerService sut = BuildPlayingAt([.. tracks], 1);
        sut.IsShuffleEnabled = true;

        // Act
        sut.ShuffleTracks();
        sut.IsShuffleEnabled = false;

        // Assert
        Assert.Equal(Ids(tracks), Ids(sut.Playlist));
    }

    [Fact(DisplayName = "when_the_queue_is_empty_toggling_shuffle_only_flips_the_flag")]
    public void Toggling_shuffle_on_an_empty_queue_is_harmless()
    {
        // Arrange
        PlayerService sut = BuildService();

        // Act
        sut.IsShuffleEnabled = true;

        // Assert
        Assert.True(sut.IsShuffleEnabled);
        Assert.Empty(sut.Playlist);

        sut.IsShuffleEnabled = false;

        Assert.False(sut.IsShuffleEnabled);
        Assert.Empty(sut.Playlist);
    }

    [Fact(DisplayName = "when_a_radio_station_plays_the_shuffle_flag_is_kept")]
    public void Playing_a_radio_station_keeps_the_shuffle_flag()
    {
        // Arrange
        PlayerService sut = BuildPlayingAt(BuildTracks(5), 0);
        sut.IsShuffleEnabled = true;
        _engine.Setup(o => o.SetStream(It.IsAny<RadioStationDto>())).Returns(true);
        RadioStationDto station = new(Id: 0, Name: "Nova", StreamUrl: "http://stream/nova.mp3", HomepageUrl: null, StationUuid: null, FaviconUrl: null, CountryCode: null, Codec: null, Bitrate: null, AddedAt: DateTime.UtcNow, LastListen: null);

        // Act
        sut.PlayRadioStation(station);

        // Assert
        Assert.True(sut.IsShuffleEnabled);
        Assert.Empty(sut.Playlist);
    }

    [Fact(DisplayName = "when_shuffle_changes_the_imminent_track_the_queued_gapless_track_is_cleared")]
    public void Changing_the_imminent_track_through_shuffle_clears_the_queued_gapless_track()
    {
        // Arrange
        _engine.Setup(o => o.QueueNextTrack(It.IsAny<TrackDto>(), It.IsAny<float>())).Returns(true);
        _engine.SetupGet(o => o.Position).Returns(90);
        List<TrackDto> tracks = BuildTracks(40);
        PlayerService sut = BuildPlayingAt([.. tracks], 0);
        _engine.Raise(m => m.OnMediaAboutToEnd += null, _engine.Object, EventArgs.Empty);
        _engine.Invocations.Clear();

        // Act
        sut.IsShuffleEnabled = true;

        // Assert
        bool imminentChanged = sut.GetQueue()[0].Id != 2;
        _engine.Verify(o => o.ClearNextTrack(), imminentChanged ? Times.Once() : Times.Never());
    }

    [Fact(DisplayName = "when_the_service_is_built_the_shuffle_state_is_read_from_the_options")]
    public void Building_the_service_reads_the_shuffle_state_from_the_options()
    {
        // Arrange
        _appOptions.SetupProperty(o => o.ShuffleEnabled, true);

        // Act
        PlayerService sut = BuildService();

        // Assert
        Assert.True(sut.IsShuffleEnabled);
    }

    [Fact(DisplayName = "when_the_shuffle_state_changes_it_is_written_back_to_the_options")]
    public void Changing_the_shuffle_state_writes_it_back_to_the_options()
    {
        // Arrange
        PlayerService sut = BuildService();

        // Act
        sut.IsShuffleEnabled = true;

        // Assert
        _appOptions.VerifySet(o => o.ShuffleEnabled = true, Times.Once);
    }

    [Fact(DisplayName = "when_the_service_starts_with_shuffle_enabled_a_loaded_playlist_is_shuffled_and_restorable")]
    public void Starting_with_shuffle_enabled_shuffles_the_loaded_playlist_and_restores_it()
    {
        // Arrange
        _appOptions.SetupProperty(o => o.ShuffleEnabled, true);
        List<TrackDto> tracks = BuildTracks(20);
        PlayerService sut = BuildService();

        // Act
        sut.LoadPlaylist([.. tracks], tracks[3]);
        sut.IsShuffleEnabled = false;

        // Assert
        Assert.Equal(Ids(tracks.Skip(4)), Ids(sut.GetQueue()));
    }
}