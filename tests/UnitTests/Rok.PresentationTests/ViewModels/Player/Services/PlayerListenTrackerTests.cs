using MiF.Mediator.Interfaces;
using Moq;
using Rok.Application.Features.Albums.Command;
using Rok.Application.Features.Artists.Command;
using Rok.Application.Features.Genres.Command;
using Rok.Application.Features.ListeningEvents.Command;
using Rok.Application.Features.Tracks.Command;
using Rok.ViewModels.Player.Services;

namespace Rok.PresentationTests.ViewModels.Player.Services;

public class PlayerListenTrackerTests
{
    private readonly Mock<IMediator> _mediator = new();

    private PlayerListenTracker BuildTracker() => new(_mediator.Object);

    [Fact(DisplayName = "UpdateTrackListenAsync should send the command on first call for a track")]
    public async Task UpdateTrackListenAsync_ShouldSendCommand_OnFirstCall()
    {
        // Arrange
        PlayerListenTracker sut = BuildTracker();

        // Act
        await sut.UpdateTrackListenAsync(42);

        // Assert
        _mediator.Verify(m => m.SendMessageAsync(It.Is<UpdateTrackLastListenCommand>(c => c.TrackId == 42), It.IsAny<CancellationToken>()), Times.Once);
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
        _mediator.Verify(m => m.SendMessageAsync(It.IsAny<UpdateTrackLastListenCommand>(), It.IsAny<CancellationToken>()), Times.Once);
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
        _mediator.Verify(m => m.SendMessageAsync(It.Is<UpdateArtistLastListenCommand>(c => c.Id == 7), It.IsAny<CancellationToken>()), Times.Once);
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
        _mediator.Verify(m => m.SendMessageAsync(It.Is<UpdateAlbumLastListenCommand>(c => c.Id == 11), It.IsAny<CancellationToken>()), Times.Once);
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
        _mediator.Verify(m => m.SendMessageAsync(It.Is<UpdateGenretLastListenCommand>(c => c.Id == 3), It.IsAny<CancellationToken>()), Times.Once);
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
        _mediator.Verify(m => m.SendMessageAsync(It.IsAny<UpdateTrackLastListenCommand>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        _mediator.Verify(m => m.SendMessageAsync(It.IsAny<UpdateAlbumLastListenCommand>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        _mediator.Verify(m => m.SendMessageAsync(It.IsAny<UpdateArtistLastListenCommand>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        _mediator.Verify(m => m.SendMessageAsync(It.IsAny<UpdateGenretLastListenCommand>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact(DisplayName = "UpdateListeningEventsAsync should always send the command with the supplied fields")]
    public async Task UpdateListeningEventsAsync_ShouldSendCommandWithFields()
    {
        // Arrange
        PlayerListenTracker sut = BuildTracker();

        // Act
        await sut.UpdateListeningEventsAsync(trackId: 1, artistId: 2, albumId: 3, genreId: 4, durationPlayed: 100, durationTotal: 200);

        // Assert
        _mediator.Verify(m => m.SendMessageAsync(
            It.Is<CreateListeningEventCommand>(c =>
                c.TrackId == 1 && c.ArtistId == 2 && c.AlbumId == 3 && c.GenreId == 4 &&
                c.DurationPlayed == 100 && c.DurationTotal == 200),
            It.IsAny<CancellationToken>()), Times.Once);
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
        _mediator.Verify(m => m.SendMessageAsync(It.IsAny<CreateListeningEventCommand>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
