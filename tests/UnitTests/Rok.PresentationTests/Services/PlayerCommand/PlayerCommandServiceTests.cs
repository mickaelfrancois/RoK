using MiF.Mediator.Interfaces;
using Moq;
using Rok.Application.Dto;
using Rok.Application.Features.Albums.Query;
using Rok.Application.Features.Artists.Query;
using Rok.Application.Features.Genres.Query;
using Rok.Application.Features.Playlists.Query;
using Rok.Application.Features.Tracks.Query;
using Rok.Application.Player;
using Rok.Services.PlayerCommand;

namespace Rok.PresentationTests.Services.PlayerCommand;

public class PlayerCommandServiceTests
{
    private readonly Mock<IPlayerService> _player = new();
    private readonly Mock<IMediator> _mediator = new();

    private PlayerCommandService BuildService() => new(_player.Object, _mediator.Object);

    [Fact(DisplayName = "Play should delegate to the player service")]
    public void Play_ShouldDelegateToPlayer()
    {
        // Arrange
        PlayerCommandService sut = BuildService();

        // Act
        sut.Play();

        // Assert
        _player.Verify(p => p.Play(), Times.Once);
    }

    [Fact(DisplayName = "Pause should delegate to the player service")]
    public void Pause_ShouldDelegateToPlayer()
    {
        // Arrange
        PlayerCommandService sut = BuildService();

        // Act
        sut.Pause();

        // Assert
        _player.Verify(p => p.Pause(), Times.Once);
    }

    [Fact(DisplayName = "Next should delegate to player Skip")]
    public void Next_ShouldDelegateToPlayerSkip()
    {
        // Arrange
        PlayerCommandService sut = BuildService();

        // Act
        sut.Next();

        // Assert
        _player.Verify(p => p.Skip(), Times.Once);
    }

    [Fact(DisplayName = "Previous should delegate to player Previous")]
    public void Previous_ShouldDelegateToPlayer()
    {
        // Arrange
        PlayerCommandService sut = BuildService();

        // Act
        sut.Previous();

        // Assert
        _player.Verify(p => p.Previous(), Times.Once);
    }

    [Fact(DisplayName = "ToggleMute should invert the player IsMuted property")]
    public void ToggleMute_ShouldInvertIsMuted()
    {
        // Arrange
        _player.SetupProperty(p => p.IsMuted, false);
        PlayerCommandService sut = BuildService();

        // Act
        sut.ToggleMute();

        // Assert
        Assert.True(_player.Object.IsMuted);
    }

    [Theory(DisplayName = "Toggle should pause when playing and play otherwise")]
    [InlineData(EPlaybackState.Playing, false, true)]
    [InlineData(EPlaybackState.Paused, true, false)]
    [InlineData(EPlaybackState.Stopped, true, false)]
    public void Toggle_ShouldRouteByPlaybackState(EPlaybackState state, bool expectPlay, bool expectPause)
    {
        // Arrange
        _player.SetupGet(p => p.PlaybackState).Returns(state);
        PlayerCommandService sut = BuildService();

        // Act
        sut.Toggle();

        // Assert
        _player.Verify(p => p.Play(), expectPlay ? Times.Once() : Times.Never());
        _player.Verify(p => p.Pause(), expectPause ? Times.Once() : Times.Never());
    }

    [Theory(DisplayName = "SetVolume should clamp the value between 0 and 100")]
    [InlineData(50d, 50d)]
    [InlineData(-10d, 0d)]
    [InlineData(150d, 100d)]
    public void SetVolume_ShouldClampToZeroToHundred(double input, double expected)
    {
        // Arrange
        _player.SetupProperty(p => p.Volume, 0d);
        PlayerCommandService sut = BuildService();

        // Act
        sut.SetVolume(input);

        // Assert
        Assert.Equal(expected, _player.Object.Volume);
    }

