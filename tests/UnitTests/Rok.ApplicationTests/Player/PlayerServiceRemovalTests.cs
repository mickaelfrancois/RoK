using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Dto;
using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Pictures;
using Rok.Application.Messages;
using Rok.Application.Player;

namespace Rok.ApplicationTests.Player;

public class PlayerServiceRemovalTests
{
    private readonly Mock<IPlayerEngine> _engine = new();
    private readonly Mock<IAppOptions> _appOptions = new();
    private readonly Mock<ICallDetectionService> _callDetection = new();
    private readonly Mock<IAlbumPicture> _albumPicture = new();
    private readonly Messenger _messenger = new();

    public PlayerServiceRemovalTests()
    {
        _engine.Setup(o => o.SetTrack(It.IsAny<TrackDto>())).Returns(true);
        _appOptions.SetupGet(o => o.CrossFade).Returns(false);
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
        logger: NullLogger<PlayerService>.Instance);

    private static TrackDto BuildTrack(long id, long? albumId = null, long? artistId = null, long? genreId = null)
        => new() { Id = id, Title = $"t{id}", AlbumId = albumId, ArtistId = artistId, GenreId = genreId };

    [Fact(DisplayName = "when_remove_upcoming_by_track_then_only_that_upcoming_track_is_removed")]
    public void Remove_upcoming_by_track_removes_only_that_track()
    {
        // Arrange
        TrackDto t1 = BuildTrack(1);
        TrackDto t2 = BuildTrack(2);
        TrackDto t3 = BuildTrack(3);
        PlayerService sut = BuildService();
        sut.LoadPlaylist(new List<TrackDto> { t1, t2, t3 });

        // Act
        int removed = sut.RemoveUpcomingByTrack(2);

        // Assert
        Assert.Equal(1, removed);
        Assert.Equal(new List<TrackDto> { t1, t3 }, sut.Playlist);
        Assert.Equal(t1, sut.CurrentTrack);
    }

    [Fact(DisplayName = "when_remove_upcoming_by_album_then_all_upcoming_album_tracks_are_removed_and_others_kept")]
    public void Remove_upcoming_by_album_removes_only_upcoming_album_tracks()
    {
        // Arrange
        TrackDto t1 = BuildTrack(1, albumId: 10);
        TrackDto t2 = BuildTrack(2, albumId: 10);
        TrackDto t3 = BuildTrack(3, albumId: 10);
        TrackDto t4 = BuildTrack(4, albumId: 10);
        TrackDto t5 = BuildTrack(5, albumId: 20);
        PlayerService sut = BuildService();
        sut.LoadPlaylist(new List<TrackDto> { t1, t2, t3, t4, t5 });
        sut.Next(); // current = t2 (index 1); played = [t1]; upcoming = [t3, t4, t5]

        // Act
        int removed = sut.RemoveUpcomingByAlbum(10);

        // Assert
        Assert.Equal(2, removed);
        Assert.Equal(new List<TrackDto> { t1, t2, t5 }, sut.Playlist);
        Assert.Equal(t2, sut.CurrentTrack);
        Assert.Equal(new List<TrackDto> { t5 }, sut.GetQueue());
    }

    [Fact(DisplayName = "when_remove_upcoming_by_artist_then_all_upcoming_artist_tracks_are_removed")]
    public void Remove_upcoming_by_artist_removes_all_upcoming_artist_tracks()
    {
        // Arrange
        TrackDto t1 = BuildTrack(1, artistId: 7);
        TrackDto t2 = BuildTrack(2, artistId: 7);
        TrackDto t3 = BuildTrack(3, artistId: 8);
        PlayerService sut = BuildService();
        sut.LoadPlaylist(new List<TrackDto> { t1, t2, t3 });

        // Act
        int removed = sut.RemoveUpcomingByArtist(7);

        // Assert
        Assert.Equal(1, removed);
        Assert.Equal(new List<TrackDto> { t1, t3 }, sut.Playlist);
    }

    [Fact(DisplayName = "when_remove_upcoming_by_genre_then_all_upcoming_genre_tracks_are_removed")]
    public void Remove_upcoming_by_genre_removes_all_upcoming_genre_tracks()
    {
        // Arrange
        TrackDto t1 = BuildTrack(1, genreId: 3);
        TrackDto t2 = BuildTrack(2, genreId: 3);
        TrackDto t3 = BuildTrack(3, genreId: 3);
        PlayerService sut = BuildService();
        sut.LoadPlaylist(new List<TrackDto> { t1, t2, t3 });

        // Act
        int removed = sut.RemoveUpcomingByGenre(3);

        // Assert
        Assert.Equal(2, removed);
        Assert.Equal(new List<TrackDto> { t1 }, sut.Playlist);
    }

