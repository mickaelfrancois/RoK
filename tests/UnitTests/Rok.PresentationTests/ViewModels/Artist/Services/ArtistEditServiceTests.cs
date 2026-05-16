using Microsoft.Extensions.Logging.Abstractions;
using Rok.Application.Dto;
using Rok.Application.Features.Artists.Requests;
using Rok.ViewModels.Artist.Services;

namespace Rok.PresentationTests.ViewModels.Artist.Services;

public class ArtistEditServiceTests
{
    private readonly FakeMediator _mediator = new();

    private ArtistEditService BuildService() => new(_mediator, NullLogger<ArtistEditService>.Instance);

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
        UpdateArtistFavoriteRequest sent = Assert.Single(_mediator.Sent<UpdateArtistFavoriteRequest>());
        Assert.Equal(5, sent.Id);
        Assert.True(sent.IsFavorite);
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
        UpdateArtistTagsRequest sent = Assert.Single(_mediator.Sent<UpdateArtistTagsRequest>());
        Assert.Equal(5, sent.Id);
        Assert.True(sent.Tags.SequenceEqual(tags));
    }

    [Fact(DisplayName = "UpdatePictureDominantColorAsync should send the color command")]
    public async Task UpdatePictureDominantColorAsync_ShouldSendCommand()
    {
        // Arrange
        ArtistEditService sut = BuildService();

        // Act
        await sut.UpdatePictureDominantColorAsync(id: 5, colorValue: 0xFF112233);

        // Assert
        UpdateArtistPictureDominantColorRequest sent = Assert.Single(_mediator.Sent<UpdateArtistPictureDominantColorRequest>());
        Assert.Equal(5, sent.Id);
        Assert.Equal(0xFF112233, sent.ColorValue);
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