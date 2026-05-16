using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Dto;
using Rok.Application.Features.Playlists;
using Rok.Application.Features.Playlists.Requests;
using Rok.ViewModels.Playlist.Services;

namespace Rok.PresentationTests.ViewModels.Playlist.Services;

public class PlaylistGenerationServiceTests
{
    private readonly FakeMediator _mediator = new();
    private readonly Mock<IPlaylistService> _playlistService = new();

    private PlaylistGenerationService BuildService() =>
        new(_mediator, _playlistService.Object, NullLogger<PlaylistGenerationService>.Instance);

    [Fact(DisplayName = "GenerateTracksAsync should delegate generation to IPlaylistService and return its tracks")]
    public async Task GenerateTracksAsync_ShouldReturnGeneratedTracks()
    {
        // Arrange
        PlaylistHeaderDto playlist = new() { Id = 7 };
        PlaylistTracksDto tracks = new() { Tracks = new() { new TrackDto { Id = 1 }, new TrackDto { Id = 2 } } };
        _playlistService.Setup(p => p.GenerateAsync(playlist)).ReturnsAsync(tracks);
        PlaylistGenerationService sut = BuildService();

        // Act
        List<TrackDto> result = await sut.GenerateTracksAsync(playlist);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(1, result[0].Id);
    }

    [Fact(DisplayName = "GenerateTracksAsync should persist tracks with incremental positions starting at one")]
    public async Task GenerateTracksAsync_ShouldPersistTracksWithIncrementalPositions()
    {
        // Arrange
        PlaylistHeaderDto playlist = new() { Id = 7 };
        PlaylistTracksDto tracks = new() { Tracks = new() { new TrackDto { Id = 10 }, new TrackDto { Id = 20 }, new TrackDto { Id = 30 } } };
        _playlistService.Setup(p => p.GenerateAsync(playlist)).ReturnsAsync(tracks);
        PlaylistGenerationService sut = BuildService();

        // Act
        await sut.GenerateTracksAsync(playlist);

        // Assert
        CreatePlaylistTracksRequest sent = Assert.Single(_mediator.Sent<CreatePlaylistTracksRequest>());
        Assert.Equal(7, sent.PlaylistId);
        Assert.Equal(3, sent.Tracks.Count);
        Assert.Equal(10, sent.Tracks[0].TrackId);
        Assert.Equal(1, sent.Tracks[0].Position);
        Assert.Equal(20, sent.Tracks[1].TrackId);
        Assert.Equal(2, sent.Tracks[1].Position);
        Assert.Equal(30, sent.Tracks[2].TrackId);
        Assert.Equal(3, sent.Tracks[2].Position);
    }

    [Fact(DisplayName = "GenerateTracksAsync should send an empty CreatePlaylistTracksRequest when no tracks were generated")]
    public async Task GenerateTracksAsync_ShouldSendEmptyCommand_WhenNoTracksGenerated()
    {
        // Arrange
        PlaylistHeaderDto playlist = new() { Id = 7 };
        _playlistService.Setup(p => p.GenerateAsync(playlist)).ReturnsAsync(new PlaylistTracksDto());
        PlaylistGenerationService sut = BuildService();

        // Act
        List<TrackDto> result = await sut.GenerateTracksAsync(playlist);

        // Assert
        Assert.Empty(result);
        CreatePlaylistTracksRequest sent = Assert.Single(_mediator.Sent<CreatePlaylistTracksRequest>());
        Assert.Equal(7, sent.PlaylistId);
        Assert.Empty(sent.Tracks);
    }
}