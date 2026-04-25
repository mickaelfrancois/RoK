using MiF.Mediator.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Dto.MusicDataApi;
using Rok.Application.Features.Albums.Command;
using Rok.Application.Features.Albums.Services;
using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Pictures;

namespace Rok.ApplicationTests.Features.Albums.Services;

public class AlbumApiServiceTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IMusicDataApiService> _musicData = new();
    private readonly Mock<IAlbumPictureService> _pictureService = new();

    private AlbumApiService BuildService() => new(_mediator.Object, _musicData.Object, NullLogger<AlbumApiService>.Instance);

    [Theory(DisplayName = "GetAndUpdateAlbumDataAsync should return false when album name or artist name is empty")]
    [InlineData("", "Beatles")]
    [InlineData("Album", "")]
    [InlineData("", "")]
    public async Task GetAndUpdateAlbumDataAsync_ShouldReturnFalse_WhenNamesMissing(string name, string artistName)
    {
        // Arrange
        AlbumDto album = new() { Id = 1, Name = name, ArtistName = artistName };
        AlbumApiService sut = BuildService();

        // Act
        bool result = await sut.GetAndUpdateAlbumDataAsync(album, _pictureService.Object);

        // Assert
        Assert.False(result);
        _musicData.Verify(m => m.GetAlbumAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact(DisplayName = "GetAndUpdateAlbumDataAsync should return false when API retry is not allowed")]
    public async Task GetAndUpdateAlbumDataAsync_ShouldReturnFalse_WhenRetryNotAllowed()
    {
        // Arrange
        AlbumDto album = new() { Id = 1, Name = "Album", ArtistName = "Artist" };
        _musicData.Setup(m => m.IsApiRetryAllowed(It.IsAny<DateTime?>())).Returns(false);
        AlbumApiService sut = BuildService();

        // Act
        bool result = await sut.GetAndUpdateAlbumDataAsync(album, _pictureService.Object);

        // Assert
        Assert.False(result);
        _musicData.Verify(m => m.GetAlbumAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
        _mediator.Verify(m => m.SendMessageAsync(It.IsAny<UpdateAlbumGetMetaDataLastAttemptCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "GetAndUpdateAlbumDataAsync should update last-attempt timestamp before calling the API")]
    public async Task GetAndUpdateAlbumDataAsync_ShouldUpdateLastAttempt_BeforeApiCall()
    {
        // Arrange
        AlbumDto album = new() { Id = 1, Name = "Album", ArtistName = "Artist" };
        _musicData.Setup(m => m.IsApiRetryAllowed(It.IsAny<DateTime?>())).Returns(true);
        _musicData.Setup(m => m.GetAlbumAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>())).ReturnsAsync((MusicDataAlbumDto?)null);
        AlbumApiService sut = BuildService();

        // Act
        await sut.GetAndUpdateAlbumDataAsync(album, _pictureService.Object);

        // Assert
        _mediator.Verify(m => m.SendMessageAsync(It.Is<UpdateAlbumGetMetaDataLastAttemptCommand>(c => c.AlbumId == 1), It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(album.GetMetaDataLastAttempt);
    }

    [Fact(DisplayName = "GetAndUpdateAlbumDataAsync should return false when the API returns null")]
    public async Task GetAndUpdateAlbumDataAsync_ShouldReturnFalse_WhenApiReturnsNull()
    {
        // Arrange
        AlbumDto album = new() { Id = 1, Name = "Album", ArtistName = "Artist" };
        _musicData.Setup(m => m.IsApiRetryAllowed(It.IsAny<DateTime?>())).Returns(true);
        _musicData.Setup(m => m.GetAlbumAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>())).ReturnsAsync((MusicDataAlbumDto?)null);
        AlbumApiService sut = BuildService();

        // Act
        bool result = await sut.GetAndUpdateAlbumDataAsync(album, _pictureService.Object);

        // Assert
        Assert.False(result);
    }

    [Fact(DisplayName = "GetAndUpdateAlbumDataAsync should return false when the API response has no MusicBrainz id")]
    public async Task GetAndUpdateAlbumDataAsync_ShouldReturnFalse_WhenApiResponseHasNoMbid()
    {
        // Arrange
        AlbumDto album = new() { Id = 1, Name = "Album", ArtistName = "Artist" };
        _musicData.Setup(m => m.IsApiRetryAllowed(It.IsAny<DateTime?>())).Returns(true);
        _musicData.Setup(m => m.GetAlbumAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
                  .ReturnsAsync(new MusicDataAlbumDto { MusicBrainzID = string.Empty });
        AlbumApiService sut = BuildService();

        // Act
        bool result = await sut.GetAndUpdateAlbumDataAsync(album, _pictureService.Object);

        // Assert
        Assert.False(result);
    }
}
