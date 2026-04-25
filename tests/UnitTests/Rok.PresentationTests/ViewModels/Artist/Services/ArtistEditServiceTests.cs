using MiF.Mediator.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Dto;
using Rok.Application.Features.Artists.Command;
using Rok.ViewModels.Artist.Services;

namespace Rok.PresentationTests.ViewModels.Artist.Services;

public class ArtistEditServiceTests
{
    private readonly Mock<IMediator> _mediator = new();

    private ArtistEditService BuildService() => new(_mediator.Object, NullLogger<ArtistEditService>.Instance);

    [Fact(DisplayName = "UpdateFavoriteAsync should send the favorite command and update the artist state")]
    public async Task UpdateFavoriteAsync_ShouldSendCommandAndUpdateState()
    {
        // Arrange
        ArtistDto artist = new() { Id = 5, IsFavorite = false };
        ArtistEditService sut = BuildService();

        // Act
        await sut.UpdateFavoriteAsync(artist, isFavorite: true);

        // Assert
        Assert.True(artist.IsFavorite);
        _mediator.Verify(m => m.SendMessageAsync(
            It.Is<UpdateArtistFavoriteCommand>(c => c.Id == 5 && c.IsFavorite == true),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "UpdateTagsAsync should send the tags command for the given artist")]
    public async Task UpdateTagsAsync_ShouldSendTagsCommand()
    {
        // Arrange
        ArtistEditService sut = BuildService();
        string[] tags = new[] { "rock", "indie" };

        // Act
        await sut.UpdateTagsAsync(id: 5, tags);

        // Assert
        _mediator.Verify(m => m.SendMessageAsync(
            It.Is<UpdateArtistTagsCommand>(c => c.Id == 5 && c.Tags.SequenceEqual(tags)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "UpdatePictureDominantColorAsync should send the color command")]
    public async Task UpdatePictureDominantColorAsync_ShouldSendCommand()
    {
        // Arrange
        ArtistEditService sut = BuildService();

        // Act
        await sut.UpdatePictureDominantColorAsync(id: 5, colorValue: 0xFF112233);

        // Assert
        _mediator.Verify(m => m.SendMessageAsync(
            It.Is<UpdateArtistPictureDominantColorCommand>(c => c.Id == 5 && c.ColorValue == 0xFF112233),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory(DisplayName = "OpenOfficialSiteAsync should return false when no URL is available on the artist")]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData(" ", " ")]
    public async Task OpenOfficialSiteAsync_ShouldReturnFalse_WhenNoUrl(string? officialSite, string? wikipedia)
    {
        // Arrange
        ArtistDto artist = new() { Id = 1, Name = "X", OfficialSiteUrl = officialSite!, WikipediaUrl = wikipedia! };
        ArtistEditService sut = BuildService();

        // Act
        bool result = await sut.OpenOfficialSiteAsync(artist);

        // Assert
        Assert.False(result);
    }
}
