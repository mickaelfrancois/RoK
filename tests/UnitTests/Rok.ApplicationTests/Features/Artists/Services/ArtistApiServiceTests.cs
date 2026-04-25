using MiF.Mediator.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Dto.MusicDataApi;
using Rok.Application.Features.Artists.Command;
using Rok.Application.Features.Artists.Services;
using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Pictures;

namespace Rok.ApplicationTests.Features.Artists.Services;

public class ArtistApiServiceTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IMusicDataApiService> _musicData = new();
    private readonly Mock<IArtistPictureService> _pictureService = new();
    private readonly Mock<IBackdropPicture> _backdropPicture = new();

    private ArtistApiService BuildService() => new(_mediator.Object, _musicData.Object, NullLogger<ArtistApiService>.Instance);

    [Fact(DisplayName = "GetAndUpdateArtistDataAsync should return false when artist name is empty")]
    public async Task GetAndUpdateArtistDataAsync_ShouldReturnFalse_WhenNameMissing()
    {
        // Arrange
        ArtistDto artist = new() { Id = 1, Name = "" };
        ArtistApiService sut = BuildService();

        // Act
        bool result = await sut.GetAndUpdateArtistDataAsync(artist, _pictureService.Object, _backdropPicture.Object);

        // Assert
        Assert.False(result);
        _musicData.Verify(m => m.GetArtistAsync(It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact(DisplayName = "GetAndUpdateArtistDataAsync should return false when API retry is not allowed")]
    public async Task GetAndUpdateArtistDataAsync_ShouldReturnFalse_WhenRetryNotAllowed()
    {
        // Arrange
        ArtistDto artist = new() { Id = 1, Name = "Beatles" };
        _musicData.Setup(m => m.IsApiRetryAllowed(It.IsAny<DateTime?>())).Returns(false);
        ArtistApiService sut = BuildService();

        // Act
        bool result = await sut.GetAndUpdateArtistDataAsync(artist, _pictureService.Object, _backdropPicture.Object);

        // Assert
        Assert.False(result);
        _musicData.Verify(m => m.GetArtistAsync(It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact(DisplayName = "GetAndUpdateArtistDataAsync should update last-attempt timestamp before calling the API")]
    public async Task GetAndUpdateArtistDataAsync_ShouldUpdateLastAttempt_BeforeApiCall()
    {
        // Arrange
        ArtistDto artist = new() { Id = 1, Name = "Beatles" };
        _musicData.Setup(m => m.IsApiRetryAllowed(It.IsAny<DateTime?>())).Returns(true);
        _musicData.Setup(m => m.GetArtistAsync(It.IsAny<string>(), It.IsAny<string?>())).ReturnsAsync((MusicDataArtistDto?)null);
        ArtistApiService sut = BuildService();

        // Act
        await sut.GetAndUpdateArtistDataAsync(artist, _pictureService.Object, _backdropPicture.Object);

        // Assert
        _mediator.Verify(m => m.SendMessageAsync(It.Is<UpdateArtistGetMetaDataLastAttemptCommand>(c => c.ArtistId == 1), It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(artist.GetMetaDataLastAttempt);
    }

    [Fact(DisplayName = "GetAndUpdateArtistDataAsync should return false when the API returns null")]
    public async Task GetAndUpdateArtistDataAsync_ShouldReturnFalse_WhenApiReturnsNull()
    {
        // Arrange
        ArtistDto artist = new() { Id = 1, Name = "Beatles" };
        _musicData.Setup(m => m.IsApiRetryAllowed(It.IsAny<DateTime?>())).Returns(true);
        _musicData.Setup(m => m.GetArtistAsync(It.IsAny<string>(), It.IsAny<string?>())).ReturnsAsync((MusicDataArtistDto?)null);
        ArtistApiService sut = BuildService();

        // Act
        bool result = await sut.GetAndUpdateArtistDataAsync(artist, _pictureService.Object, _backdropPicture.Object);

        // Assert
        Assert.False(result);
    }

    [Fact(DisplayName = "GetAndUpdateArtistDataAsync should return false when the API response has no MusicBrainz id")]
    public async Task GetAndUpdateArtistDataAsync_ShouldReturnFalse_WhenApiResponseHasNoMbid()
    {
        // Arrange
        ArtistDto artist = new() { Id = 1, Name = "Beatles" };
        _musicData.Setup(m => m.IsApiRetryAllowed(It.IsAny<DateTime?>())).Returns(true);
        _musicData.Setup(m => m.GetArtistAsync(It.IsAny<string>(), It.IsAny<string?>()))
                  .ReturnsAsync(new MusicDataArtistDto { MusicBrainzID = string.Empty });
        ArtistApiService sut = BuildService();

        // Act
        bool result = await sut.GetAndUpdateArtistDataAsync(artist, _pictureService.Object, _backdropPicture.Object);

        // Assert
        Assert.False(result);
    }
}
