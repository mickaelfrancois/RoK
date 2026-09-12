using System.Text.Json;
using Moq;
using Rok.Services.PlayerCommand;
using Rok.Services.PlayerCommand.Api;
using Rok.WebApi.Contracts;

namespace Rok.PresentationTests.Services.PlayerCommand.Api;

public class SurpriseRouteHandlerTests
{
    private readonly Mock<IPlayerCommandService> _commandService = new();

    private SurpriseRouteHandler BuildHandler() =>
        new(_commandService.Object, action => action());

    [Theory(DisplayName = "CanHandle should accept POST requests on the two surprise routes only")]
    [InlineData("POST", "/api/surprise/album", true)]
    [InlineData("POST", "/api/surprise/artist", true)]
    [InlineData("GET", "/api/surprise/album", false)]
    [InlineData("POST", "/api/surprise/genre", false)]
    [InlineData("POST", "/api/surprise", false)]
    public void CanHandle_ShouldAcceptOnlyMatchingMethodAndPath(string method, string path, bool expected)
    {
        // Arrange
        SurpriseRouteHandler sut = BuildHandler();

        // Act
        bool result = sut.CanHandle(method, path);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact(DisplayName = "HandleAsync should report the album that was drawn")]
    public async Task HandleAsync_ShouldReportDrawnAlbum()
    {
        // Arrange
        _commandService.Setup(c => c.SurpriseAlbumAsync())
                       .ReturnsAsync(new SurprisePick("album", 7, "13", "Suicidal tendencies", 12));
        SurpriseRouteHandler sut = BuildHandler();

        // Act
        WebApiResult result = await sut.HandleAsync("/api/surprise/album");

        // Assert
        using JsonDocument document = JsonDocument.Parse(result.Body);
        JsonElement pick = document.RootElement;

        Assert.Equal(200, result.StatusCode);
        Assert.Equal("album", pick.GetProperty("kind").GetString());
        Assert.Equal("13", pick.GetProperty("name").GetString());
        Assert.Equal("Suicidal tendencies", pick.GetProperty("artistName").GetString());
        Assert.Equal(12, pick.GetProperty("trackCount").GetInt32());
        _commandService.Verify(c => c.SurpriseArtistAsync(), Times.Never);
    }

    [Fact(DisplayName = "HandleAsync should route the artist draw to the artist command")]
    public async Task HandleAsync_ShouldRouteArtistDraw()
    {
        // Arrange
        _commandService.Setup(c => c.SurpriseArtistAsync())
                       .ReturnsAsync(new SurprisePick("artist", 42, "Etienne daho", null, 30));
        SurpriseRouteHandler sut = BuildHandler();

        // Act
        WebApiResult result = await sut.HandleAsync("/api/surprise/artist");

        // Assert
        using JsonDocument document = JsonDocument.Parse(result.Body);

        Assert.Equal(200, result.StatusCode);
        Assert.Equal("artist", document.RootElement.GetProperty("kind").GetString());
        Assert.False(document.RootElement.TryGetProperty("artistName", out _));
        _commandService.Verify(c => c.SurpriseArtistAsync(), Times.Once);
        _commandService.Verify(c => c.SurpriseAlbumAsync(), Times.Never);
    }

    [Fact(DisplayName = "HandleAsync should return NotFound when nothing playable could be drawn")]
    public async Task HandleAsync_ShouldReturnNotFound_WhenNothingDrawn()
    {
        // Arrange
        _commandService.Setup(c => c.SurpriseAlbumAsync()).ReturnsAsync((SurprisePick?)null);
        SurpriseRouteHandler sut = BuildHandler();

        // Act
        WebApiResult result = await sut.HandleAsync("/api/surprise/album");

        // Assert
        Assert.Equal(404, result.StatusCode);
    }
}