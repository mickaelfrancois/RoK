using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Dto;
using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Pictures;
using Rok.Application.Player;
using Rok.Services.PlayerCommand.Api;

namespace Rok.PresentationTests.Services.PlayerCommand.Api;

public class CurrentArtistImageRouteHandlerTests
{
    private readonly Mock<IPlayerService> _playerService = new();
    private readonly Mock<IArtistPicture> _artistPicture = new();
    private readonly Mock<IFileSystem> _fileSystem = new();

    private CurrentArtistImageRouteHandler BuildHandler() =>
        new(_playerService.Object, _artistPicture.Object, _fileSystem.Object, action => action(),
            NullLogger<CurrentArtistImageRouteHandler>.Instance);

    private static TrackDto TrackWithArtist(string artistName) =>
        new() { Id = 1, ArtistName = artistName, MusicFile = @"C:\Music\Album\track.mp3" };

    [Theory(DisplayName = "CanHandle should accept only GET on the artist image route")]
    [InlineData("GET", "/current/artist-image", true)]
    [InlineData("POST", "/current/artist-image", false)]
    [InlineData("GET", "/current/album-cover", false)]
    [InlineData("GET", "/current", false)]
    public void CanHandle_ShouldAcceptOnlyMatchingMethodAndRoute(string method, string path, bool expected)
    {
        // Arrange
        CurrentArtistImageRouteHandler sut = BuildHandler();

        // Act
        bool result = sut.CanHandle(method, path);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact(DisplayName = "HandleAsync should return NotFound when nothing is playing")]
    public async Task HandleAsync_ShouldReturnNotFound_WhenNoTrack()
    {
        // Arrange
        _playerService.Setup(p => p.CurrentTrack).Returns((TrackDto?)null);
        CurrentArtistImageRouteHandler sut = BuildHandler();

        // Act
        WebApiResult result = await sut.HandleAsync("/current/artist-image");

        // Assert
        Assert.Equal(404, result.StatusCode);
        _fileSystem.Verify(f => f.ReadAllBytesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "HandleAsync should return NotFound when the current track has no artist name")]
    public async Task HandleAsync_ShouldReturnNotFound_WhenArtistEmpty()
    {
        // Arrange
        _playerService.Setup(p => p.CurrentTrack).Returns(TrackWithArtist(string.Empty));
        CurrentArtistImageRouteHandler sut = BuildHandler();

        // Act
        WebApiResult result = await sut.HandleAsync("/current/artist-image");

        // Assert
        Assert.Equal(404, result.StatusCode);
    }

    [Fact(DisplayName = "HandleAsync should return NotFound when no artist image exists")]
    public async Task HandleAsync_ShouldReturnNotFound_WhenImageMissing()
    {
        // Arrange
        _playerService.Setup(p => p.CurrentTrack).Returns(TrackWithArtist("Madonna"));
        _artistPicture.Setup(a => a.PictureFileExists("Madonna")).Returns(false);
        CurrentArtistImageRouteHandler sut = BuildHandler();

        // Act
        WebApiResult result = await sut.HandleAsync("/current/artist-image");

        // Assert
        Assert.Equal(404, result.StatusCode);
        _fileSystem.Verify(f => f.ReadAllBytesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "HandleAsync should return the artist image bytes when it exists in cache")]
    public async Task HandleAsync_ShouldReturnBytes_WhenImageExists()
    {
        // Arrange
        byte[] bytes = [5, 6, 7];
        _playerService.Setup(p => p.CurrentTrack).Returns(TrackWithArtist("Madonna"));
        _artistPicture.Setup(a => a.PictureFileExists("Madonna")).Returns(true);
        _artistPicture.Setup(a => a.GetPictureFile("Madonna")).Returns(@"C:\Cache\@Artists\madonna\artist.jpg");
        _fileSystem.Setup(f => f.ReadAllBytesAsync(@"C:\Cache\@Artists\madonna\artist.jpg", It.IsAny<CancellationToken>()))
            .ReturnsAsync(bytes);
        CurrentArtistImageRouteHandler sut = BuildHandler();

        // Act
        WebApiResult result = await sut.HandleAsync("/current/artist-image");

        // Assert
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(bytes, result.BinaryBody);
        Assert.Equal("image/jpeg", result.ContentType);
    }
}