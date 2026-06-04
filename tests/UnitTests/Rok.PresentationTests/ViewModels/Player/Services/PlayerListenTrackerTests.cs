using Rok.Application.Features.Albums.Requests;
using Rok.Application.Features.Artists.Requests;
using Rok.Application.Features.Genres.Requests;
using Rok.Application.Features.ListeningEvents.Requests;
using Rok.Application.Features.Tracks.Requests;
using Rok.ViewModels.Player.Services;

namespace Rok.PresentationTests.ViewModels.Player.Services;

public class PlayerListenTrackerTests
{
    private readonly FakeMediator _mediator = new();

    private PlayerListenTracker BuildTracker() => new(_mediator);

    [Fact(DisplayName = "UpdateTrackListenAsync should send the command on first call for a track")]
    public async Task UpdateTrackListenAsync_ShouldSendCommand_OnFirstCall()
    {
        // Arrange
        PlayerListenTracker sut = BuildTracker();

        // Act
        await sut.UpdateTrackListenAsync(42);

        // Assert
        UpdateTrackLastListenRequest sent = Assert.Single(_mediator.Sent<UpdateTrackLastListenRequest>());
        Assert.Equal(42, sent.TrackId);
    }

    [Fact(DisplayName = "UpdateTrackListenAsync should skip the command on a subsequent call for the same track")]
    public async Task UpdateTrackListenAsync_ShouldSkip_OnSubsequentCallForSameTrack()
    {
        // Arrange
        PlayerListenTracker sut = BuildTracker();

        // Act
        await sut.UpdateTrackListenAsync(42);
        await sut.UpdateTrackListenAsync(42);

        // Assert
        Assert.Single(_mediator.Sent<UpdateTrackLastListenRequest>());
    }

    [Fact(DisplayName = "UpdateArtistListenAsync should send the command on first call for an artist")]
    public async Task UpdateArtistListenAsync_ShouldSendCommand_OnFirstCall()
    {
        // Arrange
        PlayerListenTracker sut = BuildTracker();

        // Act
        await sut.UpdateArtistListenAsync(7);
        await sut.UpdateArtistListenAsync(7);

        // Assert
        UpdateArtistLastListenRequest sent = Assert.Single(_mediator.Sent<UpdateArtistLastListenRequest>());
        Assert.Equal(7, sent.Id);
    }

    [Fact(DisplayName = "UpdateAlbumListenAsync should send the command on first call for an album")]
    public async Task UpdateAlbumListenAsync_ShouldSendCommand_OnFirstCall()
    {
        // Arrange
        PlayerListenTracker sut = BuildTracker();

        // Act
        await sut.UpdateAlbumListenAsync(11);
        await sut.UpdateAlbumListenAsync(11);

        // Assert
        UpdateAlbumLastListenRequest sent = Assert.Single(_mediator.Sent<UpdateAlbumLastListenRequest>());
        Assert.Equal(11, sent.Id);
    }

    [Fact(DisplayName = "UpdateGenreListenAsync should send the command on first call for a genre")]
    public async Task UpdateGenreListenAsync_ShouldSendCommand_OnFirstCall()
    {
        // Arrange
        PlayerListenTracker sut = BuildTracker();

        // Act
        await sut.UpdateGenreListenAsync(3);
        await sut.UpdateGenreListenAsync(3);

        // Assert
        UpdateGenretLastListenRequest sent = Assert.Single(_mediator.Sent<UpdateGenretLastListenRequest>());
        Assert.Equal(3, sent.Id);
    }

