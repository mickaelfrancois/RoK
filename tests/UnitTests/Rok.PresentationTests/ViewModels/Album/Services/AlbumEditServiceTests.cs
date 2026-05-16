using Rok.Application.Dto;
using Rok.Application.Features.Albums.Requests;
using Rok.ViewModels.Album.Services;

namespace Rok.PresentationTests.ViewModels.Album.Services;

public class AlbumEditServiceTests
{
    private readonly FakeMediator _mediator = new();

    private AlbumEditService BuildService() => new(_mediator);

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
        UpdateAlbumFavoriteRequest sent = Assert.Single(_mediator.Sent<UpdateAlbumFavoriteRequest>());
        Assert.Equal(5, sent.Id);
        Assert.True(sent.IsFavorite);
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
        UpdateAlbumTagsRequest sent = Assert.Single(_mediator.Sent<UpdateAlbumTagsRequest>());
        Assert.Equal(5, sent.Id);
        Assert.True(sent.Tags.SequenceEqual(tags));
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
        UpdateAlbumPictureDominantColorRequest sent = Assert.Single(_mediator.Sent<UpdateAlbumPictureDominantColorRequest>());
        Assert.Equal(5, sent.Id);
        Assert.Equal(colorValue, sent.ColorValue);
    }
}