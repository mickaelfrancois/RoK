using Rok.Application.Features.Playlists;
using Rok.Shared.Enums;

namespace Rok.ApplicationTests.Features.Playlists;

public class PlaylistPlaybackPolicyTests
{
    [Fact(DisplayName = "ShouldRegenerateBeforePlay should return true for a smart playlist with the flag active and no track clicked")]
    public void ShouldRegenerateBeforePlay_ShouldReturnTrue_WhenSmartFlagActiveAndNoTrackClicked()
    {
        // Arrange
        PlaylistHeaderDto playlist = new() { Type = (int)PlaylistType.Smart, RefreshOnPlay = true };

        // Act
        bool result = PlaylistPlaybackPolicy.ShouldRegenerateBeforePlay(playlist, trackClicked: false);

        // Assert
        Assert.True(result);
    }

    [Fact(DisplayName = "ShouldRegenerateBeforePlay should return false for a smart playlist with the flag active when a track was clicked")]
    public void ShouldRegenerateBeforePlay_ShouldReturnFalse_WhenSmartFlagActiveButTrackClicked()
    {
        // Arrange
        PlaylistHeaderDto playlist = new() { Type = (int)PlaylistType.Smart, RefreshOnPlay = true };

        // Act
        bool result = PlaylistPlaybackPolicy.ShouldRegenerateBeforePlay(playlist, trackClicked: true);

        // Assert
        Assert.False(result);
    }

    [Fact(DisplayName = "ShouldRegenerateBeforePlay should return false for a smart playlist with the flag inactive")]
    public void ShouldRegenerateBeforePlay_ShouldReturnFalse_WhenSmartFlagInactive()
    {
        // Arrange
        PlaylistHeaderDto playlist = new() { Type = (int)PlaylistType.Smart, RefreshOnPlay = false };

        // Act
        bool result = PlaylistPlaybackPolicy.ShouldRegenerateBeforePlay(playlist, trackClicked: false);

        // Assert
        Assert.False(result);
    }

    [Fact(DisplayName = "ShouldRegenerateBeforePlay should return false for a classic playlist even with the flag active")]
    public void ShouldRegenerateBeforePlay_ShouldReturnFalse_WhenClassicPlaylist()
    {
        // Arrange
        PlaylistHeaderDto playlist = new() { Type = (int)PlaylistType.Classic, RefreshOnPlay = true };

        // Act
        bool result = PlaylistPlaybackPolicy.ShouldRegenerateBeforePlay(playlist, trackClicked: false);

        // Assert
        Assert.False(result);
    }
}