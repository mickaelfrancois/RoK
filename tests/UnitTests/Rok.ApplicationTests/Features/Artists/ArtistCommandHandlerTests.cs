using Moq;
using Rok.Application.Features.Artists.Command;
using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Repositories;

namespace Rok.ApplicationTests.Features.Artists;

public class CreateArtistCommandHandlerTests
{
    [Fact(DisplayName = "Handle should return new id when artist is created")]
    public async Task Handle_ShouldReturnNewId_WhenArtistIsCreated()
    {
        // Arrange
        Mock<IArtistRepository> repository = new();
        repository.Setup(r => r.AddAsync(It.IsAny<ArtistEntity>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(42);
        CreateArtistCommandHandler handler = new(repository.Object);

        // Act
        Result<long> result = await handler.HandleAsync(new CreateArtistCommand { Name = "Queen" }, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact(DisplayName = "Handle should return failure when repository returns non-positive id")]
    public async Task Handle_ShouldReturnFailure_WhenRepositoryReturnsNonPositiveId()
    {
        // Arrange
        Mock<IArtistRepository> repository = new();
        repository.Setup(r => r.AddAsync(It.IsAny<ArtistEntity>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(0);
        CreateArtistCommandHandler handler = new(repository.Object);

        // Act
        Result<long> result = await handler.HandleAsync(new CreateArtistCommand { Name = "Unknown" }, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
    }
}

public class UpdateArtistCommandHandlerTests
{
    [Fact(DisplayName = "Handle should apply fields to existing artist and persist")]
    public async Task Handle_ShouldApplyFields_ToExistingArtist_AndPersist()
    {
        // Arrange
        ArtistEntity entity = new() { Id = 1, Name = "Old", Biography = "old bio" };
        Mock<IArtistRepository> repository = new();
        repository.Setup(r => r.GetByIdAsync(1, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(entity);
        repository.Setup(r => r.UpdateAsync(entity, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(true);
        UpdateArtistCommandHandler handler = new(repository.Object);

        UpdateArtistCommand command = new() { Id = 1, WikipediaUrl = "wiki", Biography = "new bio", Disbanded = true };

        // Act
        Result<bool> result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("wiki", entity.WikipediaUrl);
        Assert.Equal("new bio", entity.Biography);
        Assert.True(entity.Disbanded);
    }

    [Fact(DisplayName = "Handle should keep existing biography when new biography is blank")]
    public async Task Handle_ShouldKeepExistingBiography_WhenNewBiographyIsBlank()
    {
        // Arrange
        ArtistEntity entity = new() { Id = 1, Biography = "original bio" };
        Mock<IArtistRepository> repository = new();
        repository.Setup(r => r.GetByIdAsync(1, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(entity);
        repository.Setup(r => r.UpdateAsync(entity, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(true);
        UpdateArtistCommandHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.HandleAsync(new UpdateArtistCommand { Id = 1, Biography = "  " }, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("original bio", entity.Biography);
    }

    [Fact(DisplayName = "Handle should return failure when artist is not found")]
    public async Task Handle_ShouldReturnFailure_WhenArtistNotFound()
    {
        // Arrange
        Mock<IArtistRepository> repository = new();
        repository.Setup(r => r.GetByIdAsync(It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync((ArtistEntity?)null);
        UpdateArtistCommandHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.HandleAsync(new UpdateArtistCommand { Id = 99 }, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
    }
}

public class DeleteArtistCommandHandlerTests
{
    [Fact(DisplayName = "Handle should return success when repository deletes artist")]
    public async Task Handle_ShouldReturnSuccess_WhenRepositoryDeletesArtist()
    {
        // Arrange
        Mock<IArtistRepository> repository = new();
        repository.Setup(r => r.DeleteAsync(It.Is<ArtistEntity>(a => a.Id == 5), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(true);
        DeleteArtistCommandHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.HandleAsync(new DeleteArtistCommand { Id = 5 }, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact(DisplayName = "Handle should return failure when repository fails to delete artist")]
    public async Task Handle_ShouldReturnFailure_WhenRepositoryFailsToDelete()
    {
        // Arrange
        Mock<IArtistRepository> repository = new();
        repository.Setup(r => r.DeleteAsync(It.IsAny<ArtistEntity>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(false);
        DeleteArtistCommandHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.HandleAsync(new DeleteArtistCommand { Id = 5 }, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
    }
}

public class UpdateArtistFavoriteCommandHandlerTests
{
    [Fact(DisplayName = "Handle should return success when repository updates favorite")]
    public async Task Handle_ShouldReturnSuccess_WhenRepositoryUpdatesFavorite()
    {
        // Arrange
        Mock<IArtistRepository> repository = new();
        repository.Setup(r => r.UpdateFavoriteAsync(1, false, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(true);
        UpdateArtistFavoriteCommandHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.HandleAsync(new UpdateArtistFavoriteCommand(1, false), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact(DisplayName = "Handle should return failure when repository fails to update favorite")]
    public async Task Handle_ShouldReturnFailure_WhenRepositoryFails()
    {
        // Arrange
        Mock<IArtistRepository> repository = new();
        repository.Setup(r => r.UpdateFavoriteAsync(It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(false);
        UpdateArtistFavoriteCommandHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.HandleAsync(new UpdateArtistFavoriteCommand(1, true), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
    }
}

public class ResetArtistListenCountCommandHandlerTests
{
    [Fact(DisplayName = "Handle should return success when repository resets listen count")]
    public async Task Handle_ShouldReturnSuccess_WhenRepositoryResetsListenCount()
    {
        // Arrange
        Mock<IArtistRepository> repository = new();
        repository.Setup(r => r.ResetListenCountAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(true);
        ResetArtistListenCountCommandHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.HandleAsync(new ResetArtistListenCountCommand(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact(DisplayName = "Handle should return failure when repository fails to reset listen count")]
    public async Task Handle_ShouldReturnFailure_WhenRepositoryFails()
    {
        // Arrange
        Mock<IArtistRepository> repository = new();
        repository.Setup(r => r.ResetListenCountAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(false);
        ResetArtistListenCountCommandHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.HandleAsync(new ResetArtistListenCountCommand(), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
    }
}

public class UpdateArtistLastListenCommandHandlerTests
{
    [Fact(DisplayName = "Handle should return success when repository updates last listen")]
    public async Task Handle_ShouldReturnSuccess_WhenRepositoryUpdatesLastListen()
    {
        // Arrange
        Mock<IArtistRepository> repository = new();
        repository.Setup(r => r.UpdateLastListenAsync(1, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(true);
        UpdateArtistLastListenCommandHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.HandleAsync(new UpdateArtistLastListenCommand(1), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact(DisplayName = "Handle should return failure when repository fails to update last listen")]
    public async Task Handle_ShouldReturnFailure_WhenRepositoryFails()
    {
        // Arrange
        Mock<IArtistRepository> repository = new();
        repository.Setup(r => r.UpdateLastListenAsync(It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(false);
        UpdateArtistLastListenCommandHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.HandleAsync(new UpdateArtistLastListenCommand(1), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
    }
}

public class UpdateArtistPictureDominantColorCommandHandlerTests
{
    [Fact(DisplayName = "Handle should forward color value to repository")]
    public async Task Handle_ShouldForwardColorValue_ToRepository()
    {
        // Arrange
        Mock<IArtistRepository> repository = new();
        repository.Setup(r => r.UpdatePictureDominantColorAsync(1, 123L, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(true);
        UpdateArtistPictureDominantColorCommandHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.HandleAsync(new UpdateArtistPictureDominantColorCommand(1, 123L), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact(DisplayName = "Handle should return failure when repository fails to update dominant color")]
    public async Task Handle_ShouldReturnFailure_WhenRepositoryFails()
    {
        // Arrange
        Mock<IArtistRepository> repository = new();
        repository.Setup(r => r.UpdatePictureDominantColorAsync(It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(false);
        UpdateArtistPictureDominantColorCommandHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.HandleAsync(new UpdateArtistPictureDominantColorCommand(1, null), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
    }
}

public class UpdateArtistStatisticsCommandHandlerTests
{
    [Fact(DisplayName = "Handle should forward statistics to repository")]
    public async Task Handle_ShouldForwardStatistics_ToRepository()
    {
        // Arrange
        Mock<IArtistRepository> repository = new();
        repository.Setup(r => r.UpdateStatisticsAsync(1, 10, 3600L, 2, 0, 1, 0, 1990, 2000, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(true);
        UpdateArtistStatisticsCommandHandler handler = new(repository.Object);
        UpdateArtistStatisticsCommand command = new(1)
        {
            TrackCount = 10,
            TotalDurationSeconds = 3600,
            AlbumCount = 2,
            LiveCount = 1,
            YearMini = 1990,
            YearMaxi = 2000
        };

        // Act
        Result<bool> result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact(DisplayName = "Handle should normalize zero year bounds to null before saving")]
    public async Task Handle_ShouldNormalizeZeroYearBounds_ToNull_BeforeSaving()
    {
        // Arrange
        Mock<IArtistRepository> repository = new();
        repository.Setup(r => r.UpdateStatisticsAsync(1, 0, 0L, 0, 0, 0, 0, null, null, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(true);
        UpdateArtistStatisticsCommandHandler handler = new(repository.Object);
        UpdateArtistStatisticsCommand command = new(1) { YearMini = 0, YearMaxi = 0 };

        // Act
        Result<bool> result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(command.YearMini);
        Assert.Null(command.YearMaxi);
    }

    [Fact(DisplayName = "Handle should return failure when repository fails to update statistics")]
    public async Task Handle_ShouldReturnFailure_WhenRepositoryFails()
    {
        // Arrange
        Mock<IArtistRepository> repository = new();
        repository.Setup(r => r.UpdateStatisticsAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(false);
        UpdateArtistStatisticsCommandHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.HandleAsync(new UpdateArtistStatisticsCommand(1), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
    }
}

public class UpdateArtistGetMetaDataLastAttemptCommandHandlerTests
{
    [Fact(DisplayName = "Handle should return success when repository updates metadata timestamp")]
    public async Task Handle_ShouldReturnSuccess_WhenRepositoryUpdatesTimestamp()
    {
        // Arrange
        Mock<IArtistRepository> repository = new();
        repository.Setup(r => r.UpdateGetMetaDataLastAttemptAsync(1, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(true);
        UpdateArtistGetMetaDataLastAttemptCommandHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.HandleAsync(new UpdateArtistGetMetaDataLastAttemptCommand(1), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact(DisplayName = "Handle should return failure when repository fails to update metadata timestamp")]
    public async Task Handle_ShouldReturnFailure_WhenRepositoryFails()
    {
        // Arrange
        Mock<IArtistRepository> repository = new();
        repository.Setup(r => r.UpdateGetMetaDataLastAttemptAsync(It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(false);
        UpdateArtistGetMetaDataLastAttemptCommandHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.HandleAsync(new UpdateArtistGetMetaDataLastAttemptCommand(1), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
    }
}

public class UpdateArtistTagsCommandHandlerTests
{
    [Fact(DisplayName = "Handle should pass artist tag context to tag repository")]
    public async Task Handle_ShouldPassArtistTagContext_ToTagRepository()
    {
        // Arrange
        Mock<ITagRepository> repository = new();
        IEnumerable<string> tags = new[] { "indie", "alt" };
        repository.Setup(r => r.UpdateEntityTagsAsync(1, tags, "artisttags", "artistid")).ReturnsAsync(true);
        UpdateArtistTagsCommandHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.HandleAsync(new UpdateArtistTagsCommand(1, tags), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        repository.Verify(r => r.UpdateEntityTagsAsync(1, tags, "artisttags", "artistid"), Times.Once);
    }

    [Fact(DisplayName = "Handle should return failure when tag repository fails to update tags")]
    public async Task Handle_ShouldReturnFailure_WhenTagRepositoryFails()
    {
        // Arrange
        Mock<ITagRepository> repository = new();
        repository.Setup(r => r.UpdateEntityTagsAsync(It.IsAny<long>(), It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(false);
        UpdateArtistTagsCommandHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.HandleAsync(new UpdateArtistTagsCommand(1, Array.Empty<string>()), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
    }
}
