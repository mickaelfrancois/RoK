using Moq;
using Rok.Services.PlayerCommand;
using Rok.Services.PlayerCommand.Api;

namespace Rok.PresentationTests.Services.PlayerCommand.Api;

public class PlayerControlRouteHandlerTests
{
    private readonly Mock<IPlayerCommandService> _commandService = new();

    private PlayerControlRouteHandler BuildHandler() =>
        new(_commandService.Object, action => action());

    [Theory(DisplayName = "CanHandle should accept POST requests on the player prefix only")]
    [InlineData("POST", "/api/player/play", true)]
    [InlineData("POST", "/api/player/volume/40", true)]
    [InlineData("GET", "/api/player/play", false)]
    [InlineData("POST", "/api/playlists/1/play", false)]
    [InlineData("POST", "/play", false)]
    public void CanHandle_ShouldAcceptOnlyMatchingMethodAndPath(string method, string path, bool expected)
    {
        // Arrange
        PlayerControlRouteHandler sut = BuildHandler();

        // Act
        bool result = sut.CanHandle(method, path);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact(DisplayName = "HandleAsync should forward the toggle command")]
    public async Task HandleAsync_ShouldForwardToggle()
    {
        // Arrange
        PlayerControlRouteHandler sut = BuildHandler();

        // Act
        WebApiResult result = await sut.HandleAsync("/api/player/toggle");

        // Assert
        Assert.Equal(200, result.StatusCode);
        _commandService.Verify(c => c.Toggle(), Times.Once);
    }

    [Fact(DisplayName = "HandleAsync should forward shuffle and loop commands")]
    public async Task HandleAsync_ShouldForwardShuffleAndLoop()
    {
        // Arrange
        PlayerControlRouteHandler sut = BuildHandler();

        // Act
        WebApiResult shuffle = await sut.HandleAsync("/api/player/shuffle");
        WebApiResult loop = await sut.HandleAsync("/api/player/loop");

        // Assert
        Assert.Equal(200, shuffle.StatusCode);
        Assert.Equal(200, loop.StatusCode);
        _commandService.Verify(c => c.Shuffle(), Times.Once);
        _commandService.Verify(c => c.ToggleLoop(), Times.Once);
    }

    [Fact(DisplayName = "HandleAsync should read the volume as an invariant number")]
    public async Task HandleAsync_ShouldReadVolume()
    {
        // Arrange
        PlayerControlRouteHandler sut = BuildHandler();

        // Act
        WebApiResult result = await sut.HandleAsync("/api/player/volume/42.5");

        // Assert
        Assert.Equal(200, result.StatusCode);
        _commandService.Verify(c => c.SetVolume(42.5), Times.Once);
    }

    [Fact(DisplayName = "HandleAsync should read the seek position as an invariant number")]
    public async Task HandleAsync_ShouldReadSeekPosition()
    {
        // Arrange
        PlayerControlRouteHandler sut = BuildHandler();

        // Act
        WebApiResult result = await sut.HandleAsync("/api/player/seek/93.25");

        // Assert
        Assert.Equal(200, result.StatusCode);
        _commandService.Verify(c => c.Seek(93.25), Times.Once);
    }

    [Fact(DisplayName = "HandleAsync should play a queued track by identifier")]
    public async Task HandleAsync_ShouldPlayQueuedTrack()
    {
        // Arrange
        _commandService.Setup(c => c.PlayQueuedTrack(77)).Returns(true);
        PlayerControlRouteHandler sut = BuildHandler();

        // Act
        WebApiResult result = await sut.HandleAsync("/api/player/queue/77/play");

        // Assert
        Assert.Equal(200, result.StatusCode);
        _commandService.Verify(c => c.PlayQueuedTrack(77), Times.Once);
    }

    [Fact(DisplayName = "HandleAsync should return NotFound when the track is not queued")]
    public async Task HandleAsync_ShouldReturnNotFound_WhenTrackIsNotQueued()
    {
        // Arrange
        _commandService.Setup(c => c.PlayQueuedTrack(It.IsAny<long>())).Returns(false);
        PlayerControlRouteHandler sut = BuildHandler();

        // Act
        WebApiResult result = await sut.HandleAsync("/api/player/queue/404/play");

        // Assert
        Assert.Equal(404, result.StatusCode);
    }

    [Theory(DisplayName = "HandleAsync should return NotFound for unknown or malformed commands")]
    [InlineData("/api/player/explode")]
    [InlineData("/api/player/volume/loud")]
    [InlineData("/api/player/seek/")]
    [InlineData("/api/player/queue/abc/play")]
    public async Task HandleAsync_ShouldReturnNotFound_ForUnknownCommands(string path)
    {
        // Arrange
        PlayerControlRouteHandler sut = BuildHandler();

        // Act
        WebApiResult result = await sut.HandleAsync(path);

        // Assert
        Assert.Equal(404, result.StatusCode);
    }
}