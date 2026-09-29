using Rok.Application.Player;

namespace Rok.ApplicationTests.Player;

public class PlaybackTransitionPolicyTests
{
    private static TrackDto BuildTrack(long? albumId, int? trackNumber, bool isLive = false) => new() { Id = trackNumber ?? 0, AlbumId = albumId, TrackNumber = trackNumber, IsAlbumLive = isLive };

    [Theory(DisplayName = "decide_returns_gapless_when_crossfade_is_disabled")]
    [InlineData(1L, 1, 1L, 2)]
    [InlineData(1L, 1, 2L, 7)]
    [InlineData(null, null, null, null)]
    public void Decide_ReturnsGapless_WhenCrossfadeIsDisabled(long? currentAlbum, int? currentNumber, long? nextAlbum, int? nextNumber)
    {
        // Act
        EPlaybackTransition transition = PlaybackTransitionPolicy.Decide(crossfadeEnabled: false, isMuted: false, BuildTrack(currentAlbum, currentNumber), BuildTrack(nextAlbum, nextNumber));

        // Assert
        Assert.Equal(EPlaybackTransition.Gapless, transition);
    }

    [Theory(DisplayName = "decide_returns_gapless_for_the_following_track_of_the_same_album")]
    [InlineData(false)]
    [InlineData(true)]
    public void Decide_ReturnsGapless_ForFollowingTrackOfSameAlbum(bool isLive)
    {
        // Act
        EPlaybackTransition transition = PlaybackTransitionPolicy.Decide(crossfadeEnabled: true, isMuted: false, BuildTrack(5, 3, isLive), BuildTrack(5, 4, isLive));

        // Assert
        Assert.Equal(EPlaybackTransition.Gapless, transition);
    }

    [Theory(DisplayName = "decide_returns_crossfade_when_the_same_album_track_is_not_the_following_one")]
    [InlineData(3, 5)]
    [InlineData(3, 2)]
    [InlineData(3, 3)]
    public void Decide_ReturnsCrossfade_WhenSameAlbumTrackIsNotFollowingOne(int currentNumber, int nextNumber)
    {
        // Act
        EPlaybackTransition transition = PlaybackTransitionPolicy.Decide(crossfadeEnabled: true, isMuted: false, BuildTrack(5, currentNumber), BuildTrack(5, nextNumber));

        // Assert
        Assert.Equal(EPlaybackTransition.Crossfade, transition);
    }

    [Fact(DisplayName = "decide_returns_crossfade_when_the_album_changes")]
    public void Decide_ReturnsCrossfade_WhenAlbumChanges()
    {
        // Act
        EPlaybackTransition transition = PlaybackTransitionPolicy.Decide(crossfadeEnabled: true, isMuted: false, BuildTrack(5, 3), BuildTrack(6, 4));

        // Assert
        Assert.Equal(EPlaybackTransition.Crossfade, transition);
    }

    [Theory(DisplayName = "decide_returns_crossfade_when_album_or_track_number_is_missing")]
    [InlineData(null, 3, null, 4)]
    [InlineData(5L, null, 5L, 4)]
    [InlineData(5L, 3, 5L, null)]
    public void Decide_ReturnsCrossfade_WhenAlbumOrTrackNumberIsMissing(long? currentAlbum, int? currentNumber, long? nextAlbum, int? nextNumber)
    {
        // Act
        EPlaybackTransition transition = PlaybackTransitionPolicy.Decide(crossfadeEnabled: true, isMuted: false, BuildTrack(currentAlbum, currentNumber), BuildTrack(nextAlbum, nextNumber));

        // Assert
        Assert.Equal(EPlaybackTransition.Crossfade, transition);
    }

    [Fact(DisplayName = "decide_returns_gapless_when_muted")]
    public void Decide_ReturnsGapless_WhenMuted()
    {
        // Act
        EPlaybackTransition transition = PlaybackTransitionPolicy.Decide(crossfadeEnabled: true, isMuted: true, BuildTrack(5, 3), BuildTrack(6, 1));

        // Assert
        Assert.Equal(EPlaybackTransition.Gapless, transition);
    }

    [Fact(DisplayName = "decide_returns_crossfade_between_live_tracks_of_different_albums")]
    public void Decide_ReturnsCrossfade_BetweenLiveTracksOfDifferentAlbums()
    {
        // Act
        EPlaybackTransition transition = PlaybackTransitionPolicy.Decide(crossfadeEnabled: true, isMuted: false, BuildTrack(5, 3, isLive: true), BuildTrack(6, 4, isLive: true));

        // Assert
        Assert.Equal(EPlaybackTransition.Crossfade, transition);
    }
}