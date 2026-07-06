using Moq;
using Rok.Application.Features.Artists.Requests;
using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Repositories;

namespace Rok.ApplicationTests.Features.Artists.Requests;

public class CreateArtistRequestHandlerTests
{
    [Fact(DisplayName = "Handle should return new id when artist is created")]
    public async Task Handle_ShouldReturnNewId_WhenArtistIsCreated()
    {
        // Arrange
        Mock<IArtistRepository> repository = new();
        repository.Setup(r => r.AddAsync(It.IsAny<ArtistEntity>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(42);
        CreateArtistRequestHandler handler = new(repository.Object);

        // Act
        Result<long> result = await handler.Handle(new CreateArtistRequest { Name = "Queen" }, CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.Equal(42, result.Value);
    }

    [Fact(DisplayName = "Handle should return failure when repository returns non-positive id")]
    public async Task Handle_ShouldReturnFailure_WhenRepositoryReturnsNonPositiveId()
    {
        // Arrange
        Mock<IArtistRepository> repository = new();
        repository.Setup(r => r.AddAsync(It.IsAny<ArtistEntity>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(0);
        CreateArtistRequestHandler handler = new(repository.Object);

        // Act
        Result<long> result = await handler.Handle(new CreateArtistRequest { Name = "Unknown" }, CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<OperationError>().And.HaveErrorWithCode("artist.create_failed");
    }
}

public class UpdateArtistRequestHandlerTests
{
    [Fact(DisplayName = "Handle should apply fields to existing artist and persist")]
    public async Task Handle_ShouldApplyFields_ToExistingArtist_AndPersist()
    {
        // Arrange
        ArtistEntity entity = new() { Id = 1, Name = "Old", Biography = "old bio" };
        Mock<IArtistRepository> repository = new();
        repository.Setup(r => r.GetByIdAsync(1, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(entity);
        repository.Setup(r => r.UpdateAsync(entity, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(true);
        UpdateArtistRequestHandler handler = new(repository.Object);

        UpdateArtistRequest command = new() { Id = 1, WikipediaUrl = "wiki", Biography = "new bio", Disbanded = true };

        // Act
        Result<bool> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.Equal("wiki", entity.WikipediaUrl);
        Assert.Equal("new bio", entity.Biography);
        Assert.True(entity.Disbanded);
    }

    [Fact(DisplayName = "Handle should clear biography when command biography is blank")]
    public async Task Handle_ShouldClearBiography_WhenCommandBiographyIsBlank()
    {
        // Arrange
        ArtistEntity entity = new() { Id = 1, Biography = "original bio" };
        Mock<IArtistRepository> repository = new();
        repository.Setup(r => r.GetByIdAsync(1, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(entity);
        repository.Setup(r => r.UpdateAsync(entity, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(true);
        UpdateArtistRequestHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.Handle(new UpdateArtistRequest { Id = 1, Biography = "  " }, CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.Equal("  ", entity.Biography);
    }

    [Fact(DisplayName = "Handle should return failure when artist is not found")]
    public async Task Handle_ShouldReturnFailure_WhenArtistNotFound()
    {
        // Arrange
        Mock<IArtistRepository> repository = new();
        repository.Setup(r => r.GetByIdAsync(It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync((ArtistEntity?)null);
        UpdateArtistRequestHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.Handle(new UpdateArtistRequest { Id = 99 }, CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<NotFoundError>().And.HaveErrorWithCode("artist.not_found");
    }
}

public class DeleteArtistRequestHandlerTests
{
    [Fact(DisplayName = "Handle should return success when repository deletes artist")]
    public async Task Handle_ShouldReturnSuccess_WhenRepositoryDeletesArtist()
    {
        // Arrange
        Mock<IArtistRepository> repository = new();
        repository.Setup(r => r.DeleteAsync(It.Is<ArtistEntity>(a => a.Id == 5), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(true);
        DeleteArtistRequestHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.Handle(new DeleteArtistRequest { Id = 5 }, CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
    }

    [Fact(DisplayName = "Handle should return failure when repository fails to delete artist")]
    public async Task Handle_ShouldReturnFailure_WhenRepositoryFailsToDelete()
    {
        // Arrange
        Mock<IArtistRepository> repository = new();
        repository.Setup(r => r.DeleteAsync(It.IsAny<ArtistEntity>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(false);
        DeleteArtistRequestHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.Handle(new DeleteArtistRequest { Id = 5 }, CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<OperationError>().And.HaveErrorWithCode("artist.delete_failed");
    }
}

public class UpdateArtistFavoriteRequestHandlerTests
{
    [Fact(DisplayName = "Handle should return success when repository updates favorite")]
    public async Task Handle_ShouldReturnSuccess_WhenRepositoryUpdatesFavorite()
    {
        // Arrange
        Mock<IArtistRepository> repository = new();
        repository.Setup(r => r.UpdateFavoriteAsync(1, false, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(true);
        UpdateArtistFavoriteRequestHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.Handle(new UpdateArtistFavoriteRequest(1, false), CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
    }

    [Fact(DisplayName = "Handle should return failure when repository fails to update favorite")]
    public async Task Handle_ShouldReturnFailure_WhenRepositoryFails()
    {
        // Arrange
        Mock<IArtistRepository> repository = new();
        repository.Setup(r => r.UpdateFavoriteAsync(It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(false);
        UpdateArtistFavoriteRequestHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.Handle(new UpdateArtistFavoriteRequest(1, true), CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<OperationError>().And.HaveErrorWithCode("artist.favorite_update_failed");
    }
}

public class ResetArtistListenCountRequestHandlerTests
{
    [Fact(DisplayName = "Handle should return success when repository resets listen count")]
    public async Task Handle_ShouldReturnSuccess_WhenRepositoryResetsListenCount()
    {
        // Arrange
        Mock<IArtistRepository> repository = new();
        repository.Setup(r => r.ResetListenCountAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(true);
        ResetArtistListenCountRequestHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.Handle(new ResetArtistListenCountRequest(), CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
    }

    [Fact(DisplayName = "Handle should return failure when repository fails to reset listen count")]
    public async Task Handle_ShouldReturnFailure_WhenRepositoryFails()
    {
        // Arrange
        Mock<IArtistRepository> repository = new();
        repository.Setup(r => r.ResetListenCountAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(false);
        ResetArtistListenCountRequestHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.Handle(new ResetArtistListenCountRequest(), CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<OperationError>().And.HaveErrorWithCode("artist.listen_count_reset_failed");
    }
}

public class UpdateArtistLastListenRequestHandlerTests
{
    [Fact(DisplayName = "Handle should return success when repository updates last listen")]
    public async Task Handle_ShouldReturnSuccess_WhenRepositoryUpdatesLastListen()
    {
        // Arrange
        Mock<IArtistRepository> repository = new();
        repository.Setup(r => r.UpdateLastListenAsync(1, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(true);
        UpdateArtistLastListenRequestHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.Handle(new UpdateArtistLastListenRequest(1), CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
    }

    [Fact(DisplayName = "Handle should return failure when repository fails to update last listen")]
    public async Task Handle_ShouldReturnFailure_WhenRepositoryFails()
    {
        // Arrange
        Mock<IArtistRepository> repository = new();
        repository.Setup(r => r.UpdateLastListenAsync(It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(false);
        UpdateArtistLastListenRequestHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.Handle(new UpdateArtistLastListenRequest(1), CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<OperationError>().And.HaveErrorWithCode("artist.last_listen_update_failed");
    }
}

public class UpdateArtistPictureDominantColorRequestHandlerTests
{
    [Fact(DisplayName = "Handle should forward color value to repository")]
    public async Task Handle_ShouldForwardColorValue_ToRepository()
    {
        // Arrange
        Mock<IArtistRepository> repository = new();
        repository.Setup(r => r.UpdatePictureDominantColorAsync(1, 123L, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(true);
        UpdateArtistPictureDominantColorRequestHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.Handle(new UpdateArtistPictureDominantColorRequest(1, 123L), CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
    }

    [Fact(DisplayName = "Handle should return failure when repository fails to update dominant color")]
    public async Task Handle_ShouldReturnFailure_WhenRepositoryFails()
    {
        // Arrange
        Mock<IArtistRepository> repository = new();
        repository.Setup(r => r.UpdatePictureDominantColorAsync(It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(false);
        UpdateArtistPictureDominantColorRequestHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.Handle(new UpdateArtistPictureDominantColorRequest(1, null), CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<OperationError>().And.HaveErrorWithCode("artist.picture_dominant_color_update_failed");
    }
}

public class UpdateArtistStatisticsRequestHandlerTests
{
    [Fact(DisplayName = "Handle should forward statistics to repository")]
    public async Task Handle_ShouldForwardStatistics_ToRepository()
    {
        // Arrange
        Mock<IArtistRepository> repository = new();
        repository.Setup(r => r.UpdateStatisticsAsync(1, 10, 3600L, 2, 0, 1, 0, 1990, 2000, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(true);
        UpdateArtistStatisticsRequestHandler handler = new(repository.Object);
        UpdateArtistStatisticsRequest command = new(1)
        {
            TrackCount = 10,
            TotalDurationSeconds = 3600,
            AlbumCount = 2,
            LiveCount = 1,
            YearMini = 1990,
            YearMaxi = 2000
        };

        // Act
        Result<bool> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
    }

    [Fact(DisplayName = "Handle should normalize zero year bounds to null before saving")]
    public async Task Handle_ShouldNormalizeZeroYearBounds_ToNull_BeforeSaving()
    {
        // Arrange
        Mock<IArtistRepository> repository = new();
        repository.Setup(r => r.UpdateStatisticsAsync(1, 0, 0L, 0, 0, 0, 0, null, null, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(true);
        UpdateArtistStatisticsRequestHandler handler = new(repository.Object);
        UpdateArtistStatisticsRequest command = new(1) { YearMini = 0, YearMaxi = 0 };

        // Act
        Result<bool> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.Null(command.YearMini);
        Assert.Null(command.YearMaxi);
    }

    [Fact(DisplayName = "Handle should return failure when repository fails to update statistics")]
    public async Task Handle_ShouldReturnFailure_WhenRepositoryFails()
    {
        // Arrange
        Mock<IArtistRepository> repository = new();
        repository.Setup(r => r.UpdateStatisticsAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(false);
        UpdateArtistStatisticsRequestHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.Handle(new UpdateArtistStatisticsRequest(1), CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<OperationError>().And.HaveErrorWithCode("artist.statistics_update_failed");
    }
}

public class UpdateArtistGetMetaDataLastAttemptRequestHandlerTests
{
    [Fact(DisplayName = "Handle should return success when repository updates metadata timestamp")]
    public async Task Handle_ShouldReturnSuccess_WhenRepositoryUpdatesTimestamp()
    {
        // Arrange
        Mock<IArtistRepository> repository = new();
        repository.Setup(r => r.UpdateGetMetaDataLastAttemptAsync(1, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(true);
        UpdateArtistGetMetaDataLastAttemptRequestHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.Handle(new UpdateArtistGetMetaDataLastAttemptRequest(1), CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
    }

    [Fact(DisplayName = "Handle should return failure when repository fails to update metadata timestamp")]
    public async Task Handle_ShouldReturnFailure_WhenRepositoryFails()
    {
        // Arrange
        Mock<IArtistRepository> repository = new();
        repository.Setup(r => r.UpdateGetMetaDataLastAttemptAsync(It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(false);
        UpdateArtistGetMetaDataLastAttemptRequestHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.Handle(new UpdateArtistGetMetaDataLastAttemptRequest(1), CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<OperationError>().And.HaveErrorWithCode("artist.meta_attempt_update_failed");
    }
}

public class UpdateArtistTagsRequestHandlerTests
{
    [Fact(DisplayName = "Handle should pass artist tag context to tag repository")]
    public async Task Handle_ShouldPassArtistTagContext_ToTagRepository()
    {
        // Arrange
        Mock<ITagRepository> repository = new();
        IEnumerable<string> tags = new[] { "indie", "alt" };
        repository.Setup(r => r.UpdateEntityTagsAsync(1, tags, "artisttags", "artistid")).ReturnsAsync(true);
        UpdateArtistTagsRequestHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.Handle(new UpdateArtistTagsRequest(1, tags), CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        repository.Verify(r => r.UpdateEntityTagsAsync(1, tags, "artisttags", "artistid"), Times.Once);
    }

    [Fact(DisplayName = "Handle should return failure when tag repository fails to update tags")]
    public async Task Handle_ShouldReturnFailure_WhenTagRepositoryFails()
    {
        // Arrange
        Mock<ITagRepository> repository = new();
        repository.Setup(r => r.UpdateEntityTagsAsync(It.IsAny<long>(), It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(false);
        UpdateArtistTagsRequestHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.Handle(new UpdateArtistTagsRequest(1, Array.Empty<string>()), CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<OperationError>().And.HaveErrorWithCode("artist.tags_update_failed");
    }
}