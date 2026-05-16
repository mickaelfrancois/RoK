using Microsoft.Extensions.Logging;
using Moq;
using Rok.Application.Features.Playlists.Requests;
using Rok.Application.Interfaces.Repositories;

namespace Rok.ApplicationTests.Features.Playlists.Requests;

public class CreatePlaylistRequestHandlerTests
{
    [Fact(DisplayName = "Handle should return new id when playlist is created")]
    public async Task Handle_ShouldReturnNewId_WhenPlaylistIsCreated()
    {
        // Arrange
        Mock<IPlaylistHeaderRepository> repository = new();
        repository.Setup(r => r.AddAsync(It.IsAny<PlaylistHeaderEntity>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(42);
        CreatePlaylistRequestHandler handler = new(repository.Object);

        // Act
        Result<long> result = await handler.Handle(new CreatePlaylistRequest { Name = "My mix" }, CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.Equal(42, result.Value);
    }

    [Fact(DisplayName = "Handle should return failure when repository returns non-positive id")]
    public async Task Handle_ShouldReturnFailure_WhenRepositoryReturnsNonPositiveId()
    {
        // Arrange
        Mock<IPlaylistHeaderRepository> repository = new();
        repository.Setup(r => r.AddAsync(It.IsAny<PlaylistHeaderEntity>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(0);
        CreatePlaylistRequestHandler handler = new(repository.Object);

        // Act
        Result<long> result = await handler.Handle(new CreatePlaylistRequest { Name = "X" }, CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<OperationError>().And.HaveErrorWithCode("playlist.create_failed");
    }
}

public class UpdatePlaylistRequestHandlerTests
{
    [Fact(DisplayName = "Handle should return success when repository updates playlist")]
    public async Task Handle_ShouldReturnSuccess_WhenRepositoryUpdatesPlaylist()
    {
        // Arrange
        Mock<IPlaylistHeaderRepository> repository = new();
        repository.Setup(r => r.UpdateAsync(It.IsAny<PlaylistHeaderEntity>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(true);
        UpdatePlaylistRequestHandler handler = new(repository.Object, Mock.Of<ILogger<UpdatePlaylistRequestHandler>>());

        // Act
        Result result = await handler.Handle(new UpdatePlaylistRequest { Id = 1, Name = "N" }, CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
    }

    [Fact(DisplayName = "Handle should return failure when repository fails to update playlist")]
    public async Task Handle_ShouldReturnFailure_WhenRepositoryFails()
    {
        // Arrange
        Mock<IPlaylistHeaderRepository> repository = new();
        repository.Setup(r => r.UpdateAsync(It.IsAny<PlaylistHeaderEntity>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(false);
        UpdatePlaylistRequestHandler handler = new(repository.Object, Mock.Of<ILogger<UpdatePlaylistRequestHandler>>());

        // Act
        Result result = await handler.Handle(new UpdatePlaylistRequest { Id = 1 }, CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<OperationError>().And.HaveErrorWithCode("playlist.update_failed");
    }
}

public class DeletePlaylistRequestHandlerTests
{
    [Fact(DisplayName = "Handle should return success when one row is deleted")]
    public async Task Handle_ShouldReturnSuccess_WhenOneRowIsDeleted()
    {
        // Arrange
        Mock<IPlaylistHeaderRepository> repository = new();
        repository.Setup(r => r.DeleteAsync(7L, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(1);
        DeletePlaylistRequestHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.Handle(new DeletePlaylistRequest { Id = 7 }, CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
    }

    [Fact(DisplayName = "Handle should return failure when no row is deleted")]
    public async Task Handle_ShouldReturnFailure_WhenNoRowIsDeleted()
    {
        // Arrange
        Mock<IPlaylistHeaderRepository> repository = new();
        repository.Setup(r => r.DeleteAsync(It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(0);
        DeletePlaylistRequestHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.Handle(new DeletePlaylistRequest { Id = 7 }, CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<OperationError>().And.HaveErrorWithCode("playlist.delete_failed");
    }
}

public class UpdatePlaylistPictureRequestHandlerTests
{
    [Fact(DisplayName = "Handle should forward picture to repository")]
    public async Task Handle_ShouldForwardPicture_ToRepository()
    {
        // Arrange
        Mock<IPlaylistHeaderRepository> repository = new();
        repository.Setup(r => r.UpdatePictureAsync(1, "pic.png", It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(true);
        UpdatePlaylistPictureRequestHandler handler = new(repository.Object);

        // Act
        Unit _ = await handler.Handle(new UpdatePlaylistPictureRequest { Id = 1, Picture = "pic.png" }, CancellationToken.None);

        // Assert
        repository.Verify(r => r.UpdatePictureAsync(1, "pic.png", It.IsAny<RepositoryConnectionKind>()), Times.Once);
    }
}

public class AddTrackToPlaylistRequestHandlerTests
{
    [Fact(DisplayName = "Handle should add track and update playlist header when inputs are valid")]
    public async Task Handle_ShouldAddTrackAndUpdateHeader_WhenInputsAreValid()
    {
        // Arrange
        TrackEntity track = new() { Id = 5, Duration = 200 };
        PlaylistHeaderEntity header = new() { Id = 1, TrackCount = 2, Duration = 400 };

        Mock<ITrackRepository> trackRepository = new();
        trackRepository.Setup(r => r.GetByIdAsync(5, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(track);
        Mock<IPlaylistHeaderRepository> headerRepository = new();
        headerRepository.Setup(r => r.GetByIdAsync(1, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(header);
        headerRepository.Setup(r => r.UpdateAsync(header, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(true);
        Mock<IPlaylistTrackRepository> trackLinkRepository = new();
        trackLinkRepository.Setup(r => r.GetAsync(1L, 5L, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(0);
        trackLinkRepository.Setup(r => r.AddAsync(It.IsAny<PlaylistTrackEntity>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(99);

        AddTrackToPlaylistRequestHandler handler = new(trackLinkRepository.Object, headerRepository.Object, trackRepository.Object, Mock.Of<ILogger<AddTrackToPlaylistRequestHandler>>());

        // Act
        Result<long> result = await handler.Handle(new AddTrackToPlaylistRequest { PlaylistId = 1, TrackId = 5 }, CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.Equal(99, result.Value);
        Assert.Equal(3, header.TrackCount);
        Assert.Equal(600, header.Duration);
    }

    [Fact(DisplayName = "Handle should return failure when track does not exist")]
    public async Task Handle_ShouldReturnFailure_WhenTrackDoesNotExist()
    {
        // Arrange
        Mock<ITrackRepository> trackRepository = new();
        trackRepository.Setup(r => r.GetByIdAsync(It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync((TrackEntity?)null);
        AddTrackToPlaylistRequestHandler handler = new(Mock.Of<IPlaylistTrackRepository>(), Mock.Of<IPlaylistHeaderRepository>(), trackRepository.Object, Mock.Of<ILogger<AddTrackToPlaylistRequestHandler>>());

        // Act
        Result<long> result = await handler.Handle(new AddTrackToPlaylistRequest { PlaylistId = 1, TrackId = 5 }, CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<NotFoundError>().And.HaveErrorWithCode("track.not_found");
    }

    [Fact(DisplayName = "Handle should return failure when playlist does not exist")]
    public async Task Handle_ShouldReturnFailure_WhenPlaylistDoesNotExist()
    {
        // Arrange
        Mock<ITrackRepository> trackRepository = new();
        trackRepository.Setup(r => r.GetByIdAsync(It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(new TrackEntity { Id = 5 });
        Mock<IPlaylistHeaderRepository> headerRepository = new();
        headerRepository.Setup(r => r.GetByIdAsync(It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync((PlaylistHeaderEntity?)null);

        AddTrackToPlaylistRequestHandler handler = new(Mock.Of<IPlaylistTrackRepository>(), headerRepository.Object, trackRepository.Object, Mock.Of<ILogger<AddTrackToPlaylistRequestHandler>>());

        // Act
        Result<long> result = await handler.Handle(new AddTrackToPlaylistRequest { PlaylistId = 1, TrackId = 5 }, CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<NotFoundError>().And.HaveErrorWithCode("playlist.not_found");
    }

    [Fact(DisplayName = "Handle should return DUPLICATE failure when track already exists in playlist")]
    public async Task Handle_ShouldReturnDuplicateFailure_WhenTrackAlreadyExistsInPlaylist()
    {
        // Arrange
        Mock<ITrackRepository> trackRepository = new();
        trackRepository.Setup(r => r.GetByIdAsync(It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(new TrackEntity { Id = 5 });
        Mock<IPlaylistHeaderRepository> headerRepository = new();
        headerRepository.Setup(r => r.GetByIdAsync(It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(new PlaylistHeaderEntity { Id = 1 });
        Mock<IPlaylistTrackRepository> trackLinkRepository = new();
        trackLinkRepository.Setup(r => r.GetAsync(1L, 5L, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(77);

        AddTrackToPlaylistRequestHandler handler = new(trackLinkRepository.Object, headerRepository.Object, trackRepository.Object, Mock.Of<ILogger<AddTrackToPlaylistRequestHandler>>());

        // Act
        Result<long> result = await handler.Handle(new AddTrackToPlaylistRequest { PlaylistId = 1, TrackId = 5 }, CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<ConflictError>().And.HaveErrorWithCode("playlist.duplicate_track");
        trackLinkRepository.Verify(r => r.AddAsync(It.IsAny<PlaylistTrackEntity>(), It.IsAny<RepositoryConnectionKind>()), Times.Never);
    }

    [Fact(DisplayName = "Handle should return failure when repository throws inside transaction")]
    public async Task Handle_ShouldReturnFailure_WhenRepositoryThrowsInsideTransaction()
    {
        // Arrange
        Mock<ITrackRepository> trackRepository = new();
        trackRepository.Setup(r => r.GetByIdAsync(5, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(new TrackEntity { Id = 5 });
        Mock<IPlaylistHeaderRepository> headerRepository = new();
        headerRepository.Setup(r => r.GetByIdAsync(1, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(new PlaylistHeaderEntity { Id = 1 });
        Mock<IPlaylistTrackRepository> trackLinkRepository = new();
        trackLinkRepository.Setup(r => r.GetAsync(1L, 5L, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(0);
        trackLinkRepository.Setup(r => r.AddAsync(It.IsAny<PlaylistTrackEntity>(), It.IsAny<RepositoryConnectionKind>())).ThrowsAsync(new InvalidOperationException("DB error"));

        AddTrackToPlaylistRequestHandler handler = new(trackLinkRepository.Object, headerRepository.Object, trackRepository.Object, Mock.Of<ILogger<AddTrackToPlaylistRequestHandler>>());

        // Act
        Result<long> result = await handler.Handle(new AddTrackToPlaylistRequest { PlaylistId = 1, TrackId = 5 }, CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<OperationError>().And.HaveErrorWithCode("playlist.add_track_transaction_failed");
    }
}

public class AddAlbumToPlaylistRequestHandlerTests
{
    [Fact(DisplayName = "Handle should add only tracks not already in the playlist")]
    public async Task Handle_ShouldAddOnlyTracksNotAlreadyInPlaylist()
    {
        // Arrange
        List<TrackEntity> tracks = new()
        {
            new() { Id = 1, Duration = 100 },
            new() { Id = 2, Duration = 200 },
            new() { Id = 3, Duration = 300 }
        };
        PlaylistHeaderEntity header = new() { Id = 10, TrackCount = 0, Duration = 0 };

        Mock<ITrackRepository> trackRepository = new();
        trackRepository.Setup(r => r.GetByAlbumIdAsync(5L, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(tracks);
        Mock<IPlaylistHeaderRepository> headerRepository = new();
        headerRepository.Setup(r => r.GetByIdAsync(10, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(header);
        headerRepository.Setup(r => r.UpdateAsync(header, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(true);
        Mock<IPlaylistTrackRepository> trackLinkRepository = new();
        trackLinkRepository.Setup(r => r.GetAsync(10L, 1L, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(0);
        trackLinkRepository.Setup(r => r.GetAsync(10L, 2L, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(55);
        trackLinkRepository.Setup(r => r.GetAsync(10L, 3L, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(0);
        trackLinkRepository.Setup(r => r.AddAsync(It.IsAny<PlaylistTrackEntity>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(1);

        AddAlbumToPlaylistRequestHandler handler = new(trackLinkRepository.Object, headerRepository.Object, trackRepository.Object, Mock.Of<ILogger<AddAlbumToPlaylistRequestHandler>>());

        // Act
        Result<long> result = await handler.Handle(new AddAlbumToPlaylistRequest { PlaylistId = 10, AlbumId = 5 }, CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.Equal(2, result.Value);
        trackLinkRepository.Verify(r => r.AddAsync(It.IsAny<PlaylistTrackEntity>(), It.IsAny<RepositoryConnectionKind>()), Times.Exactly(2));
        Assert.Equal(2, header.TrackCount);
        Assert.Equal(400, header.Duration);
    }

    [Fact(DisplayName = "Handle should return failure when playlist does not exist")]
    public async Task Handle_ShouldReturnFailure_WhenPlaylistDoesNotExist()
    {
        // Arrange
        Mock<ITrackRepository> trackRepository = new();
        trackRepository.Setup(r => r.GetByAlbumIdAsync(It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(new List<TrackEntity>());
        Mock<IPlaylistHeaderRepository> headerRepository = new();
        headerRepository.Setup(r => r.GetByIdAsync(It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync((PlaylistHeaderEntity?)null);

        AddAlbumToPlaylistRequestHandler handler = new(Mock.Of<IPlaylistTrackRepository>(), headerRepository.Object, trackRepository.Object, Mock.Of<ILogger<AddAlbumToPlaylistRequestHandler>>());

        // Act
        Result<long> result = await handler.Handle(new AddAlbumToPlaylistRequest { PlaylistId = 99, AlbumId = 5 }, CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<NotFoundError>().And.HaveErrorWithCode("playlist.not_found");
    }

    [Fact(DisplayName = "Handle should return failure when no new track was added")]
    public async Task Handle_ShouldReturnFailure_WhenNoNewTrackWasAdded()
    {
        // Arrange
        List<TrackEntity> tracks = new() { new() { Id = 1 } };
        Mock<ITrackRepository> trackRepository = new();
        trackRepository.Setup(r => r.GetByAlbumIdAsync(It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(tracks);
        Mock<IPlaylistHeaderRepository> headerRepository = new();
        headerRepository.Setup(r => r.GetByIdAsync(It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(new PlaylistHeaderEntity { Id = 10 });
        Mock<IPlaylistTrackRepository> trackLinkRepository = new();
        trackLinkRepository.Setup(r => r.GetAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(77);

        AddAlbumToPlaylistRequestHandler handler = new(trackLinkRepository.Object, headerRepository.Object, trackRepository.Object, Mock.Of<ILogger<AddAlbumToPlaylistRequestHandler>>());

        // Act
        Result<long> result = await handler.Handle(new AddAlbumToPlaylistRequest { PlaylistId = 10, AlbumId = 1 }, CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<OperationError>().And.HaveErrorWithCode("playlist.add_album_no_tracks");
    }

    [Fact(DisplayName = "Handle should return failure when repository throws inside transaction")]
    public async Task Handle_ShouldReturnFailure_WhenRepositoryThrowsInsideTransaction()
    {
        // Arrange
        List<TrackEntity> tracks = new() { new() { Id = 1, Duration = 100 } };
        Mock<ITrackRepository> trackRepository = new();
        trackRepository.Setup(r => r.GetByAlbumIdAsync(5L, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(tracks);
        Mock<IPlaylistHeaderRepository> headerRepository = new();
        headerRepository.Setup(r => r.GetByIdAsync(10, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(new PlaylistHeaderEntity { Id = 10 });
        Mock<IPlaylistTrackRepository> trackLinkRepository = new();
        trackLinkRepository.Setup(r => r.GetAsync(10L, 1L, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(0);
        trackLinkRepository.Setup(r => r.AddAsync(It.IsAny<PlaylistTrackEntity>(), It.IsAny<RepositoryConnectionKind>())).ThrowsAsync(new InvalidOperationException("DB error"));

        AddAlbumToPlaylistRequestHandler handler = new(trackLinkRepository.Object, headerRepository.Object, trackRepository.Object, Mock.Of<ILogger<AddAlbumToPlaylistRequestHandler>>());

        // Act
        Result<long> result = await handler.Handle(new AddAlbumToPlaylistRequest { PlaylistId = 10, AlbumId = 5 }, CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<OperationError>().And.HaveErrorWithCode("playlist.add_album_transaction_failed");
    }
}

public class AddArtistToPlaylistRequestHandlerTests
{
    [Fact(DisplayName = "Handle should add tracks of the artist and increment playlist count")]
    public async Task Handle_ShouldAddTracksOfArtist_AndIncrementPlaylistCount()
    {
        // Arrange
        List<TrackEntity> tracks = new()
        {
            new() { Id = 1, Duration = 120 },
            new() { Id = 2, Duration = 180 }
        };
        PlaylistHeaderEntity header = new() { Id = 10 };

        Mock<ITrackRepository> trackRepository = new();
        trackRepository.Setup(r => r.GetByArtistIdAsync(7L, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(tracks);
        Mock<IPlaylistHeaderRepository> headerRepository = new();
        headerRepository.Setup(r => r.GetByIdAsync(10, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(header);
        headerRepository.Setup(r => r.UpdateAsync(header, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(true);
        Mock<IPlaylistTrackRepository> trackLinkRepository = new();
        trackLinkRepository.Setup(r => r.GetAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(0);
        trackLinkRepository.Setup(r => r.AddAsync(It.IsAny<PlaylistTrackEntity>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(1);

        AddArtistToPlaylistRequestHandler handler = new(trackLinkRepository.Object, headerRepository.Object, trackRepository.Object, Mock.Of<ILogger<AddArtistToPlaylistRequestHandler>>());

        // Act
        Result<long> result = await handler.Handle(new AddArtistToPlaylistRequest { PlaylistId = 10, ArtistId = 7 }, CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.Equal(2, result.Value);
        Assert.Equal(2, header.TrackCount);
        Assert.Equal(300, header.Duration);
    }

    [Fact(DisplayName = "Handle should return failure when repository throws inside transaction")]
    public async Task Handle_ShouldReturnFailure_WhenRepositoryThrowsInsideTransaction()
    {
        // Arrange
        List<TrackEntity> tracks = new() { new() { Id = 1, Duration = 100 } };
        Mock<ITrackRepository> trackRepository = new();
        trackRepository.Setup(r => r.GetByArtistIdAsync(7L, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(tracks);
        Mock<IPlaylistHeaderRepository> headerRepository = new();
        headerRepository.Setup(r => r.GetByIdAsync(10, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(new PlaylistHeaderEntity { Id = 10 });
        Mock<IPlaylistTrackRepository> trackLinkRepository = new();
        trackLinkRepository.Setup(r => r.GetAsync(10L, 1L, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(0);
        trackLinkRepository.Setup(r => r.AddAsync(It.IsAny<PlaylistTrackEntity>(), It.IsAny<RepositoryConnectionKind>())).ThrowsAsync(new InvalidOperationException("DB error"));

        AddArtistToPlaylistRequestHandler handler = new(trackLinkRepository.Object, headerRepository.Object, trackRepository.Object, Mock.Of<ILogger<AddArtistToPlaylistRequestHandler>>());

        // Act
        Result<long> result = await handler.Handle(new AddArtistToPlaylistRequest { PlaylistId = 10, ArtistId = 7 }, CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<OperationError>().And.HaveErrorWithCode("playlist.add_artist_transaction_failed");
    }
}

public class RemoveTrackFromPlaylistRequestHandlerTests
{
    [Fact(DisplayName = "Handle should remove track and decrement playlist counters")]
    public async Task Handle_ShouldRemoveTrack_AndDecrementPlaylistCounters()
    {
        // Arrange
        TrackEntity track = new() { Id = 5, Duration = 120 };
        PlaylistHeaderEntity header = new() { Id = 10, TrackCount = 2, Duration = 400 };

        Mock<ITrackRepository> trackRepository = new();
        trackRepository.Setup(r => r.GetByIdAsync(5, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(track);
        Mock<IPlaylistHeaderRepository> headerRepository = new();
        headerRepository.Setup(r => r.GetByIdAsync(10, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(header);
        headerRepository.Setup(r => r.UpdateAsync(header, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(true);
        Mock<IPlaylistTrackRepository> trackLinkRepository = new();
        trackLinkRepository.Setup(r => r.GetAsync(10L, 5L, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(77);
        trackLinkRepository.Setup(r => r.DeleteAsync(10L, 5L, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(1);

        RemoveTrackFromPlaylistRequestHandler handler = new(trackLinkRepository.Object, headerRepository.Object, trackRepository.Object, Mock.Of<ILogger<RemoveTrackFromPlaylistRequestHandler>>());

        // Act
        Result result = await handler.Handle(new RemoveTrackFromPlaylistRequest { PlaylistId = 10, TrackId = 5 }, CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.Equal(1, header.TrackCount);
        Assert.Equal(280, header.Duration);
    }

    [Fact(DisplayName = "Handle should not let track count or duration go below zero")]
    public async Task Handle_ShouldNotLetTrackCountOrDuration_GoBelowZero()
    {
        // Arrange
        TrackEntity track = new() { Id = 5, Duration = 999 };
        PlaylistHeaderEntity header = new() { Id = 10, TrackCount = 0, Duration = 0 };

        Mock<ITrackRepository> trackRepository = new();
        trackRepository.Setup(r => r.GetByIdAsync(5, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(track);
        Mock<IPlaylistHeaderRepository> headerRepository = new();
        headerRepository.Setup(r => r.GetByIdAsync(10, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(header);
        headerRepository.Setup(r => r.UpdateAsync(header, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(true);
        Mock<IPlaylistTrackRepository> trackLinkRepository = new();
        trackLinkRepository.Setup(r => r.GetAsync(10L, 5L, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(77);
        trackLinkRepository.Setup(r => r.DeleteAsync(10L, 5L, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(1);

        RemoveTrackFromPlaylistRequestHandler handler = new(trackLinkRepository.Object, headerRepository.Object, trackRepository.Object, Mock.Of<ILogger<RemoveTrackFromPlaylistRequestHandler>>());

        // Act
        Result result = await handler.Handle(new RemoveTrackFromPlaylistRequest { PlaylistId = 10, TrackId = 5 }, CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.Equal(0, header.TrackCount);
        Assert.Equal(0, header.Duration);
    }

    [Fact(DisplayName = "Handle should return failure when track is not in playlist")]
    public async Task Handle_ShouldReturnFailure_WhenTrackIsNotInPlaylist()
    {
        // Arrange
        Mock<ITrackRepository> trackRepository = new();
        trackRepository.Setup(r => r.GetByIdAsync(It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(new TrackEntity { Id = 5 });
        Mock<IPlaylistHeaderRepository> headerRepository = new();
        headerRepository.Setup(r => r.GetByIdAsync(It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(new PlaylistHeaderEntity { Id = 10 });
        Mock<IPlaylistTrackRepository> trackLinkRepository = new();
        trackLinkRepository.Setup(r => r.GetAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(0);

        RemoveTrackFromPlaylistRequestHandler handler = new(trackLinkRepository.Object, headerRepository.Object, trackRepository.Object, Mock.Of<ILogger<RemoveTrackFromPlaylistRequestHandler>>());

        // Act
        Result result = await handler.Handle(new RemoveTrackFromPlaylistRequest { PlaylistId = 10, TrackId = 5 }, CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<OperationError>().And.HaveErrorWithCode("playlist.track_not_in_playlist");
    }

    [Fact(DisplayName = "Handle should return failure when repository throws inside transaction")]
    public async Task Handle_ShouldReturnFailure_WhenRepositoryThrowsInsideTransaction()
    {
        // Arrange
        Mock<ITrackRepository> trackRepository = new();
        trackRepository.Setup(r => r.GetByIdAsync(5, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(new TrackEntity { Id = 5 });
        Mock<IPlaylistHeaderRepository> headerRepository = new();
        headerRepository.Setup(r => r.GetByIdAsync(10, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(new PlaylistHeaderEntity { Id = 10 });
        Mock<IPlaylistTrackRepository> trackLinkRepository = new();
        trackLinkRepository.Setup(r => r.GetAsync(10L, 5L, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(77);
        trackLinkRepository.Setup(r => r.DeleteAsync(10L, 5L, It.IsAny<RepositoryConnectionKind>())).ThrowsAsync(new InvalidOperationException("DB error"));

        RemoveTrackFromPlaylistRequestHandler handler = new(trackLinkRepository.Object, headerRepository.Object, trackRepository.Object, Mock.Of<ILogger<RemoveTrackFromPlaylistRequestHandler>>());

        // Act
        Result result = await handler.Handle(new RemoveTrackFromPlaylistRequest { PlaylistId = 10, TrackId = 5 }, CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<OperationError>().And.HaveErrorWithCode("playlist.remove_track_failed");
    }
}

public class CreatePlaylistTracksRequestHandlerTests
{
    [Fact(DisplayName = "Handle should clear playlist tracks and re-add them with their position")]
    public async Task Handle_ShouldClearPlaylistTracks_AndReAddThemWithPosition()
    {
        // Arrange
        Mock<IPlaylistTrackRepository> repository = new();
        repository.Setup(r => r.DeleteAsync(10L, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(0);
        repository.Setup(r => r.AddAsync(It.IsAny<PlaylistTrackEntity>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(1);
        CreatePlaylistTracksRequestHandler handler = new(repository.Object);

        CreatePlaylistTracksRequest command = new()
        {
            PlaylistId = 10,
            Tracks = new List<CreatePlaylistTracksDto>
            {
                new() { TrackId = 1, Position = 0, Listened = false },
                new() { TrackId = 2, Position = 1, Listened = true }
            }
        };

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        repository.Verify(r => r.DeleteAsync(10L, It.IsAny<RepositoryConnectionKind>()), Times.Once);
        repository.Verify(r => r.AddAsync(It.Is<PlaylistTrackEntity>(e => e.PlaylistId == 10 && e.TrackId == 1 && e.Position == 0 && !e.Listened), It.IsAny<RepositoryConnectionKind>()), Times.Once);
        repository.Verify(r => r.AddAsync(It.Is<PlaylistTrackEntity>(e => e.PlaylistId == 10 && e.TrackId == 2 && e.Position == 1 && e.Listened), It.IsAny<RepositoryConnectionKind>()), Times.Once);
    }
}

public class MovePlaylistTracksRequestHandlerTests
{
    [Fact(DisplayName = "Handle should update position only for tracks whose index changed")]
    public async Task Handle_ShouldUpdatePosition_OnlyForTracksWhoseIndexChanged()
    {
        // Arrange
        List<PlaylistTrackEntity> current = new()
        {
            new() { Id = 100, TrackId = 1, Position = 0 },
            new() { Id = 101, TrackId = 2, Position = 1 },
            new() { Id = 102, TrackId = 3, Position = 2 }
        };
        Mock<IPlaylistTrackRepository> repository = new();
        repository.Setup(r => r.GetAsync(10L, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(current);
        repository.Setup(r => r.UpdatePositionAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(1);
        MovePlaylistTracksRequestHandler handler = new(repository.Object, Mock.Of<ILogger<MovePlaylistTracksRequestHandler>>());

        MovePlaylistTracksRequest command = new() { PlaylistId = 10, Tracks = new List<long> { 3, 2, 1 } };

        // Act
        Result<bool> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        repository.Verify(r => r.UpdatePositionAsync(102, 0, It.IsAny<RepositoryConnectionKind>()), Times.Once);
        repository.Verify(r => r.UpdatePositionAsync(100, 2, It.IsAny<RepositoryConnectionKind>()), Times.Once);
        repository.Verify(r => r.UpdatePositionAsync(101, It.IsAny<int>(), It.IsAny<RepositoryConnectionKind>()), Times.Never);
    }

    [Fact(DisplayName = "Handle should return failure when a position update fails")]
    public async Task Handle_ShouldReturnFailure_WhenPositionUpdateFails()
    {
        // Arrange
        List<PlaylistTrackEntity> current = new()
        {
            new() { Id = 100, TrackId = 1, Position = 0 },
            new() { Id = 101, TrackId = 2, Position = 1 }
        };
        Mock<IPlaylistTrackRepository> repository = new();
        repository.Setup(r => r.GetAsync(10L, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(current);
        repository.Setup(r => r.UpdatePositionAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(0);
        MovePlaylistTracksRequestHandler handler = new(repository.Object, Mock.Of<ILogger<MovePlaylistTracksRequestHandler>>());

        // Act
        Result<bool> result = await handler.Handle(new MovePlaylistTracksRequest { PlaylistId = 10, Tracks = new List<long> { 2, 1 } }, CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<OperationError>().And.HaveErrorWithCode("playlist.move_tracks_failed");
    }

    [Fact(DisplayName = "Handle should return failure when repository throws inside transaction")]
    public async Task Handle_ShouldReturnFailure_WhenRepositoryThrowsInsideTransaction()
    {
        // Arrange
        Mock<IPlaylistTrackRepository> repository = new();
        repository.Setup(r => r.GetAsync(10L, It.IsAny<RepositoryConnectionKind>())).ThrowsAsync(new InvalidOperationException("DB error"));
        MovePlaylistTracksRequestHandler handler = new(repository.Object, Mock.Of<ILogger<MovePlaylistTracksRequestHandler>>());

        // Act
        Result<bool> result = await handler.Handle(new MovePlaylistTracksRequest { PlaylistId = 10, Tracks = new List<long> { 1, 2 } }, CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<OperationError>().And.HaveErrorWithCode("playlist.move_tracks_failed");
    }
}
