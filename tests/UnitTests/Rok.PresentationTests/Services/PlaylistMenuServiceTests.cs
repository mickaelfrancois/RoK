using CleanArch.DevKit.Mediator.Results;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Dto;
using Rok.Application.Errors;
using Rok.Application.Features.Playlists.PlaylistMenu;
using Rok.Application.Features.Playlists.Requests;
using Rok.Application.Features.Tracks.Requests;
using Rok.Application.Messages;
using Rok.Application.Player;
using Rok.Services;
using Rok.Shared.Enums;

namespace Rok.PresentationTests.Services;

public class PlaylistMenuServiceTests
{
    private readonly FakeMediator _mediator = new();
    private readonly Mock<IPlayerService> _playerService = new();
    private readonly Mock<IStringResourceProvider> _resources = new();
    private readonly IMessenger _messenger = new Messenger();

    private PlaylistMenuService BuildService()
        => new(_mediator, _messenger, _playerService.Object, _resources.Object, NullLogger<PlaylistMenuService>.Instance);

    // --- GetPlaylistMenuItemsAsync ---

    [Fact(DisplayName = "GetPlaylistMenuItemsAsync returns mapped items from mediator on first call")]
    public async Task GetPlaylistMenuItemsAsync_ReturnsMappedItems_OnFirstCall()
    {
        // Arrange
        _mediator.Setup<GetAllPlaylistsRequest, IEnumerable<PlaylistHeaderDto>>()
                 .Returns(new List<PlaylistHeaderDto> { new() { Id = 1, Name = "Mix" } });
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
        _mediator.Setup<GetAllPlaylistsRequest, IEnumerable<PlaylistHeaderDto>>()
                 .Returns(new List<PlaylistHeaderDto> { new() { Id = 1, Name = "Mix" } });
        PlaylistMenuService sut = BuildService();

        // Act
        await sut.GetPlaylistMenuItemsAsync();
        await sut.GetPlaylistMenuItemsAsync();

        // Assert
        Assert.Single(_mediator.Sent<GetAllPlaylistsRequest>());
    }

    [Fact(DisplayName = "GetPlaylistMenuItemsAsync re-queries mediator after PlaylistUpdatedMessage invalidates the cache")]
    public async Task GetPlaylistMenuItemsAsync_Requeries_AfterCacheInvalidation()
    {
        // Arrange
        _mediator.Setup<GetAllPlaylistsRequest, IEnumerable<PlaylistHeaderDto>>()
                 .Returns(new List<PlaylistHeaderDto> { new() { Id = 1, Name = "Mix" } });
        PlaylistMenuService sut = BuildService();

        // Act
        await sut.GetPlaylistMenuItemsAsync();
        _messenger.Send(new PlaylistUpdatedMessage(1, ActionType.Update));
        await sut.GetPlaylistMenuItemsAsync();

        // Assert
        Assert.Equal(2, _mediator.Sent<GetAllPlaylistsRequest>().Count);
    }

    // --- AddTrackToPlaylistAsync ---

    [Fact(DisplayName = "AddTrackToPlaylistAsync sends PlaylistUpdatedMessage and success notification on success")]
    public async Task AddTrackToPlaylistAsync_SendsUpdatedAndSuccess_WhenSuccessful()
    {
        // Arrange
        _mediator.Setup<AddTrackToPlaylistRequest, Result<long>>()
                 .Returns(Result<long>.Ok(1));
        PlaylistUpdatedMessage? update = null;
        ShowNotificationMessage? notification = null;
        void ListenUpdate(PlaylistUpdatedMessage m) => update = m;
        void ListenNotify(ShowNotificationMessage m) => notification = m;
        _messenger.Subscribe<PlaylistUpdatedMessage>(ListenUpdate);
        _messenger.Subscribe<ShowNotificationMessage>(ListenNotify);
        try
        {
            PlaylistMenuService sut = BuildService();

            // Act
            await sut.AddTrackToPlaylistAsync(playlistId: 5, trackId: 10);

            // Assert
            AddTrackToPlaylistRequest sent = Assert.Single(_mediator.Sent<AddTrackToPlaylistRequest>());
            Assert.Equal(5, sent.PlaylistId);
            Assert.Equal(10, sent.TrackId);
            Assert.NotNull(update);
            Assert.Equal(5, update!.Id);
            Assert.Equal(NotificationType.Success, notification!.Type);
        }
        finally
        {
            // _messenger is instance-scoped to this test, no manual unsubscribe needed
        }
    }