    [Fact(DisplayName = "when_remove_by_group_then_current_track_is_never_removed")]
    public void Remove_by_group_never_removes_current_track()
    {
        // Arrange
        TrackDto t1 = BuildTrack(1, albumId: 10);
        TrackDto t2 = BuildTrack(2, albumId: 10);
        TrackDto t3 = BuildTrack(3, albumId: 10);
        PlayerService sut = BuildService();
        sut.LoadPlaylist(new List<TrackDto> { t1, t2, t3 });
        sut.Next(); // current = t2 (album 10)

        // Act
        int removed = sut.RemoveUpcomingByAlbum(10);

        // Assert
        Assert.Equal(1, removed); // only t3 (upcoming)
        Assert.Contains(t2, sut.Playlist);
        Assert.Equal(t2, sut.CurrentTrack);
    }

    [Fact(DisplayName = "when_remove_by_group_then_already_played_tracks_are_never_removed")]
    public void Remove_by_group_never_removes_already_played_tracks()
    {
        // Arrange
        TrackDto t1 = BuildTrack(1, albumId: 10);
        TrackDto t2 = BuildTrack(2, albumId: 10);
        TrackDto t3 = BuildTrack(3, albumId: 10);
        PlayerService sut = BuildService();
        sut.LoadPlaylist(new List<TrackDto> { t1, t2, t3 });
        sut.Next(); // current = t2; t1 already played

        // Act
        sut.RemoveUpcomingByAlbum(10);

        // Assert
        Assert.Contains(t1, sut.Playlist);
    }

    [Fact(DisplayName = "when_counting_upcoming_then_the_count_is_returned_without_mutating_the_queue")]
    public void Count_upcoming_does_not_mutate_the_queue()
    {
        // Arrange
        TrackDto t1 = BuildTrack(1, albumId: 10);
        TrackDto t2 = BuildTrack(2, albumId: 10);
        TrackDto t3 = BuildTrack(3, albumId: 10);
        PlayerService sut = BuildService();
        sut.LoadPlaylist(new List<TrackDto> { t1, t2, t3 });
        sut.Next(); // current = t2; upcoming = [t3]

        // Act
        int count = sut.CountUpcomingByAlbum(10);

        // Assert
        Assert.Equal(1, count);
        Assert.Equal(3, sut.Playlist.Count);
    }

    [Fact(DisplayName = "when_removal_removes_at_least_one_track_then_playlist_changed_is_emitted")]
    public void Effective_removal_emits_playlist_changed()
    {
        // Arrange
        TrackDto t1 = BuildTrack(1, albumId: 10);
        TrackDto t2 = BuildTrack(2, albumId: 10);
        PlayerService sut = BuildService();
        sut.LoadPlaylist(new List<TrackDto> { t1, t2 });

        int emissions = 0;
        using IDisposable subscription = _messenger.Subscribe<PlaylistChanged>(_ => emissions++);

        // Act
        sut.RemoveUpcomingByAlbum(10);

        // Assert
        Assert.Equal(1, emissions);
    }

    [Fact(DisplayName = "when_removal_removes_nothing_then_playlist_changed_is_not_emitted")]
    public void Empty_removal_does_not_emit_playlist_changed()
    {
        // Arrange
        TrackDto t1 = BuildTrack(1, albumId: 10);
        TrackDto t2 = BuildTrack(2, albumId: 10);
        PlayerService sut = BuildService();
        sut.LoadPlaylist(new List<TrackDto> { t1, t2 });

        int emissions = 0;
        using IDisposable subscription = _messenger.Subscribe<PlaylistChanged>(_ => emissions++);

        // Act
        int removed = sut.RemoveUpcomingByAlbum(999);

        // Assert
        Assert.Equal(0, removed);
        Assert.Equal(0, emissions);
    }