    [Fact(DisplayName = "ClearCache should reset all caches and allow re-sending commands")]
    public async Task ClearCache_ShouldAllowResendingCommands()
    {
        // Arrange
        PlayerListenTracker sut = BuildTracker();
        await sut.UpdateTrackListenAsync(42);
        await sut.UpdateAlbumListenAsync(11);
        await sut.UpdateArtistListenAsync(7);
        await sut.UpdateGenreListenAsync(3);

        // Act
        sut.ClearCache();
        await sut.UpdateTrackListenAsync(42);
        await sut.UpdateAlbumListenAsync(11);
        await sut.UpdateArtistListenAsync(7);
        await sut.UpdateGenreListenAsync(3);

        // Assert — each command sent twice (before and after clear)
        Assert.Equal(2, _mediator.Sent<UpdateTrackLastListenRequest>().Count);
        Assert.Equal(2, _mediator.Sent<UpdateAlbumLastListenRequest>().Count);
        Assert.Equal(2, _mediator.Sent<UpdateArtistLastListenRequest>().Count);
        Assert.Equal(2, _mediator.Sent<UpdateGenretLastListenRequest>().Count);
    }

    [Fact(DisplayName = "UpdateListeningEventsAsync should always send the command with the supplied fields")]
    public async Task UpdateListeningEventsAsync_ShouldSendCommandWithFields()
    {
        // Arrange
        PlayerListenTracker sut = BuildTracker();

        // Act
        await sut.UpdateListeningEventsAsync(trackId: 1, artistId: 2, albumId: 3, genreId: 4, durationPlayed: 100, durationTotal: 200);

        // Assert
        CreateListeningEventRequest sent = Assert.Single(_mediator.Sent<CreateListeningEventRequest>());
        Assert.Equal(1, sent.TrackId);
        Assert.Equal(2, sent.ArtistId);
        Assert.Equal(3, sent.AlbumId);
        Assert.Equal(4, sent.GenreId);
        Assert.Equal(100, sent.DurationPlayed);
        Assert.Equal(200, sent.DurationTotal);
    }

    [Fact(DisplayName = "UpdateListeningEventsAsync should send the command even if called twice for the same track")]
    public async Task UpdateListeningEventsAsync_ShouldNotDeduplicate()
    {
        // Arrange
        PlayerListenTracker sut = BuildTracker();

        // Act
        await sut.UpdateListeningEventsAsync(1, null, null, null, 50, 100);
        await sut.UpdateListeningEventsAsync(1, null, null, null, 50, 100);

        // Assert
        Assert.Equal(2, _mediator.Sent<CreateListeningEventRequest>().Count);
    }

    [Theory(DisplayName = "SessionListenedCount should increment only when at least half of the track was played")]
    [InlineData(100, 200, 1)]
    [InlineData(199, 200, 1)]
    [InlineData(200, 200, 1)]
    [InlineData(99, 200, 0)]
    [InlineData(0, 200, 0)]
    public async Task SessionListenedCount_ShouldIncrement_WhenHalfOfTrackPlayed(long durationPlayed, long durationTotal, int expectedCount)
    {
        // Arrange
        PlayerListenTracker sut = BuildTracker();

        // Act
        await sut.UpdateListeningEventsAsync(1, null, null, null, durationPlayed, durationTotal);

        // Assert
        Assert.Equal(expectedCount, sut.SessionListenedCount);
    }

    [Fact(DisplayName = "SessionListenedCount should not increment when the track duration is unknown")]
    public async Task SessionListenedCount_ShouldNotIncrement_WhenDurationTotalIsZero()
    {
        // Arrange
        PlayerListenTracker sut = BuildTracker();

        // Act
        await sut.UpdateListeningEventsAsync(1, null, null, null, 50, 0);

        // Assert
        Assert.Equal(0, sut.SessionListenedCount);
    }

    [Fact(DisplayName = "SessionListenedCount should accumulate across tracks and survive ClearCache")]
    public async Task SessionListenedCount_ShouldSurviveClearCache()
    {
        // Arrange
        PlayerListenTracker sut = BuildTracker();
        await sut.UpdateListeningEventsAsync(1, null, null, null, 100, 100);

        // Act
        sut.ClearCache();
        await sut.UpdateListeningEventsAsync(2, null, null, null, 100, 100);

        // Assert
        Assert.Equal(2, sut.SessionListenedCount);
    }
}