    [Fact(DisplayName = "AddTrackToPlaylistAsync sends warning notification when track is already in the playlist")]
    public async Task AddTrackToPlaylistAsync_SendsWarning_WhenDuplicate()
    {
        // Arrange
        _mediator.Setup<AddTrackToPlaylistRequest, Result<long>>()
                 .Returns(Result<long>.Fail(new ConflictError("playlist.duplicate_track", "Already in playlist")));
        ShowNotificationMessage? notification = null;
        void Listen(ShowNotificationMessage m) => notification = m;
        _messenger.Subscribe<ShowNotificationMessage>(Listen);
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
            // _messenger is instance-scoped to this test, no manual unsubscribe needed
        }
    }

    [Fact(DisplayName = "AddTrackToPlaylistAsync sends error notification when mediator returns a non-duplicate error")]
    public async Task AddTrackToPlaylistAsync_SendsError_WhenMediatorFails()
    {
        // Arrange
        _mediator.Setup<AddTrackToPlaylistRequest, Result<long>>()
                 .Returns(Result<long>.Fail(new OperationError("test.db_error", "Database error")));
        ShowNotificationMessage? notification = null;
        void Listen(ShowNotificationMessage m) => notification = m;
        _messenger.Subscribe<ShowNotificationMessage>(Listen);
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
            // _messenger is instance-scoped to this test, no manual unsubscribe needed
        }
    }

    // --- CreateNewPlaylistWithArtistAsync ---

    [Fact(DisplayName = "CreateNewPlaylistWithArtistAsync dispatches AddArtistToPlaylistRequest not AddAlbumToPlaylistRequest")]
    public async Task CreateNewPlaylistWithArtistAsync_DispatchesAddArtistCommand()
    {
        // Arrange
        _mediator.Setup<CreatePlaylistRequest, Result<long>>()
                 .Returns(Result<long>.Ok(42));
        _mediator.Setup<AddArtistToPlaylistRequest, Result<long>>()
                 .Returns(Result<long>.Ok(1));
        PlaylistMenuService sut = BuildService();

        // Act
        await sut.CreateNewPlaylistWithArtistAsync("Rock mix", artistId: 7);

        // Assert
        AddArtistToPlaylistRequest sent = Assert.Single(_mediator.Sent<AddArtistToPlaylistRequest>());
        Assert.Equal(42, sent.PlaylistId);
        Assert.Equal(7, sent.ArtistId);
        Assert.Empty(_mediator.Sent<AddAlbumToPlaylistRequest>());
    }

    // --- AddArtistToCurrentListeningAsync ---

    [Fact(DisplayName = "AddArtistToCurrentListeningAsync calls player service with the artist tracks")]
    public async Task AddArtistToCurrentListeningAsync_CallsPlayerService_WhenTracksFound()
    {
        // Arrange
        List<TrackDto> tracks = new() { new TrackDto { Id = 1 }, new TrackDto { Id = 2 } };
        _mediator.Setup<GetTracksByArtistIdRequest, IEnumerable<TrackDto>>()
                 .Returns(tracks);
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
        _mediator.Setup<GetTracksByArtistIdRequest, IEnumerable<TrackDto>>()
                 .Returns(Enumerable.Empty<TrackDto>());
        ShowNotificationMessage? notification = null;
        void Listen(ShowNotificationMessage m) => notification = m;
        _messenger.Subscribe<ShowNotificationMessage>(Listen);
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
            // _messenger is instance-scoped to this test, no manual unsubscribe needed
        }
    }

    // --- AddAlbumToCurrentListeningAsync ---

    [Fact(DisplayName = "AddAlbumToCurrentListeningAsync calls player service with album tracks")]
    public async Task AddAlbumToCurrentListeningAsync_CallsPlayerService_WhenTracksFound()
    {
        // Arrange
        List<TrackDto> tracks = new() { new TrackDto { Id = 10 }, new TrackDto { Id = 11 } };
        _mediator.Setup<GetTracksByAlbumIdRequest, IEnumerable<TrackDto>>()
                 .Returns(tracks);
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
        _mediator.Setup<GetTracksByAlbumIdRequest, IEnumerable<TrackDto>>()
                 .Returns(Enumerable.Empty<TrackDto>());
        ShowNotificationMessage? notification = null;
        void Listen(ShowNotificationMessage m) => notification = m;
        _messenger.Subscribe<ShowNotificationMessage>(Listen);
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
            // _messenger is instance-scoped to this test, no manual unsubscribe needed
        }
    }

    // --- AddTrackToCurrentListeningAsync ---

    [Fact(DisplayName = "AddTrackToCurrentListeningAsync calls player service when track is found")]
    public async Task AddTrackToCurrentListeningAsync_CallsPlayerService_WhenTrackFound()
    {
        // Arrange
        TrackDto track = new() { Id = 99 };
        _mediator.Setup<GetTrackByIdRequest, Result<TrackDto>>()
                 .Returns(Result<TrackDto>.Ok(track));
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
        _mediator.Setup<GetTrackByIdRequest, Result<TrackDto>>()
                 .Returns(Result<TrackDto>.Fail(NotFoundError.ForEntity("Track", 1L)));
        ShowNotificationMessage? notification = null;
        void Listen(ShowNotificationMessage m) => notification = m;
        _messenger.Subscribe<ShowNotificationMessage>(Listen);
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
            // _messenger is instance-scoped to this test, no manual unsubscribe needed
        }
    }
}