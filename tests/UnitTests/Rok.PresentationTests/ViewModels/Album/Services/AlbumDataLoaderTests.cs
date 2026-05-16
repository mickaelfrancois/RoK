using CleanArch.DevKit.Mediator.Results;
using Rok.Application.Errors;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Dto;
using Rok.Application.Features.Albums.Requests;
using Rok.Application.Features.Tracks.Requests;
using Rok.ViewModels.Album.Services;
using Rok.ViewModels.Track;
using Rok.ViewModels.Tracks.Interfaces;

namespace Rok.PresentationTests.ViewModels.Album.Services;

public class AlbumDataLoaderTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<ITrackViewModelFactory> _vmFactory = new();

    private AlbumDataLoader BuildService() => new(_mediator.Object, _vmFactory.Object, NullLogger<AlbumDataLoader>.Instance);

    [Fact(DisplayName = "LoadAlbumAsync should return the album when the mediator succeeds")]
    public async Task LoadAlbumAsync_ShouldReturnAlbum_WhenSuccess()
    {
        // Arrange
        AlbumDto album = new() { Id = 7, Name = "Greatest Hits" };
        _mediator.Setup(m => m.Send(It.IsAny<GetAlbumByIdRequest>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(Result<AlbumDto>.Ok(album));
        AlbumDataLoader sut = BuildService();

        // Act
        AlbumDto? result = await sut.LoadAlbumAsync(7);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Greatest Hits", result!.Name);
    }

    [Fact(DisplayName = "LoadAlbumAsync should return null when the mediator returns an error")]
    public async Task LoadAlbumAsync_ShouldReturnNull_WhenError()
    {
        // Arrange
        _mediator.Setup(m => m.Send(It.IsAny<GetAlbumByIdRequest>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(Result<AlbumDto>.Fail(new OperationError("album.not_found", "not found")));
        AlbumDataLoader sut = BuildService();

        // Act
        AlbumDto? result = await sut.LoadAlbumAsync(99);

        // Assert
        Assert.Null(result);
    }

    [Fact(DisplayName = "LoadTracksAsync should return an empty list when the album has no tracks")]
    public async Task LoadTracksAsync_ShouldReturnEmpty_WhenNoTracks()
    {
        // Arrange
        _mediator.Setup(m => m.Send(It.IsAny<GetTracksByAlbumIdRequest>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<TrackDto>());
        AlbumDataLoader sut = BuildService();

        // Act
        List<TrackViewModel> result = await sut.LoadTracksAsync(7);

        // Assert
        Assert.Empty(result);
        _vmFactory.Verify(f => f.Create(), Times.Never);
    }

    [Fact(DisplayName = "ReloadAlbumAsync should return the album when the mediator succeeds")]
    public async Task ReloadAlbumAsync_ShouldReturnAlbum_WhenSuccess()
    {
        // Arrange
        AlbumDto album = new() { Id = 7 };
        _mediator.Setup(m => m.Send(It.IsAny<GetAlbumByIdRequest>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(Result<AlbumDto>.Ok(album));
        AlbumDataLoader sut = BuildService();

        // Act
        AlbumDto? result = await sut.ReloadAlbumAsync(7);

        // Assert
        Assert.NotNull(result);
    }

    [Fact(DisplayName = "ReloadAlbumAsync should return null when the mediator returns an error")]
    public async Task ReloadAlbumAsync_ShouldReturnNull_WhenError()
    {
        // Arrange
        _mediator.Setup(m => m.Send(It.IsAny<GetAlbumByIdRequest>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(Result<AlbumDto>.Fail(new OperationError("album.not_found", "not found")));
        AlbumDataLoader sut = BuildService();

        // Act
        AlbumDto? result = await sut.ReloadAlbumAsync(99);

        // Assert
        Assert.Null(result);
    }
}
