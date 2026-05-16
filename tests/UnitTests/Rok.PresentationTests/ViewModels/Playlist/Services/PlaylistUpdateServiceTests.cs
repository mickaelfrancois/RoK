using CleanArch.DevKit.Mediator.Results;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Dto;
using Rok.Application.Errors;
using Rok.Application.Features.Playlists.Requests;
using Rok.Application.Interfaces.Pictures;
using Rok.ViewModels.Playlist.Services;
using Rok.ViewModels.Track;

namespace Rok.PresentationTests.ViewModels.Playlist.Services;

public class PlaylistUpdateServiceTests
{
    private readonly FakeMediator _mediator = new();
    private readonly Mock<IArtistPicture> _artistPicture = new();

    private PlaylistUpdateService BuildService()
    {
        PlaylistPictureService pictureService = new(_artistPicture.Object, NullLogger<PlaylistPictureService>.Instance);
        return new PlaylistUpdateService(_mediator, new Messenger(), pictureService, NullLogger<PlaylistUpdateService>.Instance);
    }

    [Fact(DisplayName = "SavePlaylistAsync should not call mediator when nothing has changed and forceUpdate is false")]
    public async Task SavePlaylistAsync_ShouldSkip_WhenNoChange()
    {
        // Arrange — no track means command.Picture is null, so playlist.Picture must be null too for "no change"
        PlaylistHeaderDto playlist = new() { Id = 1, Name = "Mix", Picture = null!, Duration = 0, TrackCount = 0, TrackMaximum = 0, DurationMaximum = 0 };
        PlaylistUpdateService sut = BuildService();

        // Act
        bool result = await sut.SavePlaylistAsync(playlist, Array.Empty<TrackViewModel>(), forceUpdate: false);

        // Assert
        Assert.False(result);
        Assert.Empty(_mediator.Sent<UpdatePlaylistRequest>());
    }

    [Fact(DisplayName = "SavePlaylistAsync should call mediator when forceUpdate is true even without changes")]
    public async Task SavePlaylistAsync_ShouldUpdate_WhenForced()
    {
        // Arrange
        PlaylistHeaderDto playlist = new() { Id = 1, Name = "Mix" };
        _mediator.Setup<UpdatePlaylistRequest, Result>().Returns(Result.Ok());
        PlaylistUpdateService sut = BuildService();

        // Act
        bool result = await sut.SavePlaylistAsync(playlist, Array.Empty<TrackViewModel>(), forceUpdate: true);

        // Assert
        Assert.True(result);
        UpdatePlaylistRequest sent = Assert.Single(_mediator.Sent<UpdatePlaylistRequest>());
        Assert.Equal(1, sent.Id);
    }

    [Fact(DisplayName = "SavePlaylistAsync should return false when the mediator returns an error")]
    public async Task SavePlaylistAsync_ShouldReturnFalse_WhenMediatorFails()
    {
        // Arrange
        PlaylistHeaderDto playlist = new() { Id = 1, Name = "Mix" };
        _mediator.Setup<UpdatePlaylistRequest, Result>().Returns(Result.Fail(new OperationError("test.boom", "boom")));
        PlaylistUpdateService sut = BuildService();

        // Act
        bool result = await sut.SavePlaylistAsync(playlist, Array.Empty<TrackViewModel>(), forceUpdate: true);

        // Assert
        Assert.False(result);
    }

    [Fact(DisplayName = "RemoveTrackAsync should return true when the mediator succeeds")]
    public async Task RemoveTrackAsync_ShouldReturnTrue_WhenSuccess()
    {
        // Arrange
        _mediator.Setup<RemoveTrackFromPlaylistRequest, Result>().Returns(Result.Ok());
        PlaylistUpdateService sut = BuildService();

        // Act
        bool result = await sut.RemoveTrackAsync(playlistId: 1, trackId: 10);

        // Assert
        Assert.True(result);
        RemoveTrackFromPlaylistRequest sent = Assert.Single(_mediator.Sent<RemoveTrackFromPlaylistRequest>());
        Assert.Equal(1, sent.PlaylistId);
        Assert.Equal(10, sent.TrackId);
    }

    [Fact(DisplayName = "RemoveTrackAsync should return false when the mediator fails")]
    public async Task RemoveTrackAsync_ShouldReturnFalse_WhenFailure()
    {
        // Arrange
        _mediator.Setup<RemoveTrackFromPlaylistRequest, Result>().Returns(Result.Fail(new OperationError("test.boom", "boom")));
        PlaylistUpdateService sut = BuildService();

        // Act
        bool result = await sut.RemoveTrackAsync(playlistId: 1, trackId: 10);

        // Assert
        Assert.False(result);
    }

    [Fact(DisplayName = "DeletePlaylistAsync should return true when the mediator succeeds")]
    public async Task DeletePlaylistAsync_ShouldReturnTrue_WhenSuccess()
    {
        // Arrange
        _mediator.Setup<DeletePlaylistRequest, Result<bool>>().Returns(Result<bool>.Ok(true));
        PlaylistUpdateService sut = BuildService();

        // Act
        bool result = await sut.DeletePlaylistAsync(playlistId: 1, playlistName: "Mix");

        // Assert
        Assert.True(result);
        DeletePlaylistRequest sent = Assert.Single(_mediator.Sent<DeletePlaylistRequest>());
        Assert.Equal(1, sent.Id);
    }

    [Fact(DisplayName = "DeletePlaylistAsync should return false when the mediator fails")]
    public async Task DeletePlaylistAsync_ShouldReturnFalse_WhenFailure()
    {
        // Arrange
        _mediator.Setup<DeletePlaylistRequest, Result<bool>>().Returns(Result<bool>.Fail(new OperationError("test.boom", "boom")));
        PlaylistUpdateService sut = BuildService();

        // Act
        bool result = await sut.DeletePlaylistAsync(playlistId: 1, playlistName: "Mix");

        // Assert
        Assert.False(result);
    }

    [Fact(DisplayName = "SaveTracksPositionAsync should return true when the mediator succeeds")]
    public async Task SaveTracksPositionAsync_ShouldReturnTrue_WhenSuccess()
    {
        // Arrange
        _mediator.Setup<MovePlaylistTracksRequest, Result<bool>>().Returns(Result<bool>.Ok(true));
        PlaylistUpdateService sut = BuildService();

        // Act
        bool result = await sut.SaveTracksPositionAsync(playlistId: 1, new List<long> { 10, 20 });

        // Assert
        Assert.True(result);
        MovePlaylistTracksRequest sent = Assert.Single(_mediator.Sent<MovePlaylistTracksRequest>());
        Assert.Equal(1, sent.PlaylistId);
        Assert.True(sent.Tracks.SequenceEqual(new long[] { 10, 20 }));
    }

    [Fact(DisplayName = "SaveTracksPositionAsync should return false when the mediator fails")]
    public async Task SaveTracksPositionAsync_ShouldReturnFalse_WhenFailure()
    {
        // Arrange
        _mediator.Setup<MovePlaylistTracksRequest, Result<bool>>().Returns(Result<bool>.Fail(new OperationError("test.boom", "boom")));
        PlaylistUpdateService sut = BuildService();

        // Act
        bool result = await sut.SaveTracksPositionAsync(playlistId: 1, new List<long> { 10 });

        // Assert
        Assert.False(result);
    }
}