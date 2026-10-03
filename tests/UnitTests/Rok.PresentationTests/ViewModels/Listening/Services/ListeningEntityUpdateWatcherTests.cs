using Rok.Application.Messages;
using Rok.Shared.Enums;
using Rok.ViewModels.Listening.Services;

namespace Rok.PresentationTests.ViewModels.Listening.Services;

public class ListeningEntityUpdateWatcherTests
{
    private readonly Messenger _messenger = new();

    private ListeningEntityUpdateWatcher CreateWatcher() => new(_messenger);

    [Fact(DisplayName = "watcher_raises_artist_updated_when_tracked_artist_changes")]
    public void Watcher_RaisesArtistUpdated_WhenTrackedArtistChanges()
    {
        // Arrange
        using ListeningEntityUpdateWatcher sut = CreateWatcher();
        List<long> raised = [];
        sut.TrackedArtistUpdated += (_, id) => raised.Add(id);
        sut.SetTracked(7, null);

        // Act
        _messenger.Send(new ArtistUpdateMessage(7, ActionType.Update));

        // Assert
        Assert.Equal([7L], raised);
    }

    [Fact(DisplayName = "watcher_raises_album_updated_when_tracked_album_changes")]
    public void Watcher_RaisesAlbumUpdated_WhenTrackedAlbumChanges()
    {
        // Arrange
        using ListeningEntityUpdateWatcher sut = CreateWatcher();
        List<long> raised = [];
        sut.TrackedAlbumUpdated += (_, id) => raised.Add(id);
        sut.SetTracked(null, 3);

        // Act
        _messenger.Send(new AlbumUpdateMessage(3, ActionType.Update));

        // Assert
        Assert.Equal([3L], raised);
    }

    [Fact(DisplayName = "watcher_ignores_update_for_another_entity")]
    public void Watcher_IgnoresUpdate_ForAnotherEntity()
    {
        // Arrange
        using ListeningEntityUpdateWatcher sut = CreateWatcher();
        int raised = 0;
        sut.TrackedArtistUpdated += (_, _) => raised++;
        sut.TrackedAlbumUpdated += (_, _) => raised++;
        sut.SetTracked(7, 7);

        // Act
        _messenger.Send(new ArtistUpdateMessage(8, ActionType.Update));
        _messenger.Send(new AlbumUpdateMessage(8, ActionType.Update));

        // Assert
        Assert.Equal(0, raised);
    }

    [Theory(DisplayName = "watcher_ignores_add_and_delete_actions")]
    [InlineData(ActionType.Add, false)]
    [InlineData(ActionType.Delete, false)]
    [InlineData(ActionType.Picture, true)]
    public void Watcher_FiltersActions(ActionType action, bool expectRaised)
    {
        // Arrange
        using ListeningEntityUpdateWatcher sut = CreateWatcher();
        int raised = 0;
        sut.TrackedArtistUpdated += (_, _) => raised++;
        sut.TrackedAlbumUpdated += (_, _) => raised++;
        sut.SetTracked(7, 7);

        // Act
        _messenger.Send(new ArtistUpdateMessage(7, action));
        _messenger.Send(new AlbumUpdateMessage(7, action));

        // Assert
        Assert.Equal(expectRaised ? 2 : 0, raised);
    }

    [Fact(DisplayName = "watcher_ignores_messages_when_nothing_is_tracked")]
    public void Watcher_IgnoresMessages_WhenNothingIsTracked()
    {
        // Arrange
        using ListeningEntityUpdateWatcher sut = CreateWatcher();
        int raised = 0;
        sut.TrackedArtistUpdated += (_, _) => raised++;
        sut.TrackedAlbumUpdated += (_, _) => raised++;
        sut.SetTracked(null, null);

        // Act
        _messenger.Send(new ArtistUpdateMessage(7, ActionType.Update));
        _messenger.Send(new AlbumUpdateMessage(7, ActionType.Update));

        // Assert
        Assert.Equal(0, raised);
    }

    [Fact(DisplayName = "watcher_follows_new_tracked_ids")]
    public void Watcher_FollowsNewTrackedIds()
    {
        // Arrange
        using ListeningEntityUpdateWatcher sut = CreateWatcher();
        List<long> raised = [];
        sut.TrackedArtistUpdated += (_, id) => raised.Add(id);
        sut.SetTracked(7, null);
        sut.SetTracked(9, null);

        // Act
        _messenger.Send(new ArtistUpdateMessage(7, ActionType.Update));
        _messenger.Send(new ArtistUpdateMessage(9, ActionType.Update));

        // Assert
        Assert.Equal([9L], raised);
    }

    [Fact(DisplayName = "watcher_dispose_unsubscribes_from_messenger")]
    public void Watcher_Dispose_UnsubscribesFromMessenger()
    {
        // Arrange
        ListeningEntityUpdateWatcher sut = CreateWatcher();
        int raised = 0;
        sut.TrackedArtistUpdated += (_, _) => raised++;
        sut.TrackedAlbumUpdated += (_, _) => raised++;
        sut.SetTracked(7, 7);

        // Act
        sut.Dispose();
        _messenger.Send(new ArtistUpdateMessage(7, ActionType.Update));
        _messenger.Send(new AlbumUpdateMessage(7, ActionType.Update));

        // Assert
        Assert.Equal(0, raised);
    }
}