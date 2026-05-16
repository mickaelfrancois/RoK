using CleanArch.DevKit.Mediator.Results;
using Rok.Application.Errors;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Dto;
using Rok.Application.Features.Playlists.Requests;
using Rok.Application.Features.Tracks.Requests;
using Rok.ViewModels.Playlist.Services;
using Rok.ViewModels.Track;
using Rok.ViewModels.Tracks.Interfaces;

namespace Rok.PresentationTests.ViewModels.Playlist.Services;

public class PlaylistDataLoaderTests
{
    private readonly FakeMediator _mediator = new();
    private readonly Mock<ITrackViewModelFactory> _vmFactory = new();

    private PlaylistDataLoader BuildService() => new(_mediator, _vmFactory.Object, NullLogger<PlaylistDataLoader>.Instance);

    [Fact(DisplayName = "LoadPlaylistAsync should return the playlist when the mediator succeeds")]
    public async Task LoadPlaylistAsync_ShouldReturnPlaylist_WhenSuccess()
    {
        // Arrange
        PlaylistHeaderDto playlist = new() { Id = 7, Name = "Mix" };
        _mediator.Setup<GetPlaylistByIdRequest, Result<PlaylistHeaderDto>>().Returns(Result<PlaylistHeaderDto>.Ok(playlist));
        PlaylistDataLoader sut = BuildService();

        // Act
        PlaylistHeaderDto? result = await sut.LoadPlaylistAsync(7);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Mix", result!.Name);
    }

    [Fact(DisplayName = "LoadPlaylistAsync should return null when the mediator returns an error")]
    public async Task LoadPlaylistAsync_ShouldReturnNull_WhenError()
    {
        // Arrange
        _mediator.Setup<GetPlaylistByIdRequest, Result<PlaylistHeaderDto>>().Returns(Result<PlaylistHeaderDto>.Fail(new OperationError("playlist.not_found", "not found")));
        PlaylistDataLoader sut = BuildService();

        // Act
        PlaylistHeaderDto? result = await sut.LoadPlaylistAsync(99);

        // Assert
        Assert.Null(result);
    }

    [Fact(DisplayName = "LoadTracksAsync should return an empty list when the playlist has no tracks")]
    public async Task LoadTracksAsync_ShouldReturnEmpty_WhenNoTracks()
    {
        // Arrange
        _mediator.Setup<GetTracksByPlaylistIdRequest, IEnumerable<TrackDto>>().Returns(new List<TrackDto>());
        PlaylistDataLoader sut = BuildService();

        // Act
        List<TrackViewModel> result = await sut.LoadTracksAsync(7);

        // Assert
        Assert.Empty(result);
        _vmFactory.Verify(f => f.Create(), Times.Never);
    }
}
