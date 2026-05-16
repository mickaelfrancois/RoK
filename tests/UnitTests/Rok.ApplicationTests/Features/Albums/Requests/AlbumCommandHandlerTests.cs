using Moq;
using Rok.Application.Features.Albums.Requests;
using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Repositories;

namespace Rok.ApplicationTests.Features.Albums.Requests;

public class UpdateAlbumFavoriteRequestHandlerTests
{
    [Fact(DisplayName = "Handle should return success when repository updates favorite")]
    public async Task Handle_ShouldReturnSuccess_WhenRepositoryUpdatesFavorite()
    {
        // Arrange
        Mock<IAlbumRepository> repository = new();
        repository.Setup(r => r.UpdateFavoriteAsync(1, true, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(true);
        UpdateAlbumFavoriteRequestHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.Handle(new UpdateAlbumFavoriteRequest(1, true), CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
    }

    [Fact(DisplayName = "Handle should return failure when repository fails to update favorite")]
    public async Task Handle_ShouldReturnFailure_WhenRepositoryFails()
    {
        // Arrange
        Mock<IAlbumRepository> repository = new();
        repository.Setup(r => r.UpdateFavoriteAsync(It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(false);
        UpdateAlbumFavoriteRequestHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.Handle(new UpdateAlbumFavoriteRequest(1, true), CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<OperationError>().And.HaveErrorWithCode("album.favorite_update_failed");
    }
}

public class UpdateAlbumLastListenRequestHandlerTests
{
    [Fact(DisplayName = "Handle should return success when repository updates last listen")]
    public async Task Handle_ShouldReturnSuccess_WhenRepositoryUpdatesLastListen()
    {
        // Arrange
        Mock<IAlbumRepository> repository = new();
        repository.Setup(r => r.UpdateLastListenAsync(1, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(true);
        UpdateAlbumLastListenRequestHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.Handle(new UpdateAlbumLastListenRequest(1), CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
    }

    [Fact(DisplayName = "Handle should return failure when repository fails to update last listen")]
    public async Task Handle_ShouldReturnFailure_WhenRepositoryFails()
    {
        // Arrange
        Mock<IAlbumRepository> repository = new();
        repository.Setup(r => r.UpdateLastListenAsync(It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(false);
        UpdateAlbumLastListenRequestHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.Handle(new UpdateAlbumLastListenRequest(1), CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<OperationError>().And.HaveErrorWithCode("album.last_listen_update_failed");
    }
}

public class ResetAlbumListenCountRequestHandlerTests
{
    [Fact(DisplayName = "Handle should return success when repository resets listen count")]
    public async Task Handle_ShouldReturnSuccess_WhenRepositoryResetsListenCount()
    {
        // Arrange
        Mock<IAlbumRepository> repository = new();
        repository.Setup(r => r.ResetListenCountAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(true);
        ResetAlbumListenCountRequestHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.Handle(new ResetAlbumListenCountRequest(), CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
    }

    [Fact(DisplayName = "Handle should return failure when repository fails to reset listen count")]
    public async Task Handle_ShouldReturnFailure_WhenRepositoryFails()
    {
        // Arrange
        Mock<IAlbumRepository> repository = new();
        repository.Setup(r => r.ResetListenCountAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(false);
        ResetAlbumListenCountRequestHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.Handle(new ResetAlbumListenCountRequest(), CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<OperationError>().And.HaveErrorWithCode("album.listen_count_reset_failed");
    }
}

public class UpdateAlbumStatisticsRequestHandlerTests
{
    [Fact(DisplayName = "Handle should forward track count and duration to repository")]
    public async Task Handle_ShouldForwardTrackCountAndDuration_ToRepository()
    {
        // Arrange
        Mock<IAlbumRepository> repository = new();
        repository.Setup(r => r.UpdateStatisticsAsync(5, 12, 3600, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(true);
        UpdateAlbumStatisticsRequestHandler handler = new(repository.Object);
        UpdateAlbumStatisticsRequest command = new(5) { TrackCount = 12, Duration = 3600 };

        // Act
        Result<bool> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        repository.Verify(r => r.UpdateStatisticsAsync(5, 12, 3600, It.IsAny<RepositoryConnectionKind>()), Times.Once);
    }

    [Fact(DisplayName = "Handle should return failure when repository fails to update statistics")]
    public async Task Handle_ShouldReturnFailure_WhenRepositoryFails()
    {
        // Arrange
        Mock<IAlbumRepository> repository = new();
        repository.Setup(r => r.UpdateStatisticsAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(false);
        UpdateAlbumStatisticsRequestHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.Handle(new UpdateAlbumStatisticsRequest(1), CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<OperationError>().And.HaveErrorWithCode("album.statistics_update_failed");
    }
}

public class UpdateAlbumPictureDominantColorRequestHandlerTests
{
    [Fact(DisplayName = "Handle should forward color value to repository and succeed")]
    public async Task Handle_ShouldForwardColorValue_AndSucceed()
    {
        // Arrange
        Mock<IAlbumRepository> repository = new();
        repository.Setup(r => r.UpdatePictureDominantColorAsync(1, 255L, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(true);
        UpdateAlbumPictureDominantColorRequestHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.Handle(new UpdateAlbumPictureDominantColorRequest(1, 255L), CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
    }

    [Fact(DisplayName = "Handle should return failure when repository fails to update dominant color")]
    public async Task Handle_ShouldReturnFailure_WhenRepositoryFails()
    {
        // Arrange
        Mock<IAlbumRepository> repository = new();
        repository.Setup(r => r.UpdatePictureDominantColorAsync(It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(false);
        UpdateAlbumPictureDominantColorRequestHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.Handle(new UpdateAlbumPictureDominantColorRequest(1, null), CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<OperationError>().And.HaveErrorWithCode("album.picture_dominant_color_update_failed");
    }
}

public class UpdateAlbumGetMetaDataLastAttemptRequestHandlerTests
{
    [Fact(DisplayName = "Handle should return success when repository updates metadata timestamp")]
    public async Task Handle_ShouldReturnSuccess_WhenRepositoryUpdatesTimestamp()
    {
        // Arrange
        Mock<IAlbumRepository> repository = new();
        repository.Setup(r => r.UpdateGetMetaDataLastAttemptAsync(1, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(true);
        UpdateAlbumGetMetaDataLastAttemptRequestHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.Handle(new UpdateAlbumGetMetaDataLastAttemptRequest(1), CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
    }

    [Fact(DisplayName = "Handle should return failure when repository fails to update metadata timestamp")]
    public async Task Handle_ShouldReturnFailure_WhenRepositoryFails()
    {
        // Arrange
        Mock<IAlbumRepository> repository = new();
        repository.Setup(r => r.UpdateGetMetaDataLastAttemptAsync(It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(false);
        UpdateAlbumGetMetaDataLastAttemptRequestHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.Handle(new UpdateAlbumGetMetaDataLastAttemptRequest(1), CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<OperationError>().And.HaveErrorWithCode("album.meta_attempt_update_failed");
    }
}

public class UpdateAlbumTagsRequestHandlerTests
{
    [Fact(DisplayName = "Handle should pass album tag context to tag repository")]
    public async Task Handle_ShouldPassAlbumTagContext_ToTagRepository()
    {
        // Arrange
        Mock<ITagRepository> repository = new();
        IEnumerable<string> tags = new[] { "rock", "pop" };
        repository.Setup(r => r.UpdateEntityTagsAsync(1, tags, "albumtags", "albumid")).ReturnsAsync(true);
        UpdateAlbumTagsRequestHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.Handle(new UpdateAlbumTagsRequest(1, tags), CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        repository.Verify(r => r.UpdateEntityTagsAsync(1, tags, "albumtags", "albumid"), Times.Once);
    }

    [Fact(DisplayName = "Handle should return failure when tag repository fails to update tags")]
    public async Task Handle_ShouldReturnFailure_WhenTagRepositoryFails()
    {
        // Arrange
        Mock<ITagRepository> repository = new();
        repository.Setup(r => r.UpdateEntityTagsAsync(It.IsAny<long>(), It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(false);
        UpdateAlbumTagsRequestHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.Handle(new UpdateAlbumTagsRequest(1, Array.Empty<string>()), CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<OperationError>().And.HaveErrorWithCode("album.tags_update_failed");
    }
}

public class UpdateAlbumRequestHandlerTests
{
    [Fact(DisplayName = "Handle should apply patched fields and persist the entity")]
    public async Task Handle_ShouldApplyPatchedFields_AndPersistEntity()
    {
        // Arrange
        AlbumEntity entity = new() { Id = 1, Name = "Old", Label = "Original label" };
        Mock<IAlbumRepository> repository = new();
        repository.Setup(r => r.GetByIdAsync(1, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(entity);
        repository.Setup(r => r.UpdateAsync(entity, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(true);
        UpdateAlbumRequestHandler handler = new(repository.Object);

        UpdateAlbumRequest command = new() { Id = 1 };
        command.Label.Set("New label");
        command.IsLive.Set(true);

        // Act
        Result<bool> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.Equal("New label", entity.Label);
        Assert.True(entity.IsLive);
    }

    [Fact(DisplayName = "Handle should reset metadata attempt timestamp when MusicBrainzID is patched")]
    public async Task Handle_ShouldResetMetadataAttemptTimestamp_WhenMusicBrainzIdIsPatched()
    {
        // Arrange
        AlbumEntity entity = new() { Id = 1, GetMetaDataLastAttempt = DateTime.UtcNow };
        Mock<IAlbumRepository> repository = new();
        repository.Setup(r => r.GetByIdAsync(1, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(entity);
        repository.Setup(r => r.UpdateAsync(entity, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(true);
        UpdateAlbumRequestHandler handler = new(repository.Object);

        UpdateAlbumRequest command = new() { Id = 1 };
        command.MusicBrainzID.Set("mbid-123");

        // Act
        Result<bool> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.Equal("mbid-123", entity.MusicBrainzID);
        Assert.Null(entity.GetMetaDataLastAttempt);
    }

    [Fact(DisplayName = "Handle should return failure when album is not found")]
    public async Task Handle_ShouldReturnFailure_WhenAlbumNotFound()
    {
        // Arrange
        Mock<IAlbumRepository> repository = new();
        repository.Setup(r => r.GetByIdAsync(It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync((AlbumEntity?)null);
        UpdateAlbumRequestHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.Handle(new UpdateAlbumRequest { Id = 99 }, CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<NotFoundError>().And.HaveErrorWithCode("album.not_found");
    }

    [Fact(DisplayName = "Handle should return failure when repository fails to update album")]
    public async Task Handle_ShouldReturnFailure_WhenRepositoryFailsToUpdate()
    {
        // Arrange
        AlbumEntity entity = new() { Id = 1 };
        Mock<IAlbumRepository> repository = new();
        repository.Setup(r => r.GetByIdAsync(1, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(entity);
        repository.Setup(r => r.UpdateAsync(entity, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(false);
        UpdateAlbumRequestHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.Handle(new UpdateAlbumRequest { Id = 1 }, CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<OperationError>().And.HaveErrorWithCode("album.update_failed");
    }
}