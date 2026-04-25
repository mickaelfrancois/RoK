using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Services.PlayerCommand;
using Rok.Services.PlayerCommand.Api;

namespace Rok.PresentationTests.Services.PlayerCommand.Api;

public class ListenAlbumRouteHandlerTests
{
    private readonly Mock<IPlayerCommandService> _commandService = new();

    private ListenAlbumRouteHandler BuildHandler() =>
        new(_commandService.Object, action => action(), NullLogger<ListenAlbumRouteHandler>.Instance);

    [Theory(DisplayName = "CanHandle should accept GET requests on the listen album prefix only")]
    [InlineData("GET", "/listen/album/foo", true)]
    [InlineData("GET", "/listen/album/", true)]
    [InlineData("POST", "/listen/album/foo", false)]
    [InlineData("GET", "/listen/artist/foo", false)]
    [InlineData("GET", "/other", false)]
    public void CanHandle_ShouldAcceptOnlyMatchingMethodAndPath(string method, string path, bool expected)
    {
        // Arrange
        ListenAlbumRouteHandler sut = BuildHandler();

        // Act
        bool result = sut.CanHandle(method, path);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact(DisplayName = "HandleAsync should return BadRequest when album name is empty")]
    public async Task HandleAsync_ShouldReturnBadRequest_WhenEmpty()
    {
        // Arrange
        ListenAlbumRouteHandler sut = BuildHandler();

        // Act
        WebApiResult result = await sut.HandleAsync("/listen/album/");

        // Assert
        Assert.Equal(400, result.StatusCode);
    }

    [Fact(DisplayName = "HandleAsync should return Ok when ListenAlbumAsync succeeds")]
    public async Task HandleAsync_ShouldReturnOk_WhenServiceSucceeds()
    {
        // Arrange
        _commandService.Setup(c => c.ListenAlbumAsync("Best Of")).ReturnsAsync(true);
        ListenAlbumRouteHandler sut = BuildHandler();

        // Act
        WebApiResult result = await sut.HandleAsync("/listen/album/Best%20Of");

        // Assert
        Assert.Equal(200, result.StatusCode);
        _commandService.Verify(c => c.ListenAlbumAsync("Best Of"), Times.Once);
    }

    [Fact(DisplayName = "HandleAsync should return NotFound when ListenAlbumAsync returns false")]
    public async Task HandleAsync_ShouldReturnNotFound_WhenServiceReturnsFalse()
    {
        // Arrange
        _commandService.Setup(c => c.ListenAlbumAsync(It.IsAny<string>())).ReturnsAsync(false);
        ListenAlbumRouteHandler sut = BuildHandler();

        // Act
        WebApiResult result = await sut.HandleAsync("/listen/album/Missing");

        // Assert
        Assert.Equal(404, result.StatusCode);
        Assert.Contains("Missing", result.Body);
    }
}

public class ListenArtistRouteHandlerTests
{
    private readonly Mock<IPlayerCommandService> _commandService = new();

    private ListenArtistRouteHandler BuildHandler() =>
        new(_commandService.Object, action => action(), NullLogger<ListenArtistRouteHandler>.Instance);

    [Fact(DisplayName = "CanHandle should accept GET requests on the listen artist prefix")]
    public void CanHandle_ShouldAcceptArtistPrefix()
    {
        // Arrange
        ListenArtistRouteHandler sut = BuildHandler();

        // Act & Assert
        Assert.True(sut.CanHandle("GET", "/listen/artist/foo"));
        Assert.False(sut.CanHandle("GET", "/listen/album/foo"));
    }

    [Fact(DisplayName = "HandleAsync should call ListenArtistAsync with the resolved name")]
    public async Task HandleAsync_ShouldCallListenArtistAsync()
    {
        // Arrange
        _commandService.Setup(c => c.ListenArtistAsync("Beatles")).ReturnsAsync(true);
        ListenArtistRouteHandler sut = BuildHandler();

        // Act
        WebApiResult result = await sut.HandleAsync("/listen/artist/Beatles");

        // Assert
        Assert.Equal(200, result.StatusCode);
        _commandService.Verify(c => c.ListenArtistAsync("Beatles"), Times.Once);
        _commandService.Verify(c => c.ListenAlbumAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact(DisplayName = "HandleAsync should return BadRequest when artist name is empty")]
    public async Task HandleAsync_ShouldReturnBadRequest_WhenEmpty()
    {
        // Arrange
        ListenArtistRouteHandler sut = BuildHandler();

        // Act
        WebApiResult result = await sut.HandleAsync("/listen/artist/");

        // Assert
        Assert.Equal(400, result.StatusCode);
    }
}

public class ListenGenreRouteHandlerTests
{
    private readonly Mock<IPlayerCommandService> _commandService = new();

