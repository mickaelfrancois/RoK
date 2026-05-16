using Moq;
using Rok.Application.Features.Tracks.Requests;
using Rok.Application.Interfaces.Repositories;

namespace Rok.ApplicationTests.Features.Tracks.Requests;

public class GetTrackByIdQueryHandlerTests
{
    [Fact(DisplayName = "Handle should return mapped track when track exists")]
    public async Task Handle_ShouldReturnMappedTrack_WhenTrackExists()
    {
        // Arrange
        TrackEntity entity = new() { Id = 3, Title = "Bohemian Rhapsody" };
        Mock<ITrackRepository> repository = new();
        repository.Setup(r => r.GetByIdAsync(3, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(entity);
        GetTrackByIdRequestHandler handler = new(repository.Object);

        // Act
        Result<TrackDto> result = await handler.Handle(new GetTrackByIdRequest(3), CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.Equal(3, result.Value.Id);
        Assert.Equal("Bohemian Rhapsody", result.Value.Title);
    }

    [Fact(DisplayName = "Handle should return NotFound failure when track does not exist")]
    public async Task Handle_ShouldReturnNotFoundFailure_WhenTrackDoesNotExist()
    {
        // Arrange
        Mock<ITrackRepository> repository = new();
        repository.Setup(r => r.GetByIdAsync(It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync((TrackEntity?)null);
        GetTrackByIdRequestHandler handler = new(repository.Object);

        // Act
        Result<TrackDto> result = await handler.Handle(new GetTrackByIdRequest(999), CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<NotFoundError>().And.HaveErrorWithCode("track.not_found");
    }
}

public class GetAllTracksQueryHandlerTests
{
    [Fact(DisplayName = "Handle should return all mapped tracks from repository")]
    public async Task Handle_ShouldReturnAllMappedTracks_FromRepository()
    {
        // Arrange
        List<TrackEntity> tracks = new()
        {
            new() { Id = 1, Title = "One" },
            new() { Id = 2, Title = "Two" }
        };
        Mock<ITrackRepository> repository = new();
        repository.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(tracks);
        GetAllTracksRequestHandler handler = new(repository.Object);

        // Act
        IEnumerable<TrackDto> result = await handler.Handle(new GetAllTracksRequest(), CancellationToken.None);

        // Assert
        Assert.Equal(2, result.Count());
    }
}

public class GetTracksByAlbumIdQueryHandlerTests
{
    [Fact(DisplayName = "Handle should return tracks fetched by album id")]
    public async Task Handle_ShouldReturnTracks_FetchedByAlbumId()
    {
        // Arrange
        List<TrackEntity> tracks = new() { new() { Id = 1, Title = "T1", AlbumId = 7 } };
        Mock<ITrackRepository> repository = new();
        repository.Setup(r => r.GetByAlbumIdAsync(7, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(tracks);
        GetTracksByAlbumIdRequestHandler handler = new(repository.Object);

        // Act
        IEnumerable<TrackDto> result = await handler.Handle(new GetTracksByAlbumIdRequest(7), CancellationToken.None);

        // Assert
        Assert.Single(result);
        repository.Verify(r => r.GetByAlbumIdAsync(7, It.IsAny<RepositoryConnectionKind>()), Times.Once);
    }
}

public class GetTracksByArtistIdQueryHandlerTests
{
    [Fact(DisplayName = "Handle should return tracks fetched by artist id")]
    public async Task Handle_ShouldReturnTracks_FetchedByArtistId()
    {
        // Arrange
        List<TrackEntity> tracks = new() { new() { Id = 1, Title = "T1", ArtistId = 4 } };
        Mock<ITrackRepository> repository = new();
        repository.Setup(r => r.GetByArtistIdAsync(4, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(tracks);
        GetTracksByArtistIdRequestHandler handler = new(repository.Object);

        // Act
        IEnumerable<TrackDto> result = await handler.Handle(new GetTracksByArtistIdRequest(4), CancellationToken.None);

        // Assert
        Assert.Single(result);
        repository.Verify(r => r.GetByArtistIdAsync(4, It.IsAny<RepositoryConnectionKind>()), Times.Once);
    }
}

public class GetTracksByGenreIdQueryHandlerTests
{
    [Fact(DisplayName = "Handle should return tracks fetched by genre id")]
    public async Task Handle_ShouldReturnTracks_FetchedByGenreId()
    {
        // Arrange
        List<TrackEntity> tracks = new() { new() { Id = 1, Title = "T1", GenreId = 2 } };
        Mock<ITrackRepository> repository = new();
        repository.Setup(r => r.GetByGenreIdAsync(2, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(tracks);
        GetTracksByGenreIdRequestHandler handler = new(repository.Object);

        // Act
        IEnumerable<TrackDto> result = await handler.Handle(new GetTracksByGenreIdRequest(2), CancellationToken.None);

        // Assert
        Assert.Single(result);
        repository.Verify(r => r.GetByGenreIdAsync(2, It.IsAny<RepositoryConnectionKind>()), Times.Once);
    }
}

public class GetTracksByPlaylistIdQueryHandlerTests
{
    [Fact(DisplayName = "Handle should return tracks fetched by playlist id")]
    public async Task Handle_ShouldReturnTracks_FetchedByPlaylistId()
    {
        // Arrange
        List<TrackEntity> tracks = new() { new() { Id = 1, Title = "T1" } };
        Mock<ITrackRepository> repository = new();
        repository.Setup(r => r.GetByPlaylistIdAsync(10, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(tracks);
        GetTracksByPlaylistIdRequestHandler handler = new(repository.Object);

        // Act
        IEnumerable<TrackDto> result = await handler.Handle(new GetTracksByPlaylistIdRequest(10), CancellationToken.None);

        // Assert
        Assert.Single(result);
        repository.Verify(r => r.GetByPlaylistIdAsync(10, It.IsAny<RepositoryConnectionKind>()), Times.Once);
    }
}

public class GetTracksByAlbumListQueryHandlerTests
{
    [Fact(DisplayName = "Handle should forward album ids and limit to repository")]
    public async Task Handle_ShouldForwardAlbumIdsAndLimit_ToRepository()
    {
        // Arrange
        List<long> ids = new() { 1, 2 };
        List<TrackEntity> tracks = new() { new() { Id = 1 }, new() { Id = 2 } };
        Mock<ITrackRepository> repository = new();
        repository.Setup(r => r.GetByAlbumIdAsync(ids, 50, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(tracks);
        GetTracksByAlbumListRequestHandler handler = new(repository.Object);

        // Act
        IEnumerable<TrackDto> result = await handler.Handle(new GetTracksByAlbumListRequest { AlbumsId = ids, Limit = 50 }, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.Count());
    }
}

public class GetTracksByArtistListQueryHandlerTests
{
    [Fact(DisplayName = "Handle should forward artist ids and limit to repository")]
    public async Task Handle_ShouldForwardArtistIdsAndLimit_ToRepository()
    {
        // Arrange
        List<long> ids = new() { 1, 2 };
        List<TrackEntity> tracks = new() { new() { Id = 1 } };
        Mock<ITrackRepository> repository = new();
        repository.Setup(r => r.GetByArtistIdAsync(ids, 25, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(tracks);
        GetTracksByArtistListRequestHandler handler = new(repository.Object);

        // Act
        IEnumerable<TrackDto> result = await handler.Handle(new GetTracksByArtistListRequest { ArtistIds = ids, Limit = 25 }, CancellationToken.None);

        // Assert
        Assert.Single(result);
    }
}

public class GetTracksCountQueryHandlerTests
{
    [Fact(DisplayName = "Handle should return count from repository")]
    public async Task Handle_ShouldReturnCount_FromRepository()
    {
        // Arrange
        Mock<ITrackRepository> repository = new();
        repository.Setup(r => r.CountAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(123);
        GetTracksCountRequestHandler handler = new(repository.Object);

        // Act
        int count = await handler.Handle(new GetTracksCountRequest(), CancellationToken.None);

        // Assert
        Assert.Equal(123, count);
    }
}
