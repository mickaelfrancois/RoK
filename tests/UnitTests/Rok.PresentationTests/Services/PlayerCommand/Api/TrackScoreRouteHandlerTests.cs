using Moq;
using Rok.Services.PlayerCommand;
using Rok.Services.PlayerCommand.Api;

namespace Rok.PresentationTests.Services.PlayerCommand.Api;

public class TrackScoreRouteHandlerTests
{
    private readonly Mock<IPlayerCommandService> _commandService = new();

    private TrackScoreRouteHandler BuildHandler() =>
        new(_commandService.Object, action => action());

    [Theory(DisplayName = "CanHandle should accept POST requests carrying a score segment only")]
    [InlineData("POST", "/api/tracks/12/score/4", true)]
    [InlineData("GET", "/api/tracks/12/score/4", false)]
    [InlineData("POST", "/api/tracks/12", false)]
    [InlineData("POST", "/api/player/play", false)]
    public void CanHandle_ShouldAcceptOnlyMatchingMethodAndPath(string method, string path, bool expected)
    {
        // Arrange
        TrackScoreRouteHandler sut = BuildHandler();

        // Act
        bool result = sut.CanHandle(method, path);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact(DisplayName = "HandleAsync should rate the track when identifier and score are valid")]
    public async Task HandleAsync_ShouldRateTrack()
    {
        // Arrange
        _commandService.Setup(c => c.SetScoreAsync(12, 4)).ReturnsAsync(true);
        TrackScoreRouteHandler sut = BuildHandler();

        // Act
        WebApiResult result = await sut.HandleAsync("/api/tracks/12/score/4");

        // Assert
        Assert.Equal(200, result.StatusCode);
        _commandService.Verify(c => c.SetScoreAsync(12, 4), Times.Once);
    }

    [Fact(DisplayName = "HandleAsync should accept zero as the rating that clears a score")]
    public async Task HandleAsync_ShouldAcceptZero()
    {
        // Arrange
        _commandService.Setup(c => c.SetScoreAsync(12, 0)).ReturnsAsync(true);
        TrackScoreRouteHandler sut = BuildHandler();

        // Act
        WebApiResult result = await sut.HandleAsync("/api/tracks/12/score/0");

        // Assert
        Assert.Equal(200, result.StatusCode);
        _commandService.Verify(c => c.SetScoreAsync(12, 0), Times.Once);
    }

    [Theory(DisplayName = "HandleAsync should return BadRequest for an out-of-range or malformed score")]
    [InlineData("/api/tracks/12/score/6")]
    [InlineData("/api/tracks/12/score/-1")]
    [InlineData("/api/tracks/12/score/five")]
    [InlineData("/api/tracks/abc/score/3")]
    public async Task HandleAsync_ShouldReturnBadRequest_ForInvalidInput(string path)
    {
        // Arrange
        TrackScoreRouteHandler sut = BuildHandler();

        // Act
        WebApiResult result = await sut.HandleAsync(path);

        // Assert
        Assert.Equal(400, result.StatusCode);
        _commandService.Verify(c => c.SetScoreAsync(It.IsAny<long>(), It.IsAny<int>()), Times.Never);
    }

    [Fact(DisplayName = "HandleAsync should return NotFound when the rating cannot be persisted")]
    public async Task HandleAsync_ShouldReturnNotFound_WhenPersistenceFails()
    {
        // Arrange
        _commandService.Setup(c => c.SetScoreAsync(It.IsAny<long>(), It.IsAny<int>())).ReturnsAsync(false);
        TrackScoreRouteHandler sut = BuildHandler();

        // Act
        WebApiResult result = await sut.HandleAsync("/api/tracks/99/score/3");

        // Assert
        Assert.Equal(404, result.StatusCode);
    }
}