    private ListenGenreRouteHandler BuildHandler() =>
        new(_commandService.Object, action => action(), NullLogger<ListenGenreRouteHandler>.Instance);

    [Fact(DisplayName = "CanHandle should accept GET requests on the listen genre prefix")]
    public void CanHandle_ShouldAcceptGenrePrefix()
    {
        // Arrange
        ListenGenreRouteHandler sut = BuildHandler();

        // Act & Assert
        Assert.True(sut.CanHandle("GET", "/listen/genre/foo"));
        Assert.False(sut.CanHandle("GET", "/listen/album/foo"));
    }

    [Fact(DisplayName = "HandleAsync should call ListenGenreAsync with the resolved name")]
    public async Task HandleAsync_ShouldCallListenGenreAsync()
    {
        // Arrange
        _commandService.Setup(c => c.ListenGenreAsync("Rock")).ReturnsAsync(true);
        ListenGenreRouteHandler sut = BuildHandler();

        // Act
        WebApiResult result = await sut.HandleAsync("/listen/genre/Rock");

        // Assert
        Assert.Equal(200, result.StatusCode);
        _commandService.Verify(c => c.ListenGenreAsync("Rock"), Times.Once);
        _commandService.Verify(c => c.ListenAlbumAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact(DisplayName = "HandleAsync should return BadRequest when genre name is empty")]
    public async Task HandleAsync_ShouldReturnBadRequest_WhenEmpty()
    {
        // Arrange
        ListenGenreRouteHandler sut = BuildHandler();

        // Act
        WebApiResult result = await sut.HandleAsync("/listen/genre/");

        // Assert
        Assert.Equal(400, result.StatusCode);
    }
}

public class ListenPlaylistRouteHandlerTests
{
    private readonly Mock<IPlayerCommandService> _commandService = new();

    private ListenPlaylistRouteHandler BuildHandler() =>
        new(_commandService.Object, action => action(), NullLogger<ListenPlaylistRouteHandler>.Instance);

    [Fact(DisplayName = "CanHandle should accept GET requests on the listen playlist prefix")]
    public void CanHandle_ShouldAcceptPlaylistPrefix()
    {
        // Arrange
        ListenPlaylistRouteHandler sut = BuildHandler();

        // Act & Assert
        Assert.True(sut.CanHandle("GET", "/listen/playlist/MyMix"));
        Assert.False(sut.CanHandle("POST", "/listen/playlist/MyMix"));
    }

    [Fact(DisplayName = "HandleAsync should call ListenPlaylistAsync with the resolved name")]
    public async Task HandleAsync_ShouldCallListenPlaylistAsync()
    {
        // Arrange
        _commandService.Setup(c => c.ListenPlaylistAsync("MyMix")).ReturnsAsync(true);
        ListenPlaylistRouteHandler sut = BuildHandler();

        // Act
        WebApiResult result = await sut.HandleAsync("/listen/playlist/MyMix");

        // Assert
        Assert.Equal(200, result.StatusCode);
        _commandService.Verify(c => c.ListenPlaylistAsync("MyMix"), Times.Once);
    }

    [Fact(DisplayName = "HandleAsync should return NotFound when ListenPlaylistAsync returns false")]
    public async Task HandleAsync_ShouldReturnNotFound_WhenServiceReturnsFalse()
    {
        // Arrange
        _commandService.Setup(c => c.ListenPlaylistAsync(It.IsAny<string>())).ReturnsAsync(false);
        ListenPlaylistRouteHandler sut = BuildHandler();

        // Act
        WebApiResult result = await sut.HandleAsync("/listen/playlist/Missing");

        // Assert
        Assert.Equal(404, result.StatusCode);
        Assert.Contains("Missing", result.Body);
    }
}
