using Rok.Application.Features.Playlists;
using Rok.Application.Features.Playlists.Requests;

namespace Rok.ApplicationTests.Features.Playlists;

public class PlaylistHeaderChangeDetectorTests
{
    private static UpdatePlaylistRequest BaseCommand() => new()
    {
        Id = 1,
        Name = "Mix",
        Duration = 100,
        TrackCount = 5,
        TrackMaximum = 20,
        DurationMaximum = 3600,
        Picture = "artist.png",
        ShuffleOnPlay = true,
        RefreshOnPlay = false,
        Groups = new List<PlaylistGroupDto> { new() { Name = "g1" } }
    };

    private static PlaylistHeaderDto MatchingPersisted() => new()
    {
        Id = 1,
        Name = "Mix",
        Duration = 100,
        TrackCount = 5,
        TrackMaximum = 20,
        DurationMaximum = 3600,
        Picture = "artist.png",
        ShuffleOnPlay = true,
        RefreshOnPlay = false,
        GroupsJson = System.Text.Json.JsonSerializer.Serialize(new List<PlaylistGroupDto> { new() { Name = "g1" } })
    };

    [Fact(DisplayName = "HasChanges should return false when persisted state matches the command in every field")]
    public void HasChanges_ShouldReturnFalse_WhenStateIsIdenticalInEveryField()
    {
        // Arrange
        UpdatePlaylistRequest command = BaseCommand();
        PlaylistHeaderDto persisted = MatchingPersisted();

        // Act
        bool result = PlaylistHeaderChangeDetector.HasChanges(command, persisted);

        // Assert
        Assert.False(result);
    }

    [Fact(DisplayName = "HasChanges should return true when only RefreshOnPlay differs")]
    public void HasChanges_ShouldReturnTrue_WhenOnlyRefreshOnPlayDiffers()
    {
        // Arrange
        UpdatePlaylistRequest command = BaseCommand();
        command.RefreshOnPlay = true;
        PlaylistHeaderDto persisted = MatchingPersisted();

        // Act
        bool result = PlaylistHeaderChangeDetector.HasChanges(command, persisted);

        // Assert
        Assert.True(result);
    }

    [Fact(DisplayName = "HasChanges should return true when only ShuffleOnPlay differs")]
    public void HasChanges_ShouldReturnTrue_WhenOnlyShuffleOnPlayDiffers()
    {
        // Arrange
        UpdatePlaylistRequest command = BaseCommand();
        PlaylistHeaderDto persisted = MatchingPersisted();
        persisted.ShuffleOnPlay = false;

        // Act
        bool result = PlaylistHeaderChangeDetector.HasChanges(command, persisted);

        // Assert
        Assert.True(result);
    }

    [Fact(DisplayName = "HasChanges should return true when only Name differs")]
    public void HasChanges_ShouldReturnTrue_WhenOnlyNameDiffers()
    {
        // Arrange
        UpdatePlaylistRequest command = BaseCommand();
        command.Name = "Other name";
        PlaylistHeaderDto persisted = MatchingPersisted();

        // Act
        bool result = PlaylistHeaderChangeDetector.HasChanges(command, persisted);

        // Assert
        Assert.True(result);
    }

    [Fact(DisplayName = "HasChanges should return true when Duration differs")]
    public void HasChanges_ShouldReturnTrue_WhenDurationDiffers()
    {
        // Arrange
        UpdatePlaylistRequest command = BaseCommand();
        command.Duration = 999;
        PlaylistHeaderDto persisted = MatchingPersisted();

        // Act
        bool result = PlaylistHeaderChangeDetector.HasChanges(command, persisted);

        // Assert
        Assert.True(result);
    }

    [Fact(DisplayName = "HasChanges should return true when TrackCount differs")]
    public void HasChanges_ShouldReturnTrue_WhenTrackCountDiffers()
    {
        // Arrange
        UpdatePlaylistRequest command = BaseCommand();
        command.TrackCount = 99;
        PlaylistHeaderDto persisted = MatchingPersisted();

        // Act
        bool result = PlaylistHeaderChangeDetector.HasChanges(command, persisted);

        // Assert
        Assert.True(result);
    }

    [Fact(DisplayName = "HasChanges should return true when Picture differs")]
    public void HasChanges_ShouldReturnTrue_WhenPictureDiffers()
    {
        // Arrange
        UpdatePlaylistRequest command = BaseCommand();
        command.Picture = "other.png";
        PlaylistHeaderDto persisted = MatchingPersisted();

        // Act
        bool result = PlaylistHeaderChangeDetector.HasChanges(command, persisted);

        // Assert
        Assert.True(result);
    }

    [Fact(DisplayName = "HasChanges should return false when command Picture is null and persisted Picture is an empty string")]
    public void HasChanges_ShouldReturnFalse_WhenPictureIsNullAgainstPersistedEmptyString()
    {
        // Arrange
        UpdatePlaylistRequest command = BaseCommand();
        command.Picture = null!;
        PlaylistHeaderDto persisted = MatchingPersisted();
        persisted.Picture = string.Empty;

        // Act
        bool result = PlaylistHeaderChangeDetector.HasChanges(command, persisted);

        // Assert
        Assert.False(result);
    }

    [Fact(DisplayName = "HasChanges should return false when Groups have identical content but different instances")]
    public void HasChanges_ShouldReturnFalse_WhenGroupsAreEqualByValue_ButDifferentInstances()
    {
        // Arrange
        UpdatePlaylistRequest command = BaseCommand();
        command.Groups = new List<PlaylistGroupDto> { new() { Name = "g1" } };
        PlaylistHeaderDto persisted = MatchingPersisted();

        // Act
        bool result = PlaylistHeaderChangeDetector.HasChanges(command, persisted);

        // Assert
        Assert.False(result);
    }

    [Fact(DisplayName = "HasChanges should return true when Groups content differs")]
    public void HasChanges_ShouldReturnTrue_WhenGroupsContentDiffers()
    {
        // Arrange
        UpdatePlaylistRequest command = BaseCommand();
        command.Groups = new List<PlaylistGroupDto> { new() { Name = "different" } };
        PlaylistHeaderDto persisted = MatchingPersisted();

        // Act
        bool result = PlaylistHeaderChangeDetector.HasChanges(command, persisted);

        // Assert
        Assert.True(result);
    }
}