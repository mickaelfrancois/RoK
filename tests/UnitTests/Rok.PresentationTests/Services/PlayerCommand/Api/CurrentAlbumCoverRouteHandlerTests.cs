using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Dto;
using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Pictures;
using Rok.Application.Player;
using Rok.Services.PlayerCommand.Api;

namespace Rok.PresentationTests.Services.PlayerCommand.Api;

public class CurrentAlbumCoverRouteHandlerTests
{
    private readonly Mock<IPlayerService> _playerService = new();
    private readonly Mock<IAlbumPicture> _albumPicture = new();
    private readonly Mock<IFileSystem> _fileSystem = new();

    private CurrentAlbumCoverRouteHandler BuildHandler() =>
        new(_playerService.Object, _albumPicture.Object, _fileSystem.Object, action => action(),
            NullLogger<CurrentAlbumCoverRouteHandler>.Instance);

    private static TrackDto TrackWithFile(string musicFile) =>
        new() { Id = 1, MusicFile = musicFile, ArtistName = "Artist", AlbumName = "Album" };

    [Theory(DisplayName = "CanHandle should accept only GET on the album cover route")]
    [InlineData("GET", "/current/album-cover", true)]
    [InlineData("POST", "/current/album-cover", false)]
    [InlineData("GET", "/current/artist-image", false)]
    [InlineData("GET", "/current", false)]
    public void CanHandle_ShouldAcceptOnlyMatchingMethodAndRoute(string method, string path, bool expected)
    {
        // Arrange
        CurrentAlbumCoverRouteHandler sut = BuildHandler();

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
        CurrentAlbumCoverRouteHandler sut = BuildHandler();

        // Act
        WebApiResult result = await sut.HandleAsync("/current/album-cover");

        // Assert
        Assert.Equal(404, result.StatusCode);
        _fileSystem.Verify(f => f.ReadAllBytesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "HandleAsync should return NotFound when the current track has no music file")]
    public async Task HandleAsync_ShouldReturnNotFound_WhenMusicFileEmpty()
    {
        // Arrange
        _playerService.Setup(p => p.CurrentTrack).Returns(TrackWithFile(string.Empty));
        CurrentAlbumCoverRouteHandler sut = BuildHandler();

        // Act
        WebApiResult result = await sut.HandleAsync("/current/album-cover");

        // Assert
        Assert.Equal(404, result.StatusCode);
    }

    [Fact(DisplayName = "HandleAsync should return NotFound when no cover file exists")]
    public async Task HandleAsync_ShouldReturnNotFound_WhenCoverMissing()
    {
        // Arrange
        _playerService.Setup(p => p.CurrentTrack).Returns(TrackWithFile(@"C:\Music\Album\track.mp3"));
        _albumPicture.Setup(a => a.PictureFileExists(It.IsAny<string>())).Returns(false);
        CurrentAlbumCoverRouteHandler sut = BuildHandler();

        // Act
        WebApiResult result = await sut.HandleAsync("/current/album-cover");

        // Assert
        Assert.Equal(404, result.StatusCode);
        _fileSystem.Verify(f => f.ReadAllBytesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "HandleAsync should return the cover bytes when a local track has a cover")]
    public async Task HandleAsync_ShouldReturnBytes_WhenCoverExists()
    {
        // Arrange
        byte[] bytes = [1, 2, 3, 4];
        _playerService.Setup(p => p.CurrentTrack).Returns(TrackWithFile(@"C:\Music\Album\track.mp3"));
        _albumPicture.Setup(a => a.PictureFileExists(@"C:\Music\Album")).Returns(true);
        _albumPicture.Setup(a => a.GetPictureFile(@"C:\Music\Album")).Returns(@"C:\Music\Album\cover.png");
        _fileSystem.Setup(f => f.ReadAllBytesAsync(@"C:\Music\Album\cover.png", It.IsAny<CancellationToken>()))
            .ReturnsAsync(bytes);
        CurrentAlbumCoverRouteHandler sut = BuildHandler();

        // Act
        WebApiResult result = await sut.HandleAsync("/current/album-cover");

        // Assert
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(bytes, result.BinaryBody);
        Assert.Equal("image/png", result.ContentType);
    }

    [Theory(DisplayName = "HandleAsync should derive the content type from the cover extension")]
    [InlineData("cover.jpg", "image/jpeg")]
    [InlineData("cover.png", "image/png")]
    [InlineData("cover.webp", "image/webp")]
    public async Task HandleAsync_ShouldDeriveContentType(string coverFile, string expectedContentType)
    {
        // Arrange
        string coverPath = @"C:\Music\Album\" + coverFile;
        _playerService.Setup(p => p.CurrentTrack).Returns(TrackWithFile(@"C:\Music\Album\track.mp3"));
        _albumPicture.Setup(a => a.PictureFileExists(It.IsAny<string>())).Returns(true);
        _albumPicture.Setup(a => a.GetPictureFile(It.IsAny<string>())).Returns(coverPath);
        _fileSystem.Setup(f => f.ReadAllBytesAsync(coverPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync([9]);
        CurrentAlbumCoverRouteHandler sut = BuildHandler();

        // Act
        WebApiResult result = await sut.HandleAsync("/current/album-cover");

        // Assert
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expectedContentType, result.ContentType);
    }
}