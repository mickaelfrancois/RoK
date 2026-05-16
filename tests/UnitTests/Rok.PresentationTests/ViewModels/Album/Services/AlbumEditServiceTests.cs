using Moq;
using Rok.Application.Dto;
using Rok.Application.Features.Albums.Requests;
using Rok.ViewModels.Album.Services;

namespace Rok.PresentationTests.ViewModels.Album.Services;

public class AlbumEditServiceTests
{
    private readonly Mock<IMediator> _mediator = new();

    private AlbumEditService BuildService() => new(_mediator.Object);

    [Fact(DisplayName = "UpdateFavoriteAsync should send the favorite command and update the album state")]
    public async Task UpdateFavoriteAsync_ShouldSendCommandAndUpdateState()
    {
        // Arrange
        AlbumDto album = new() { Id = 5, IsFavorite = false };
        AlbumEditService sut = BuildService();

        // Act
        await sut.UpdateFavoriteAsync(album, isFavorite: true);

        // Assert
        Assert.True(album.IsFavorite);
        _mediator.Verify(m => m.Send(
            It.Is<UpdateAlbumFavoriteRequest>(c => c.Id == 5 && c.IsFavorite == true),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "UpdateTagsAsync should send the tags command for the given album")]
    public async Task UpdateTagsAsync_ShouldSendTagsCommand()
    {
        // Arrange
        AlbumEditService sut = BuildService();
        string[] tags = new[] { "rock", "live" };

        // Act
        await sut.UpdateTagsAsync(id: 5, tags);

        // Assert
        _mediator.Verify(m => m.Send(
            It.Is<UpdateAlbumTagsRequest>(c => c.Id == 5 && c.Tags.SequenceEqual(tags)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory(DisplayName = "UpdatePictureDominantColorAsync should send the color command including null values")]
    [InlineData(0xFF112233L)]
    [InlineData(null)]
    public async Task UpdatePictureDominantColorAsync_ShouldSendCommand(long? colorValue)
    {
        // Arrange
        AlbumEditService sut = BuildService();

        // Act
        await sut.UpdatePictureDominantColorAsync(id: 5, colorValue);

        // Assert
        _mediator.Verify(m => m.Send(
            It.Is<UpdateAlbumPictureDominantColorRequest>(c => c.Id == 5 && c.ColorValue == colorValue),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