    [Fact(DisplayName = "when_removing_by_group_then_tracks_with_a_null_group_id_are_never_matched")]
    public void Remove_by_group_ignores_tracks_with_null_group_id()
    {
        // Arrange
        TrackDto t1 = BuildTrack(1);
        TrackDto t2 = BuildTrack(2, genreId: null);
        TrackDto t3 = BuildTrack(3, genreId: 5);
        PlayerService sut = BuildService();
        sut.LoadPlaylist(new List<TrackDto> { t1, t2, t3 });

        // Act
        int removed = sut.RemoveUpcomingByGenre(5);

        // Assert
        Assert.Equal(1, removed);
        Assert.Equal(new List<TrackDto> { t1, t2 }, sut.Playlist);
    }

    [Fact(DisplayName = "when_group_is_entirely_already_played_then_nothing_is_removed")]
    public void Remove_by_group_does_nothing_when_group_is_entirely_played()
    {
        // Arrange
        TrackDto t1 = BuildTrack(1, albumId: 10);
        TrackDto t2 = BuildTrack(2, albumId: 10);
        TrackDto t3 = BuildTrack(3, albumId: 20);
        PlayerService sut = BuildService();
        sut.LoadPlaylist(new List<TrackDto> { t1, t2, t3 });
        sut.Next();
        sut.Next(); // current = t3; album 10 entirely behind the cursor

        // Act
        int removed = sut.RemoveUpcomingByAlbum(10);

        // Assert
        Assert.Equal(0, removed);
        Assert.Equal(3, sut.Playlist.Count);
    }

    [Fact(DisplayName = "when_track_id_is_absent_then_nothing_is_removed")]
    public void Remove_upcoming_by_track_does_nothing_when_id_absent()
    {
        // Arrange
        TrackDto t1 = BuildTrack(1);
        TrackDto t2 = BuildTrack(2);
        PlayerService sut = BuildService();
        sut.LoadPlaylist(new List<TrackDto> { t1, t2 });

        // Act
        int removed = sut.RemoveUpcomingByTrack(9999);

        // Assert
        Assert.Equal(0, removed);
        Assert.Equal(2, sut.Playlist.Count);
    }

    [Fact(DisplayName = "when_queue_is_empty_then_removal_returns_zero_without_throwing")]
    public void Remove_on_empty_queue_returns_zero()
    {
        // Arrange
        PlayerService sut = BuildService();

        // Act
        int removed = sut.RemoveUpcomingByTrack(1);

        // Assert
        Assert.Equal(0, removed);
    }

    [Fact(DisplayName = "when_player_is_in_radio_mode_then_removal_is_a_no_op")]
    public void Remove_in_radio_mode_is_no_op()
    {
        // Arrange
        _engine.Setup(o => o.SetStream(It.IsAny<RadioStationDto>())).Returns(true);
        PlayerService sut = BuildService();
        sut.PlayRadioStation(new RadioStationDto(1, "Radio", "http://stream", null, null, null, null, null, null, DateTime.UtcNow, null));

        // Act
        int removed = sut.RemoveUpcomingByAlbum(10);

        // Assert
        Assert.Equal(0, removed);
    }

    [Fact(DisplayName = "when_removing_the_imminent_track_during_a_crossfade_then_the_crossfade_is_cancelled")]
    public async Task Removing_imminent_track_during_crossfade_cancels_it()
    {
        // Arrange
        _appOptions.SetupGet(o => o.CrossFade).Returns(true);
        _engine.SetupGet(o => o.Position).Returns(95);
        _engine.SetupGet(o => o.Length).Returns(100);
        _engine.SetupGet(o => o.CrossfadeDelay).Returns(5);

        TaskCompletionSource crossfadeEntered = new();
        CancellationToken capturedToken = default;

        _engine
            .Setup(o => o.CrossfadeToAsync(It.IsAny<TrackDto>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .Returns<TrackDto, double, double, CancellationToken>((_, _, _, token) =>
            {
                capturedToken = token;
                crossfadeEntered.SetResult();
                return Task.Delay(Timeout.Infinite, token); // keep the crossfade in flight until its token is cancelled
            });

        PlayerService sut = BuildService();
        sut.LoadPlaylist(new List<TrackDto> { BuildTrack(1), BuildTrack(2), BuildTrack(3) });

        _engine.Raise(m => m.OnMediaAboutToEnd += null, this, EventArgs.Empty);
        await crossfadeEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        // Act
        int removed = sut.RemoveUpcomingByTrack(2); // the imminent upcoming track (index currentIndex + 1)

        // Assert
        Assert.Equal(1, removed);
        Assert.True(capturedToken.IsCancellationRequested);
    }
}
