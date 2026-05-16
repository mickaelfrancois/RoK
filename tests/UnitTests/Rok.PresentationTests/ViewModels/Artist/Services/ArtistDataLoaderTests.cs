using CleanArch.DevKit.Mediator.Results;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Dto;
using Rok.Application.Errors;
using Rok.Application.Features.Albums.Requests;
using Rok.Application.Features.Artists.Requests;
using Rok.Application.Features.Tracks.Requests;
using Rok.ViewModels.Album;
using Rok.ViewModels.Albums.Interfaces;
using Rok.ViewModels.Artist.Services;
using Rok.ViewModels.Track;
using Rok.ViewModels.Tracks.Interfaces;

namespace Rok.PresentationTests.ViewModels.Artist.Services;

public class ArtistDataLoaderTests
{
    private readonly FakeMediator _mediator = new();
    private readonly Mock<IAlbumViewModelFactory> _albumFactory = new();
    private readonly Mock<ITrackViewModelFactory> _trackFactory = new();

    private ArtistDataLoader BuildService() =>
        new(_mediator, _albumFactory.Object, _trackFactory.Object, NullLogger<ArtistDataLoader>.Instance);

    [Fact(DisplayName = "LoadArtistAsync should return the artist when the mediator succeeds")]
    public async Task LoadArtistAsync_ShouldReturnArtist_WhenSuccess()
    {
        // Arrange
        ArtistDto artist = new() { Id = 7, Name = "Beatles" };
        _mediator.Setup<GetArtistByIdRequest, Result<ArtistDto>>().Returns(Result<ArtistDto>.Ok(artist));
        ArtistDataLoader sut = BuildService();

        // Act
        ArtistDto? result = await sut.LoadArtistAsync(7);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Beatles", result!.Name);
    }

    [Fact(DisplayName = "LoadArtistAsync should return null when the mediator returns an error")]
    public async Task LoadArtistAsync_ShouldReturnNull_WhenError()
    {
        // Arrange
        _mediator.Setup<GetArtistByIdRequest, Result<ArtistDto>>().Returns(Result<ArtistDto>.Fail(new OperationError("artist.not_found", "not found")));
        ArtistDataLoader sut = BuildService();

        // Act
        ArtistDto? result = await sut.LoadArtistAsync(99);

        // Assert
        Assert.Null(result);
    }

    [Fact(DisplayName = "LoadAlbumsAsync should return an empty list when the artist has no albums")]
    public async Task LoadAlbumsAsync_ShouldReturnEmpty_WhenNoAlbums()
    {
        // Arrange
        _mediator.Setup<GetAlbumsByArtistIdRequest, IEnumerable<AlbumDto>>().Returns(new List<AlbumDto>());
        ArtistDataLoader sut = BuildService();

        // Act
        List<AlbumViewModel> result = await sut.LoadAlbumsAsync(7);

        // Assert
        Assert.Empty(result);
        _albumFactory.Verify(f => f.Create(), Times.Never);
    }

    [Fact(DisplayName = "LoadTracksAsync should return an empty list when the artist has no tracks")]
    public async Task LoadTracksAsync_ShouldReturnEmpty_WhenNoTracks()
    {
        // Arrange
        _mediator.Setup<GetTracksByArtistIdRequest, IEnumerable<TrackDto>>().Returns(new List<TrackDto>());
        ArtistDataLoader sut = BuildService();

        // Act
        List<TrackViewModel> result = await sut.LoadTracksAsync(7);

        // Assert
        Assert.Empty(result);
        _trackFactory.Verify(f => f.Create(), Times.Never);
    }

    [Fact(DisplayName = "ReloadArtistAsync should return the artist when the mediator succeeds")]
    public async Task ReloadArtistAsync_ShouldReturnArtist_WhenSuccess()
    {
        // Arrange
        ArtistDto artist = new() { Id = 7 };
        _mediator.Setup<GetArtistByIdRequest, Result<ArtistDto>>().Returns(Result<ArtistDto>.Ok(artist));
        ArtistDataLoader sut = BuildService();

        // Act
        ArtistDto? result = await sut.ReloadArtistAsync(7);

        // Assert
        Assert.NotNull(result);
    }

    [Fact(DisplayName = "ReloadArtistAsync should return null when the mediator returns an error")]
    public async Task ReloadArtistAsync_ShouldReturnNull_WhenError()
    {
        // Arrange
        _mediator.Setup<GetArtistByIdRequest, Result<ArtistDto>>().Returns(Result<ArtistDto>.Fail(new OperationError("artist.not_found", "not found")));
        ArtistDataLoader sut = BuildService();

        // Act
        ArtistDto? result = await sut.ReloadArtistAsync(99);

        // Assert
        Assert.Null(result);
    }
}