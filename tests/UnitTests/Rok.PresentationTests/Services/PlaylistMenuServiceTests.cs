using MiF.Mediator.Interfaces;
using MiF.Result;
using MiF.SimpleMessenger;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Dto;
using Rok.Application.Features.Playlists.Command;
using Rok.Application.Features.Playlists.PlaylistMenu;
using Rok.Application.Features.Playlists.Query;
using Rok.Application.Features.Tracks.Query;
using Rok.Application.Messages;
using Rok.Application.Player;
using Rok.Services;
using Rok.Shared.Enums;

namespace Rok.PresentationTests.Services;

public class PlaylistMenuServiceTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IPlayerService> _playerService = new();
    private readonly Mock<IStringResourceProvider> _resources = new();

    private PlaylistMenuService BuildService()
        => new(_mediator.Object, _playerService.Object, _resources.Object, NullLogger<PlaylistMenuService>.Instance);

    // --- GetPlaylistMenuItemsAsync ---

    [Fact(DisplayName = "GetPlaylistMenuItemsAsync returns mapped items from mediator on first call")]
    public async Task GetPlaylistMenuItemsAsync_ReturnsMappedItems_OnFirstCall()
    {
        // Arrange
        _mediator.Setup(m => m.SendMessageAsync(It.IsAny<GetAllPlaylistsQuery>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<PlaylistHeaderDto> { new() { Id = 1, Name = "Mix" } });
        PlaylistMenuService sut = BuildService();

        // Act
        IEnumerable<PlaylistMenuItem> items = await sut.GetPlaylistMenuItemsAsync();

        // Assert
        Assert.Single(items);
        PlaylistMenuItem item = items.First();
        Assert.Equal(1, item.Id);
        Assert.Equal("Mix", item.Name);
    }

    [Fact(DisplayName = "GetPlaylistMenuItemsAsync queries mediator only once when called twice")]
    public async Task GetPlaylistMenuItemsAsync_QueriesMediatorOnce_WhenCalledTwice()
    {
        // Arrange
        _mediator.Setup(m => m.SendMessageAsync(It.IsAny<GetAllPlaylistsQuery>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<PlaylistHeaderDto> { new() { Id = 1, Name = "Mix" } });
        PlaylistMenuService sut = BuildService();

        // Act
        await sut.GetPlaylistMenuItemsAsync();
        await sut.GetPlaylistMenuItemsAsync();

        // Assert
        _mediator.Verify(m => m.SendMessageAsync(It.IsAny<GetAllPlaylistsQuery>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "GetPlaylistMenuItemsAsync re-queries mediator after PlaylistUpdatedMessage invalidates the cache")]
    public async Task GetPlaylistMenuItemsAsync_Requeries_AfterCacheInvalidation()
    {
        // Arrange
        _mediator.Setup(m => m.SendMessageAsync(It.IsAny<GetAllPlaylistsQuery>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<PlaylistHeaderDto> { new() { Id = 1, Name = "Mix" } });
        PlaylistMenuService sut = BuildService();

        // Act
        await sut.GetPlaylistMenuItemsAsync();
        Messenger.Send(new PlaylistUpdatedMessage(1, ActionType.Update));
        await sut.GetPlaylistMenuItemsAsync();

        // Assert
        _mediator.Verify(m => m.SendMessageAsync(It.IsAny<GetAllPlaylistsQuery>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    // --- AddTrackToPlaylistAsync ---

    [Fact(DisplayName = "AddTrackToPlaylistAsync sends PlaylistUpdatedMessage and success notification on success")]
    public async Task AddTrackToPlaylistAsync_SendsUpdatedAndSuccess_WhenSuccessful()
    {
        // Arrange
        _mediator.Setup(m => m.SendMessageAsync(It.IsAny<AddTrackToPlaylistCommand>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(Result<long>.Success(1));
        PlaylistUpdatedMessage? update = null;
        ShowNotificationMessage? notification = null;
        void ListenUpdate(PlaylistUpdatedMessage m) => update = m;
        void ListenNotify(ShowNotificationMessage m) => notification = m;
        Messenger.Subscribe<PlaylistUpdatedMessage>(ListenUpdate);
        Messenger.Subscribe<ShowNotificationMessage>(ListenNotify);
        try
        {
            PlaylistMenuService sut = BuildService();

            // Act
            await sut.AddTrackToPlaylistAsync(playlistId: 5, trackId: 10);

            // Assert
            _mediator.Verify(m => m.SendMessageAsync(
                It.Is<AddTrackToPlaylistCommand>(c => c.PlaylistId == 5 && c.TrackId == 10),
                It.IsAny<CancellationToken>()), Times.Once);
            Assert.NotNull(update);
            Assert.Equal(5, update!.Id);
            Assert.Equal(NotificationType.Success, notification!.Type);
        }
        finally
        {
            try { Messenger.Unsubscribe<PlaylistUpdatedMessage>(ListenUpdate); } catch { }
            try { Messenger.Unsubscribe<ShowNotificationMessage>(ListenNotify); } catch { }
        }
    }

    [Fact(DisplayName = "AddTrackToPlaylistAsync sends warning notification when track is already in the playlist")]
    public async Task AddTrackToPlaylistAsync_SendsWarning_WhenDuplicate()
    {
        // Arrange
        _mediator.Setup(m => m.SendMessageAsync(It.IsAny<AddTrackToPlaylistCommand>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(Result<long>.Fail("DUPLICATE", "Already in playlist"));
        ShowNotificationMessage? notification = null;
        void Listen(ShowNotificationMessage m) => notification = m;
        Messenger.Subscribe<ShowNotificationMessage>(Listen);
        try
        {
            PlaylistMenuService sut = BuildService();

            // Act
            await sut.AddTrackToPlaylistAsync(playlistId: 5, trackId: 10);

            // Assert
            Assert.NotNull(notification);
            Assert.Equal(NotificationType.Warning, notification!.Type);
        }
        finally
        {
            try { Messenger.Unsubscribe<ShowNotificationMessage>(Listen); } catch { }
        }
    }

    [Fact(DisplayName = "AddTrackToPlaylistAsync sends error notification when mediator returns a non-duplicate error")]
    public async Task AddTrackToPlaylistAsync_SendsError_WhenMediatorFails()
    {
        // Arrange
        _mediator.Setup(m => m.SendMessageAsync(It.IsAny<AddTrackToPlaylistCommand>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(Result<long>.Fail("DB_ERROR", "Database error"));
        ShowNotificationMessage? notification = null;
        void Listen(ShowNotificationMessage m) => notification = m;
        Messenger.Subscribe<ShowNotificationMessage>(Listen);
        try
        {
            PlaylistMenuService sut = BuildService();

            // Act
            await sut.AddTrackToPlaylistAsync(playlistId: 5, trackId: 10);

            // Assert
            Assert.NotNull(notification);
            Assert.Equal(NotificationType.Error, notification!.Type);
        }
        finally
        {
            try { Messenger.Unsubscribe<ShowNotificationMessage>(Listen); } catch { }
        }
    }

    // --- CreateNewPlaylistWithArtistAsync ---

    [Fact(DisplayName = "CreateNewPlaylistWithArtistAsync dispatches AddArtistToPlaylistCommand not AddAlbumToPlaylistCommand")]
    public async Task CreateNewPlaylistWithArtistAsync_DispatchesAddArtistCommand()
    {
        // Arrange
        _mediator.Setup(m => m.SendMessageAsync(It.IsAny<CreatePlaylistCommand>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(Result<long>.Success(42));
        _mediator.Setup(m => m.SendMessageAsync(It.IsAny<AddArtistToPlaylistCommand>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(Result<long>.Success(1));
        PlaylistMenuService sut = BuildService();

        // Act
        await sut.CreateNewPlaylistWithArtistAsync("Rock mix", artistId: 7);

        // Assert
        _mediator.Verify(m => m.SendMessageAsync(
            It.Is<AddArtistToPlaylistCommand>(c => c.PlaylistId == 42 && c.ArtistId == 7),
            It.IsAny<CancellationToken>()), Times.Once);
        _mediator.Verify(m => m.SendMessageAsync(
            It.IsAny<AddAlbumToPlaylistCommand>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    // --- AddArtistToCurrentListeningAsync ---

    [Fact(DisplayName = "AddArtistToCurrentListeningAsync calls player service with the artist tracks")]
    public async Task AddArtistToCurrentListeningAsync_CallsPlayerService_WhenTracksFound()
    {
        // Arrange
        List<TrackDto> tracks = new() { new TrackDto { Id = 1 }, new TrackDto { Id = 2 } };
        _mediator.Setup(m => m.SendMessageAsync(It.IsAny<GetTracksByArtistIdQuery>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(tracks);
        PlaylistMenuService sut = BuildService();

        // Act
        await sut.AddArtistToCurrentListeningAsync(artistId: 3);

        // Assert
        _playerService.Verify(p => p.AddTracksToPlaylist(It.Is<List<TrackDto>>(l => l.Count == 2)), Times.Once);
    }

    [Fact(DisplayName = "AddArtistToCurrentListeningAsync sends error notification when no tracks found for artist")]
    public async Task AddArtistToCurrentListeningAsync_SendsError_WhenNoTracks()
    {
        // Arrange
        _mediator.Setup(m => m.SendMessageAsync(It.IsAny<GetTracksByArtistIdQuery>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(Enumerable.Empty<TrackDto>());
        ShowNotificationMessage? notification = null;
        void Listen(ShowNotificationMessage m) => notification = m;
        Messenger.Subscribe<ShowNotificationMessage>(Listen);
        try
        {
            PlaylistMenuService sut = BuildService();

            // Act
            await sut.AddArtistToCurrentListeningAsync(artistId: 3);

            // Assert
            Assert.NotNull(notification);
            Assert.Equal(NotificationType.Error, notification!.Type);
            _playerService.Verify(p => p.AddTracksToPlaylist(It.IsAny<List<TrackDto>>()), Times.Never);
        }
        finally
        {
            try { Messenger.Unsubscribe<ShowNotificationMessage>(Listen); } catch { }
        }
    }

    // --- AddAlbumToCurrentListeningAsync ---

    [Fact(DisplayName = "AddAlbumToCurrentListeningAsync calls player service with album tracks")]
    public async Task AddAlbumToCurrentListeningAsync_CallsPlayerService_WhenTracksFound()
    {
        // Arrange
        List<TrackDto> tracks = new() { new TrackDto { Id = 10 }, new TrackDto { Id = 11 } };
        _mediator.Setup(m => m.SendMessageAsync(It.IsAny<GetTracksByAlbumIdQuery>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(tracks);
        PlaylistMenuService sut = BuildService();

        // Act
        await sut.AddAlbumToCurrentListeningAsync(albumId: 5);

        // Assert
        _playerService.Verify(p => p.AddTracksToPlaylist(It.Is<List<TrackDto>>(l => l.Count == 2)), Times.Once);
    }

    [Fact(DisplayName = "AddAlbumToCurrentListeningAsync sends error notification when no tracks found for album")]
    public async Task AddAlbumToCurrentListeningAsync_SendsError_WhenNoTracks()
    {
        // Arrange
        _mediator.Setup(m => m.SendMessageAsync(It.IsAny<GetTracksByAlbumIdQuery>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(Enumerable.Empty<TrackDto>());
        ShowNotificationMessage? notification = null;
        void Listen(ShowNotificationMessage m) => notification = m;
        Messenger.Subscribe<ShowNotificationMessage>(Listen);
        try
        {
            PlaylistMenuService sut = BuildService();

            // Act
            await sut.AddAlbumToCurrentListeningAsync(albumId: 5);

            // Assert
            Assert.NotNull(notification);
            Assert.Equal(NotificationType.Error, notification!.Type);
            _playerService.Verify(p => p.AddTracksToPlaylist(It.IsAny<List<TrackDto>>()), Times.Never);
        }
        finally
        {
            try { Messenger.Unsubscribe<ShowNotificationMessage>(Listen); } catch { }
        }
    }

    // --- AddTrackToCurrentListeningAsync ---

    [Fact(DisplayName = "AddTrackToCurrentListeningAsync calls player service when track is found")]
    public async Task AddTrackToCurrentListeningAsync_CallsPlayerService_WhenTrackFound()
    {
        // Arrange
        TrackDto track = new() { Id = 99 };
        _mediator.Setup(m => m.SendMessageAsync(It.IsAny<GetTrackByIdQuery>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(Result<TrackDto>.Success(track));
        PlaylistMenuService sut = BuildService();

        // Act
        await sut.AddTrackToCurrentListeningAsync(trackId: 99);

        // Assert
        _playerService.Verify(p => p.AddTracksToPlaylist(
            It.Is<List<TrackDto>>(l => l.Count == 1 && l[0].Id == 99)), Times.Once);
    }

    [Fact(DisplayName = "AddTrackToCurrentListeningAsync sends error notification when track is not found")]
    public async Task AddTrackToCurrentListeningAsync_SendsError_WhenTrackNotFound()
    {
        // Arrange
        _mediator.Setup(m => m.SendMessageAsync(It.IsAny<GetTrackByIdQuery>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(Result<TrackDto>.Fail("NOT_FOUND", "Track not found"));
        ShowNotificationMessage? notification = null;
        void Listen(ShowNotificationMessage m) => notification = m;
        Messenger.Subscribe<ShowNotificationMessage>(Listen);
        try
        {
            PlaylistMenuService sut = BuildService();

            // Act
            await sut.AddTrackToCurrentListeningAsync(trackId: 99);

            // Assert
            Assert.NotNull(notification);
            Assert.Equal(NotificationType.Error, notification!.Type);
            _playerService.Verify(p => p.AddTracksToPlaylist(It.IsAny<List<TrackDto>>()), Times.Never);
        }
        finally
        {
            try { Messenger.Unsubscribe<ShowNotificationMessage>(Listen); } catch { }
        }
    }
}