    [Fact(DisplayName = "ListenPlaylistAsync should return false when no playlist matches the name")]
    public async Task ListenPlaylistAsync_ShouldReturnFalse_WhenNoMatch()
    {
        // Arrange
        _mediator.Setup(m => m.SendMessageAsync(It.IsAny<GetAllPlaylistsQuery>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<PlaylistHeaderDto>());
        PlayerCommandService sut = BuildService();

        // Act
        bool result = await sut.ListenPlaylistAsync("missing");

        // Assert
        Assert.False(result);
        _player.Verify(p => p.LoadPlaylist(It.IsAny<List<TrackDto>>(), It.IsAny<TrackDto>()), Times.Never);
    }

    [Fact(DisplayName = "ListenPlaylistAsync should return false when matching playlist has no tracks")]
    public async Task ListenPlaylistAsync_ShouldReturnFalse_WhenNoTracks()
    {
        // Arrange
        PlaylistHeaderDto playlist = new() { Id = 1, Name = "MyMix" };
        _mediator.Setup(m => m.SendMessageAsync(It.IsAny<GetAllPlaylistsQuery>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<PlaylistHeaderDto> { playlist });
        _mediator.Setup(m => m.SendMessageAsync(It.IsAny<GetTracksByPlaylistIdQuery>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<TrackDto>());
        PlayerCommandService sut = BuildService();

        // Act
        bool result = await sut.ListenPlaylistAsync("MyMix");

        // Assert
        Assert.False(result);
        _player.Verify(p => p.LoadPlaylist(It.IsAny<List<TrackDto>>(), It.IsAny<TrackDto>()), Times.Never);
    }

    [Fact(DisplayName = "ListenPlaylistAsync should load and play tracks when playlist has tracks")]
    public async Task ListenPlaylistAsync_ShouldLoadAndPlay_WhenTracksExist()
    {
        // Arrange
        PlaylistHeaderDto playlist = new() { Id = 1, Name = "MyMix" };
        List<TrackDto> tracks = new() { new TrackDto { Id = 10 } };
        _mediator.Setup(m => m.SendMessageAsync(It.IsAny<GetAllPlaylistsQuery>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<PlaylistHeaderDto> { playlist });
        _mediator.Setup(m => m.SendMessageAsync(It.IsAny<GetTracksByPlaylistIdQuery>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(tracks);
        PlayerCommandService sut = BuildService();

        // Act
        bool result = await sut.ListenPlaylistAsync("mymix");

        // Assert
        Assert.True(result);
        _player.Verify(p => p.LoadPlaylist(It.IsAny<List<TrackDto>>(), It.IsAny<TrackDto>()), Times.Once);
        _player.Verify(p => p.Play(), Times.Once);
    }

    [Fact(DisplayName = "ListenAlbumAsync should return false when album does not exist")]
    public async Task ListenAlbumAsync_ShouldReturnFalse_WhenAlbumMissing()
    {
        // Arrange
        _mediator.Setup(m => m.SendMessageAsync(It.IsAny<GetAllAlbumsQuery>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<AlbumDto>());
        PlayerCommandService sut = BuildService();

        // Act
        bool result = await sut.ListenAlbumAsync("missing");

        // Assert
        Assert.False(result);
    }

    [Fact(DisplayName = "ListenAlbumAsync should load and play tracks when album exists")]
    public async Task ListenAlbumAsync_ShouldLoadAndPlay_WhenAlbumExists()
    {
        // Arrange
        AlbumDto album = new() { Id = 1, Name = "Best Of" };
        List<TrackDto> tracks = new() { new TrackDto { Id = 10 } };
        _mediator.Setup(m => m.SendMessageAsync(It.IsAny<GetAllAlbumsQuery>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<AlbumDto> { album });
        _mediator.Setup(m => m.SendMessageAsync(It.IsAny<GetTracksByAlbumIdQuery>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(tracks);
        PlayerCommandService sut = BuildService();

        // Act
        bool result = await sut.ListenAlbumAsync("Best Of");

        // Assert
        Assert.True(result);
        _player.Verify(p => p.LoadPlaylist(It.IsAny<List<TrackDto>>(), It.IsAny<TrackDto>()), Times.Once);
        _player.Verify(p => p.Play(), Times.Once);
    }

    [Fact(DisplayName = "ListenArtistAsync should return false when artist does not exist")]
    public async Task ListenArtistAsync_ShouldReturnFalse_WhenArtistMissing()
    {
        // Arrange
        _mediator.Setup(m => m.SendMessageAsync(It.IsAny<GetAllArtistsQuery>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<ArtistDto>());
        PlayerCommandService sut = BuildService();

        // Act
        bool result = await sut.ListenArtistAsync("missing");

        // Assert
        Assert.False(result);
    }

    [Fact(DisplayName = "ListenArtistAsync should query tracks by the resolved artist id")]
    public async Task ListenArtistAsync_ShouldQueryTracksByArtistId()
    {
        // Arrange
        ArtistDto artist = new() { Id = 7, Name = "Artist X" };
        List<TrackDto> tracks = new() { new TrackDto { Id = 10 } };
        _mediator.Setup(m => m.SendMessageAsync(It.IsAny<GetAllArtistsQuery>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<ArtistDto> { artist });
        _mediator.Setup(m => m.SendMessageAsync(It.Is<GetTracksByArtistIdQuery>(q => q.ArtistId == 7), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(tracks);
        PlayerCommandService sut = BuildService();

        // Act
        bool result = await sut.ListenArtistAsync("Artist X");

        // Assert
        Assert.True(result);
        _mediator.Verify(m => m.SendMessageAsync(It.Is<GetTracksByArtistIdQuery>(q => q.ArtistId == 7), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "ListenGenreAsync should return false when genre does not exist")]
    public async Task ListenGenreAsync_ShouldReturnFalse_WhenGenreMissing()
    {
        // Arrange
        _mediator.Setup(m => m.SendMessageAsync(It.IsAny<GetAllGenresQuery>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<GenreDto>());
        PlayerCommandService sut = BuildService();

        // Act
        bool result = await sut.ListenGenreAsync("missing");

        // Assert
        Assert.False(result);
    }

    [Fact(DisplayName = "ListenGenreAsync should query tracks by the resolved genre id")]
    public async Task ListenGenreAsync_ShouldQueryTracksByGenreId()
    {
        // Arrange
        GenreDto genre = new() { Id = 3, Name = "Rock" };
        List<TrackDto> tracks = new() { new TrackDto { Id = 10 } };
        _mediator.Setup(m => m.SendMessageAsync(It.IsAny<GetAllGenresQuery>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<GenreDto> { genre });
        _mediator.Setup(m => m.SendMessageAsync(It.Is<GetTracksByGenreIdQuery>(q => q.GenreId == 3), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(tracks);
        PlayerCommandService sut = BuildService();

        // Act
        bool result = await sut.ListenGenreAsync("Rock");

        // Assert
        Assert.True(result);
        _mediator.Verify(m => m.SendMessageAsync(It.Is<GetTracksByGenreIdQuery>(q => q.GenreId == 3), It.IsAny<CancellationToken>()), Times.Once);
    }
}
