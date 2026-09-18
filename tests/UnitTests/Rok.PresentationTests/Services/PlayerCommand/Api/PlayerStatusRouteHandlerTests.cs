using System.Text.Json;
using Moq;
using Rok.Application.Dto;
using Rok.Application.Player;
using Rok.Services.PlayerCommand.Api;

namespace Rok.PresentationTests.Services.PlayerCommand.Api;

public class PlayerStatusRouteHandlerTests
{
    private readonly Mock<IPlayerService> _playerService = new();

    private readonly List<TrackDto> _playlist =
    [
        new() { Id = 1, Title = "One", ArtistName = "A", AlbumName = "Alpha", Duration = 100, Score = 3 },
        new() { Id = 2, Title = "Two", ArtistName = "B", AlbumName = "Beta", Duration = 200, Score = 5 },
        new() { Id = 2, Title = "Two again", ArtistName = "B", AlbumName = "Beta", Duration = 200, Score = 5 }
    ];

    public PlayerStatusRouteHandlerTests()
    {
        _playerService.SetupGet(p => p.Playlist).Returns(_playlist);
        _playerService.SetupGet(p => p.PlaybackState).Returns(EPlaybackState.Playing);
        _playerService.SetupGet(p => p.Mode).Returns(EPlaybackMode.Music);
        _playerService.SetupGet(p => p.Volume).Returns(64);
        _playerService.SetupGet(p => p.Position).Returns(12.5);
        _playerService.SetupGet(p => p.CanNext).Returns(true);
        _playerService.SetupGet(p => p.CurrentTrack).Returns(_playlist[1]);
    }

    private PlayerStatusRouteHandler BuildHandler() =>
        new(_playerService.Object, action => action());

    [Theory(DisplayName = "CanHandle should accept GET requests on the status and queue routes only")]
    [InlineData("GET", "/api/player/status", true)]
    [InlineData("GET", "/api/player/queue", true)]
    [InlineData("POST", "/api/player/status", false)]
    [InlineData("GET", "/api/player/volume/10", false)]
    public void CanHandle_ShouldAcceptOnlyMatchingMethodAndPath(string method, string path, bool expected)
    {
        // Arrange
        PlayerStatusRouteHandler sut = BuildHandler();

        // Act
        bool result = sut.CanHandle(method, path);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact(DisplayName = "HandleAsync should describe the player state with camel cased members")]
    public async Task HandleAsync_ShouldDescribePlayerState()
    {
        // Arrange
        PlayerStatusRouteHandler sut = BuildHandler();

        // Act
        WebApiResult result = await sut.HandleAsync("/api/player/status");

        // Assert
        using JsonDocument document = JsonDocument.Parse(result.Body);
        JsonElement status = document.RootElement;

        Assert.Equal(200, result.StatusCode);
        Assert.Equal("Playing", status.GetProperty("state").GetString());
        Assert.Equal("Music", status.GetProperty("mode").GetString());
        Assert.Equal(64, status.GetProperty("volume").GetDouble());
        Assert.Equal(12.5, status.GetProperty("position").GetDouble());
        Assert.Equal(3, status.GetProperty("queueLength").GetInt32());
        Assert.True(status.GetProperty("canNext").GetBoolean());
        Assert.Equal("Two", status.GetProperty("current").GetProperty("title").GetString());
        Assert.Equal(5, status.GetProperty("current").GetProperty("score").GetInt32());
    }

    [Fact(DisplayName = "HandleAsync should report no current track when nothing is loaded")]
    public async Task HandleAsync_ShouldReportNoCurrentTrack_WhenNothingIsLoaded()
    {
        // Arrange
        _playerService.SetupGet(p => p.CurrentTrack).Returns((TrackDto?)null);
        PlayerStatusRouteHandler sut = BuildHandler();

        // Act
        WebApiResult result = await sut.HandleAsync("/api/player/status");

        // Assert
        using JsonDocument document = JsonDocument.Parse(result.Body);

        Assert.False(document.RootElement.TryGetProperty("current", out _));
    }

    [Fact(DisplayName = "The queue signature should change when the queue is reordered behind an unchanged length")]
    public async Task QueueSignature_ShouldChange_WhenQueueIsReordered()
    {
        // Arrange
        PlayerStatusRouteHandler sut = BuildHandler();
        JsonElement before = await ReadStatusAsync(sut);

        // Act
        (_playlist[0], _playlist[2]) = (_playlist[2], _playlist[0]);
        JsonElement after = await ReadStatusAsync(sut);

        // Assert
        Assert.Equal(before.GetProperty("queueLength").GetInt32(), after.GetProperty("queueLength").GetInt32());
        Assert.Equal(
            before.GetProperty("current").GetProperty("trackId").GetInt64(),
            after.GetProperty("current").GetProperty("trackId").GetInt64());
        Assert.NotEqual(
            before.GetProperty("queueSignature").GetInt64(),
            after.GetProperty("queueSignature").GetInt64());
    }

    [Fact(DisplayName = "The queue signature should hold steady while the queue does not move")]
    public async Task QueueSignature_ShouldHoldSteady_WhenNothingMoves()
    {
        // Arrange
        PlayerStatusRouteHandler sut = BuildHandler();

        // Act
        JsonElement first = await ReadStatusAsync(sut);
        JsonElement second = await ReadStatusAsync(sut);

        // Assert
        Assert.Equal(
            first.GetProperty("queueSignature").GetInt64(),
            second.GetProperty("queueSignature").GetInt64());
    }

    private static async Task<JsonElement> ReadStatusAsync(PlayerStatusRouteHandler handler)
    {
        WebApiResult result = await handler.HandleAsync("/api/player/status");

        return JsonDocument.Parse(result.Body).RootElement.Clone();
    }

    [Fact(DisplayName = "HandleAsync should flag the playing entry by reference so duplicate identifiers stay distinct")]
    public async Task HandleAsync_ShouldFlagPlayingEntryByReference()
    {
        // Arrange
        PlayerStatusRouteHandler sut = BuildHandler();

        // Act
        WebApiResult result = await sut.HandleAsync("/api/player/queue");

        // Assert
        using JsonDocument document = JsonDocument.Parse(result.Body);
        List<JsonElement> entries = [.. document.RootElement.EnumerateArray()];

        Assert.Equal(3, entries.Count);
        Assert.False(entries[0].GetProperty("isCurrent").GetBoolean());
        Assert.True(entries[1].GetProperty("isCurrent").GetBoolean());
        Assert.False(entries[2].GetProperty("isCurrent").GetBoolean());
        Assert.Equal(2, entries[2].GetProperty("index").GetInt32());
        Assert.Equal(2, entries[2].GetProperty("trackId").GetInt64());
    }
}