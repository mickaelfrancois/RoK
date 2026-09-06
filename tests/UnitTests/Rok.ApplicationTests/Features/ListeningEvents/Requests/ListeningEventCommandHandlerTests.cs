using Moq;
using Rok.Application.Features.ListeningEvents.Requests;
using Rok.Application.Interfaces.Repositories;

namespace Rok.ApplicationTests.Features.ListeningEvents.Requests;

public class CreateListeningEventCommandHandlerTests
{
    private readonly Mock<IListeningEventRepository> _repository = new();
    private readonly Mock<ITrackRepository> _trackRepository = new();
    private readonly Mock<IArtistRepository> _artistRepository = new();
    private readonly Mock<IAlbumRepository> _albumRepository = new();
    private readonly Mock<IGenreRepository> _genreRepository = new();

    private CreateListeningEventRequestHandler CreateHandler()
    {
        return new(_repository.Object, _trackRepository.Object, _artistRepository.Object, _albumRepository.Object, _genreRepository.Object);
    }

    private void GivenTrack(long id, long? artistId = null, long? albumId = null, long? genreId = null)
    {
        TrackEntity track = new() { Id = id, ArtistId = artistId, AlbumId = albumId, GenreId = genreId };

        _trackRepository.Setup(r => r.GetByIdAsync(id, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(track);
    }

    private void GivenExistingArtist(long id)
    {
        _artistRepository.Setup(r => r.GetByIdAsync(id, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(new ArtistEntity { Id = id });
    }

    private void GivenExistingAlbum(long id)
    {
        _albumRepository.Setup(r => r.GetByIdAsync(id, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(new AlbumEntity { Id = id });
    }

    private void GivenExistingGenre(long id)
    {
        _genreRepository.Setup(r => r.GetByIdAsync(id, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(new GenreEntity { Id = id });
    }

    [Fact(DisplayName = "Handle should ignore event as fast change when played under 30 seconds and under 20 percent")]
    public async Task Handle_ShouldIgnoreEventAsFastChange_WhenPlayedUnder30SecondsAndUnder20Percent()
    {
        // Arrange
        CreateListeningEventRequestHandler handler = CreateHandler();
        CreateListeningEventRequest command = new() { TrackId = 1, DurationPlayed = 10, DurationTotal = 200 };

        // Act
        Result<long> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.Equal(0, result.Value);
        _repository.Verify(r => r.AddAsync(It.IsAny<ListeningEventEntity>(), It.IsAny<RepositoryConnectionKind>()), Times.Never);
    }

    [Fact(DisplayName = "Handle should persist event and mark it as skipped when under 20 percent but played more than 30 seconds")]
    public async Task Handle_ShouldPersistEvent_AndMarkItAsSkipped_WhenUnder20PercentButPlayedMoreThan30Seconds()
    {
        // Arrange
        ListeningEventEntity? capturedEntity = null;
        _repository.Setup(r => r.AddAsync(It.IsAny<ListeningEventEntity>(), It.IsAny<RepositoryConnectionKind>()))
            .Callback<ListeningEventEntity, RepositoryConnectionKind>((e, _) => capturedEntity = e)
            .ReturnsAsync(88);
        GivenTrack(1, artistId: 3, albumId: 2, genreId: 4);
        GivenExistingArtist(3);
        GivenExistingAlbum(2);
        GivenExistingGenre(4);
        CreateListeningEventRequestHandler handler = CreateHandler();
        CreateListeningEventRequest command = new() { TrackId = 1, AlbumId = 2, ArtistId = 3, GenreId = 4, DurationPlayed = 40, DurationTotal = 500 };

        // Act
        Result<long> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.Equal(88, result.Value);
        Assert.NotNull(capturedEntity);
        Assert.True(capturedEntity!.WasSkipped);
        Assert.Equal(1, capturedEntity.TrackId);
        Assert.Equal(40, capturedEntity.DurationPlayed);
    }

    [Fact(DisplayName = "Handle should persist event without skip flag when completion rate reaches 20 percent")]
    public async Task Handle_ShouldPersistEventWithoutSkipFlag_WhenCompletionRateReaches20Percent()
    {
        // Arrange
        ListeningEventEntity? capturedEntity = null;
        _repository.Setup(r => r.AddAsync(It.IsAny<ListeningEventEntity>(), It.IsAny<RepositoryConnectionKind>()))
            .Callback<ListeningEventEntity, RepositoryConnectionKind>((e, _) => capturedEntity = e)
            .ReturnsAsync(7);
        GivenTrack(1);
        CreateListeningEventRequestHandler handler = CreateHandler();
        CreateListeningEventRequest command = new() { TrackId = 1, DurationPlayed = 60, DurationTotal = 200 };

        // Act
        Result<long> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.Equal(7, result.Value);
        Assert.NotNull(capturedEntity);
        Assert.False(capturedEntity!.WasSkipped);
    }

    [Fact(DisplayName = "Handle should return failure when repository returns non-positive id")]
    public async Task Handle_ShouldReturnFailure_WhenRepositoryReturnsNonPositiveId()
    {
        // Arrange
        _repository.Setup(r => r.AddAsync(It.IsAny<ListeningEventEntity>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(0);
        GivenTrack(1);
        CreateListeningEventRequestHandler handler = CreateHandler();
        CreateListeningEventRequest command = new() { TrackId = 1, DurationPlayed = 100, DurationTotal = 200 };

        // Act
        Result<long> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<OperationError>().And.HaveErrorWithCode("listening_event.create_failed");
    }

    [Fact(DisplayName = "Handle should not persist event when the track no longer exists")]
    public async Task Handle_ShouldNotPersistEvent_WhenTrackNoLongerExists()
    {
        // Arrange
        _trackRepository.Setup(r => r.GetByIdAsync(42, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync((TrackEntity?)null);
        CreateListeningEventRequestHandler handler = CreateHandler();
        CreateListeningEventRequest command = new() { TrackId = 42, DurationPlayed = 100, DurationTotal = 200 };

        // Act
        Result<long> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<NotFoundError>().And.HaveErrorWithCode("track.not_found");
        _repository.Verify(r => r.AddAsync(It.IsAny<ListeningEventEntity>(), It.IsAny<RepositoryConnectionKind>()), Times.Never);
    }

    [Fact(DisplayName = "Handle should clear references pointing to deleted entities")]
    public async Task Handle_ShouldClearReferences_PointingToDeletedEntities()
    {
        // Arrange
        ListeningEventEntity? capturedEntity = null;
        _repository.Setup(r => r.AddAsync(It.IsAny<ListeningEventEntity>(), It.IsAny<RepositoryConnectionKind>()))
            .Callback<ListeningEventEntity, RepositoryConnectionKind>((e, _) => capturedEntity = e)
            .ReturnsAsync(11);
        GivenTrack(1, artistId: 3, albumId: 2, genreId: 4);
        GivenExistingArtist(3);
        CreateListeningEventRequestHandler handler = CreateHandler();
        CreateListeningEventRequest command = new() { TrackId = 1, DurationPlayed = 100, DurationTotal = 200 };

        // Act
        Result<long> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.NotNull(capturedEntity);
        Assert.Equal(3, capturedEntity!.ArtistId);
        Assert.Null(capturedEntity.AlbumId);
        Assert.Null(capturedEntity.GenreId);
    }

    [Fact(DisplayName = "Handle should use the stored track references instead of the stale ones from the request")]
    public async Task Handle_ShouldUseStoredTrackReferences_InsteadOfTheStaleOnesFromTheRequest()
    {
        // Arrange
        ListeningEventEntity? capturedEntity = null;
        _repository.Setup(r => r.AddAsync(It.IsAny<ListeningEventEntity>(), It.IsAny<RepositoryConnectionKind>()))
            .Callback<ListeningEventEntity, RepositoryConnectionKind>((e, _) => capturedEntity = e)
            .ReturnsAsync(12);
        GivenTrack(1, artistId: 30, albumId: 20, genreId: 40);
        GivenExistingArtist(30);
        GivenExistingAlbum(20);
        GivenExistingGenre(40);
        CreateListeningEventRequestHandler handler = CreateHandler();
        CreateListeningEventRequest command = new() { TrackId = 1, ArtistId = 3, AlbumId = 2, GenreId = 4, DurationPlayed = 100, DurationTotal = 200 };

        // Act
        Result<long> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.NotNull(capturedEntity);
        Assert.Equal(30, capturedEntity!.ArtistId);
        Assert.Equal(20, capturedEntity.AlbumId);
        Assert.Equal(40, capturedEntity.GenreId);
    }

    [Fact(DisplayName = "Handle should ignore event as fast change when the duration is unknown and the track was played under 30 seconds")]
    public async Task Handle_ShouldIgnoreEventAsFastChange_WhenDurationIsUnknownAndPlayedUnder30Seconds()
    {
        // Arrange
        GivenTrack(1);
        CreateListeningEventRequestHandler handler = CreateHandler();
        CreateListeningEventRequest command = new() { TrackId = 1, DurationPlayed = 2, DurationTotal = 0 };

        // Act
        Result<long> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.Equal(0, result.Value);
        _repository.Verify(r => r.AddAsync(It.IsAny<ListeningEventEntity>(), It.IsAny<RepositoryConnectionKind>()), Times.Never);
    }

    [Fact(DisplayName = "Handle should persist event without skip flag when the duration is unknown and the track was played over 30 seconds")]
    public async Task Handle_ShouldPersistEventWithoutSkipFlag_WhenDurationIsUnknownAndPlayedOver30Seconds()
    {
        // Arrange
        ListeningEventEntity? capturedEntity = null;
        _repository.Setup(r => r.AddAsync(It.IsAny<ListeningEventEntity>(), It.IsAny<RepositoryConnectionKind>()))
            .Callback<ListeningEventEntity, RepositoryConnectionKind>((e, _) => capturedEntity = e)
            .ReturnsAsync(21);
        GivenTrack(1);
        CreateListeningEventRequestHandler handler = CreateHandler();
        CreateListeningEventRequest command = new() { TrackId = 1, DurationPlayed = 300, DurationTotal = 0 };

        // Act
        Result<long> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.NotNull(capturedEntity);
        Assert.False(capturedEntity!.WasSkipped);
        Assert.Equal(300, capturedEntity.DurationPlayed);
        Assert.Equal(0, capturedEntity.DurationTotal);
    }
}