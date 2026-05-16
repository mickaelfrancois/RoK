using CleanArch.DevKit.Mediator.Results;
using Rok.Application.Errors;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Dto;
using Rok.Application.Features.Playlists.Requests;
using Rok.ViewModels.Playlist;
using Rok.ViewModels.Playlists.Interfaces;
using Rok.ViewModels.Playlists.Services;

namespace Rok.PresentationTests.ViewModels.Playlists.Services;

public class PlaylistsDataLoaderTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IPlaylistViewModelFactory> _vmFactory = new();

    private PlaylistsDataLoader BuildService() => new(_mediator.Object, _vmFactory.Object, NullLogger<PlaylistsDataLoader>.Instance);

    [Fact(DisplayName = "LoadPlaylistsAsync should leave ViewModels empty when there are no playlists")]
    public async Task LoadPlaylistsAsync_ShouldLeaveViewModelsEmpty_WhenNoPlaylists()
    {
        // Arrange
        _mediator.Setup(m => m.Send(It.IsAny<GetAllPlaylistsRequest>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<PlaylistHeaderDto>());
        PlaylistsDataLoader sut = BuildService();

        // Act
        await sut.LoadPlaylistsAsync();

        // Assert
        Assert.Empty(sut.ViewModels);
        _vmFactory.Verify(f => f.Create(), Times.Never);
    }

    [Fact(DisplayName = "GetPlaylistByIdAsync should return the playlist when the mediator succeeds")]
    public async Task GetPlaylistByIdAsync_ShouldReturnPlaylist_WhenSuccess()
    {
        // Arrange
        PlaylistHeaderDto playlist = new() { Id = 7, Name = "Mix" };
        _mediator.Setup(m => m.Send(It.IsAny<GetPlaylistByIdRequest>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(Result<PlaylistHeaderDto>.Ok(playlist));
        PlaylistsDataLoader sut = BuildService();

        // Act
        PlaylistHeaderDto? result = await sut.GetPlaylistByIdAsync(7);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Mix", result!.Name);
    }

    [Fact(DisplayName = "GetPlaylistByIdAsync should return null when the mediator returns an error")]
    public async Task GetPlaylistByIdAsync_ShouldReturnNull_WhenError()
    {
        // Arrange
        _mediator.Setup(m => m.Send(It.IsAny<GetPlaylistByIdRequest>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(Result<PlaylistHeaderDto>.Fail(new OperationError("playlist.not_found", "not found")));
        PlaylistsDataLoader sut = BuildService();

        // Act
        PlaylistHeaderDto? result = await sut.GetPlaylistByIdAsync(99);

        // Assert
        Assert.Null(result);
    }

    [Fact(DisplayName = "RemovePlaylist should be a no-op when the playlist is not in the list")]
    public void RemovePlaylist_ShouldBeNoOp_WhenNotFound()
    {
        // Arrange
        PlaylistsDataLoader sut = BuildService();

        // Act
        sut.RemovePlaylist(99);

        // Assert
        Assert.Empty(sut.ViewModels);
    }

    [Fact(DisplayName = "Clear should empty the ViewModels collection")]
    public void Clear_ShouldEmptyViewModels()
    {
        // Arrange
        PlaylistsDataLoader sut = BuildService();

        // Act
        sut.Clear();

        // Assert
        Assert.Empty(sut.ViewModels);
    }
}
