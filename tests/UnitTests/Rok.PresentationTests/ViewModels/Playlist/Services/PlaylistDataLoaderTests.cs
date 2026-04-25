using MiF.Mediator.Interfaces;
using MiF.Result;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Dto;
using Rok.Application.Features.Playlists.Query;
using Rok.Application.Features.Tracks.Query;
using Rok.ViewModels.Playlist.Services;
using Rok.ViewModels.Track;
using Rok.ViewModels.Tracks.Interfaces;

namespace Rok.PresentationTests.ViewModels.Playlist.Services;

public class PlaylistDataLoaderTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<ITrackViewModelFactory> _vmFactory = new();

    private PlaylistDataLoader BuildService() => new(_mediator.Object, _vmFactory.Object, NullLogger<PlaylistDataLoader>.Instance);

    [Fact(DisplayName = "LoadPlaylistAsync should return the playlist when the mediator succeeds")]
    public async Task LoadPlaylistAsync_ShouldReturnPlaylist_WhenSuccess()
    {
        // Arrange
        PlaylistHeaderDto playlist = new() { Id = 7, Name = "Mix" };
        _mediator.Setup(m => m.SendMessageAsync(It.IsAny<GetPlaylistByIdQuery>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(Result<PlaylistHeaderDto>.Success(playlist));
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
        _mediator.Setup(m => m.SendMessageAsync(It.IsAny<GetPlaylistByIdQuery>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(Result<PlaylistHeaderDto>.Fail("not found"));
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
        _mediator.Setup(m => m.SendMessageAsync(It.IsAny<GetTracksByPlaylistIdQuery>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<TrackDto>());
        PlaylistDataLoader sut = BuildService();

        // Act
        List<TrackViewModel> result = await sut.LoadTracksAsync(7);

        // Assert
        Assert.Empty(result);
        _vmFactory.Verify(f => f.Create(), Times.Never);
    }
}
