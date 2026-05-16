using MiF.Result;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Dto;
using Rok.Application.Features.Albums.Requests;
using Rok.Application.Features.Genres.Requests;
using Rok.Application.Features.Tracks.Requests;
using Rok.ViewModels.Album;
using Rok.ViewModels.Albums.Interfaces;
using Rok.ViewModels.Genre.Services;

namespace Rok.PresentationTests.ViewModels.Genre.Services;

public class GenreDataLoaderTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IAlbumViewModelFactory> _vmFactory = new();

    private GenreDataLoader BuildService() => new(_mediator.Object, _vmFactory.Object, NullLogger<GenreDataLoader>.Instance);

    [Fact(DisplayName = "LoadGenreAsync should return the genre when the mediator succeeds")]
    public async Task LoadGenreAsync_ShouldReturnGenre_WhenSuccess()
    {
        // Arrange
        GenreDto genre = new() { Id = 7, Name = "Jazz" };
        _mediator.Setup(m => m.Send(It.IsAny<GetGenreByIdRequest>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(Result<GenreDto>.Success(genre));
        GenreDataLoader sut = BuildService();

        // Act
        GenreDto? result = await sut.LoadGenreAsync(7);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Jazz", result!.Name);
    }

    [Fact(DisplayName = "LoadGenreAsync should return null when the mediator returns an error")]
    public async Task LoadGenreAsync_ShouldReturnNull_WhenError()
    {
        // Arrange
        _mediator.Setup(m => m.Send(It.IsAny<GetGenreByIdRequest>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(Result<GenreDto>.Fail("not found"));
        GenreDataLoader sut = BuildService();

        // Act
        GenreDto? result = await sut.LoadGenreAsync(99);

        // Assert
        Assert.Null(result);
    }

    [Fact(DisplayName = "LoadAlbumsAsync should return an empty list when the genre has no albums")]
    public async Task LoadAlbumsAsync_ShouldReturnEmpty_WhenNoAlbums()
    {
        // Arrange
        _mediator.Setup(m => m.Send(It.IsAny<GetAlbumsByGenreIdRequest>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<AlbumDto>());
        GenreDataLoader sut = BuildService();

        // Act
        List<AlbumViewModel> result = await sut.LoadAlbumsAsync(7);

        // Assert
        Assert.Empty(result);
        _vmFactory.Verify(f => f.Create(), Times.Never);
    }

    [Fact(DisplayName = "LoadTracksAsync should query tracks for the given genre and return them")]
    public async Task LoadTracksAsync_ShouldReturnTracks()
    {
        // Arrange
        List<TrackDto> tracks = new() { new TrackDto { Id = 1 }, new TrackDto { Id = 2 } };
        _mediator.Setup(m => m.Send(It.Is<GetTracksByGenreIdRequest>(q => q.GenreId == 7), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(tracks);
        GenreDataLoader sut = BuildService();

        // Act
        List<TrackDto> result = (await sut.LoadTracksAsync(7)).ToList();

        // Assert
        Assert.Equal(2, result.Count);
    }
}
