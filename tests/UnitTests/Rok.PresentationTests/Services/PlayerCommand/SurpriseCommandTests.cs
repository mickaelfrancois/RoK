using Moq;
using Rok.Application.Dto;
using Rok.Application.Features.Albums.Requests;
using Rok.Application.Features.Artists.Requests;
using Rok.Application.Features.Tracks.Requests;
using Rok.Application.Player;
using Rok.Services.PlayerCommand;
using Rok.WebApi.Contracts;

namespace Rok.PresentationTests.Services.PlayerCommand;

public class SurpriseCommandTests
{
    private readonly Mock<IPlayerService> _player = new();
    private readonly FakeMediator _mediator = new();

    private PlayerCommandService BuildService() => new(_player.Object, _mediator);

    [Fact(DisplayName = "SurpriseAlbumAsync should play the drawn album keeping its track order")]
    public async Task SurpriseAlbumAsync_ShouldKeepTrackOrder()
    {
        // Arrange
        AlbumDto album = new() { Id = 7, Name = "13", ArtistName = "Suicidal tendencies" };
        List<TrackDto> tracks = [new() { Id = 1 }, new() { Id = 2 }, new() { Id = 3 }, new() { Id = 4 }, new() { Id = 5 }];

        _mediator.Setup<GetAllAlbumsRequest, IEnumerable<AlbumDto>>().Returns(new List<AlbumDto> { album });
        _mediator.Setup<GetTracksByAlbumIdRequest, Result<IEnumerable<TrackDto>>>()
                 .Returns(Result<IEnumerable<TrackDto>>.Ok(tracks));

        List<TrackDto>? loaded = null;
        _player.Setup(p => p.LoadPlaylist(It.IsAny<List<TrackDto>>(), It.IsAny<TrackDto>()))
               .Callback<List<TrackDto>, TrackDto>((played, _) => loaded = played);

        PlayerCommandService sut = BuildService();

        // Act
        SurprisePick? pick = await sut.SurpriseAlbumAsync();

        // Assert
        Assert.NotNull(pick);
        Assert.Equal("album", pick.Kind);
        Assert.Equal(7, pick.Id);
        Assert.Equal("13", pick.Name);
        Assert.Equal("Suicidal tendencies", pick.ArtistName);
        Assert.Equal(5, pick.TrackCount);
        Assert.NotNull(loaded);
        Assert.Equal([1L, 2L, 3L, 4L, 5L], loaded.Select(track => track.Id));
    }

    [Fact(DisplayName = "SurpriseAlbumAsync should return nothing when the library holds no album")]
    public async Task SurpriseAlbumAsync_ShouldReturnNothing_WhenLibraryIsEmpty()
    {
        // Arrange
        _mediator.Setup<GetAllAlbumsRequest, IEnumerable<AlbumDto>>().Returns(new List<AlbumDto>());
        PlayerCommandService sut = BuildService();

        // Act
        SurprisePick? pick = await sut.SurpriseAlbumAsync();

        // Assert
        Assert.Null(pick);
        _player.Verify(p => p.LoadPlaylist(It.IsAny<List<TrackDto>>(), It.IsAny<TrackDto>()), Times.Never);
    }

    [Fact(DisplayName = "SurpriseAlbumAsync should give up rather than start an empty queue")]
    public async Task SurpriseAlbumAsync_ShouldGiveUp_WhenDrawsHoldNoTrack()
    {
        // Arrange
        _mediator.Setup<GetAllAlbumsRequest, IEnumerable<AlbumDto>>()
                 .Returns(new List<AlbumDto> { new() { Id = 1, Name = "Empty" }, new() { Id = 2, Name = "Also empty" } });
        _mediator.Setup<GetTracksByAlbumIdRequest, Result<IEnumerable<TrackDto>>>()
                 .Returns(Result<IEnumerable<TrackDto>>.Ok(new List<TrackDto>()));

        PlayerCommandService sut = BuildService();

        // Act
        SurprisePick? pick = await sut.SurpriseAlbumAsync();

        // Assert
        Assert.Null(pick);
        _player.Verify(p => p.LoadPlaylist(It.IsAny<List<TrackDto>>(), It.IsAny<TrackDto>()), Times.Never);
    }

    [Fact(DisplayName = "SurpriseArtistAsync should play the drawn artist and report no album name")]
    public async Task SurpriseArtistAsync_ShouldPlayDrawnArtist()
    {
        // Arrange
        ArtistDto artist = new() { Id = 42, Name = "Etienne daho" };
        List<TrackDto> tracks = [new() { Id = 1 }, new() { Id = 2 }, new() { Id = 3 }];

        _mediator.Setup<GetAllArtistsRequest, IEnumerable<ArtistDto>>().Returns(new List<ArtistDto> { artist });
        _mediator.Setup<GetTracksByArtistListRequest, IEnumerable<TrackDto>>().Returns(tracks);

        List<TrackDto>? loaded = null;
        _player.Setup(p => p.LoadPlaylist(It.IsAny<List<TrackDto>>(), It.IsAny<TrackDto>()))
               .Callback<List<TrackDto>, TrackDto>((played, _) => loaded = played);

        PlayerCommandService sut = BuildService();

        // Act
        SurprisePick? pick = await sut.SurpriseArtistAsync();

        // Assert
        Assert.NotNull(pick);
        Assert.Equal("artist", pick.Kind);
        Assert.Equal(42, pick.Id);
        Assert.Equal("Etienne daho", pick.Name);
        Assert.Null(pick.ArtistName);
        Assert.Equal(3, pick.TrackCount);
        Assert.NotNull(loaded);
        Assert.Equal([1L, 2L, 3L], loaded.Select(track => track.Id).Order());
    }

    [Fact(DisplayName = "SurpriseArtistAsync should return nothing when no artist holds a track")]
    public async Task SurpriseArtistAsync_ShouldReturnNothing_WhenNoTrack()
    {
        // Arrange
        _mediator.Setup<GetAllArtistsRequest, IEnumerable<ArtistDto>>()
                 .Returns(new List<ArtistDto> { new() { Id = 1, Name = "Silent" } });
        _mediator.Setup<GetTracksByArtistListRequest, IEnumerable<TrackDto>>().Returns(new List<TrackDto>());

        PlayerCommandService sut = BuildService();

        // Act
        SurprisePick? pick = await sut.SurpriseArtistAsync();

        // Assert
        Assert.Null(pick);
        _player.Verify(p => p.LoadPlaylist(It.IsAny<List<TrackDto>>(), It.IsAny<TrackDto>()), Times.Never);
    }
}