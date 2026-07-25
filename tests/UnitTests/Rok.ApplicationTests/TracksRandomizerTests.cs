using Rok.Application.Dto;
using Rok.Application.Randomizer;

namespace Rok.ApplicationTests;

public class TracksRandomizerTests
{
    [Fact]
    public void ArtistBalancedTrackRandomize_ShouldNotAlterOrderBeforeIndex()
    {
        // Arrange
        List<TrackDto> playlist = new()
        {
            new() { Id = 1, ArtistName = "Artist A" },
            new() { Id = 2, ArtistName = "Artist B" },
            new() { Id = 3, ArtistName = "Artist C" },
            new() { Id = 4, ArtistName = "Artist A" },
            new() { Id = 5, ArtistName = "Artist B" },
            new() { Id = 6, ArtistName = "Artist C" }
        };
        int shuffleStartIndex = 2;

        // Act
        TracksRandomizer.ArtistBalancedTrackRandomize(playlist, shuffleStartIndex, new Random(42));

        // Assert
        Assert.Equal(1, playlist[0].Id);
        Assert.Equal(2, playlist[1].Id);
        Assert.Equal(3, playlist[2].Id);
    }

    [Fact]
    public void ArtistBalancedTrackRandomize_ShouldShuffleTracksAfterIndex()
    {
        // Arrange
        List<TrackDto> playlist = new()
        {
            new() { Id = 1, ArtistName = "Artist A" },
            new() { Id = 2, ArtistName = "Artist B" },
            new() { Id = 3, ArtistName = "Artist C" },
            new() { Id = 4, ArtistName = "Artist A" },
            new() { Id = 5, ArtistName = "Artist B" },
            new() { Id = 6, ArtistName = "Artist C" }
        };
        int shuffleStartIndex = 2;

        // Act
        TracksRandomizer.ArtistBalancedTrackRandomize(playlist, shuffleStartIndex, new Random(42));

        // Assert
        List<long> shuffledIds = playlist.Skip(shuffleStartIndex + 1).Select(track => track.Id).ToList();
        Assert.NotEqual(new List<long> { 4, 5, 6 }, shuffledIds);
        Assert.Equal(3, shuffledIds.Count);
    }

    [Fact]
    public void ArtistBalancedTrackRandomize_ShouldArtistBalance()
    {
        // Arrange
        List<TrackDto> playlist = new()
        {
            new() { Id = 1, ArtistName = "Artist A" },
            new() { Id = 2, ArtistName = "Artist A" },
            new() { Id = 3, ArtistName = "Artist B" },
            new() { Id = 4, ArtistName = "Artist B" },
            new() { Id = 5, ArtistName = "Artist C" },
            new() { Id = 6, ArtistName = "Artist C" }
        };
        int shuffleStartIndex = 0;

        // Act
        TracksRandomizer.ArtistBalancedTrackRandomize(playlist, shuffleStartIndex, new Random(42));

        // Assert
        for (int i = 1; i < playlist.Count; i++)
        {
            Assert.NotEqual(playlist[i - 1].ArtistName, playlist[i].ArtistName);
        }
    }

    [Fact]
    public void ArtistBalancedTrackRandomize_ShouldRandomizeTrack_WhenOnlyOneArtist()
    {
        // Arrange
        List<TrackDto> playlist = new()
        {
            new() { Id = 1, ArtistName = "Artist A" },
            new() { Id = 2, ArtistName = "Artist A" },
            new() { Id = 3, ArtistName = "Artist A" },
            new() { Id = 4, ArtistName = "Artist A" },
            new() { Id = 5, ArtistName = "Artist A" },
        };
        int shuffleStartIndex = 0;

        // Act
        TracksRandomizer.ArtistBalancedTrackRandomize(playlist, shuffleStartIndex, new Random(42));

        // Assert
        List<long> shuffledIds = playlist.Select(track => track.Id).ToList();
        Assert.NotEqual(new List<long> { 1, 2, 3, 4, 5 }, shuffledIds);
    }

    [Fact]
    public void ArtistBalancedTrackRandomize_ShouldHandleEmptyPlaylist()
    {
        // Arrange
        List<TrackDto> playlist = new();
        int shuffleStartIndex = 0;

        // Act
        TracksRandomizer.ArtistBalancedTrackRandomize(playlist, shuffleStartIndex, new Random(42));

        // Assert
        Assert.Empty(playlist);
    }

    [Fact]
    public void ArtistBalancedTrackRandomize_ShouldHandleSingleTrack()
    {
        // Arrange
        List<TrackDto> playlist = new()
        {
            new() { Id = 1, ArtistName = "Artist A" }
        };
        int shuffleStartIndex = 0;

        // Act
        TracksRandomizer.ArtistBalancedTrackRandomize(playlist, shuffleStartIndex, new Random(42));

        // Assert
        Assert.Single(playlist);
        Assert.Equal(1, playlist[0].Id);
    }

    [Fact(DisplayName = "when_shuffle_start_index_is_zero_first_track_is_preserved")]
    public void ArtistBalancedTrackRandomize_ShouldPreserveFirstTrack_WhenShuffleStartIndexIsZero()
    {
        // Arrange
        List<TrackDto> playlist = new()
        {
            new() { Id = 1, ArtistName = "Artist A" },
            new() { Id = 2, ArtistName = "Artist B" },
            new() { Id = 3, ArtistName = "Artist C" },
            new() { Id = 4, ArtistName = "Artist D" },
            new() { Id = 5, ArtistName = "Artist E" }
        };
        int shuffleStartIndex = 0;

        // Act
        TracksRandomizer.ArtistBalancedTrackRandomize(playlist, shuffleStartIndex, new Random(42));

        // Assert
        Assert.Equal(1, playlist[0].Id);
        List<long> shuffledIds = playlist.Skip(1).Select(track => track.Id).ToList();
        Assert.NotEqual(new List<long> { 2, 3, 4, 5 }, shuffledIds);
    }

    [Fact(DisplayName = "when_shuffle_start_index_is_zero_multiset_is_preserved")]
    public void ArtistBalancedTrackRandomize_ShouldPreserveMultiset_WhenShuffleStartIndexIsZero()
    {
        // Arrange
        List<TrackDto> playlist = new()
        {
            new() { Id = 1, ArtistName = "Artist A" },
            new() { Id = 2, ArtistName = "Artist B" },
            new() { Id = 3, ArtistName = "Artist C" },
            new() { Id = 4, ArtistName = "Artist D" },
            new() { Id = 5, ArtistName = "Artist E" }
        };
        int shuffleStartIndex = 0;
        List<long> originalIds = playlist.Select(track => track.Id).ToList();

        // Act
        TracksRandomizer.ArtistBalancedTrackRandomize(playlist, shuffleStartIndex, new Random(42));

        // Assert
        List<long> resultingIds = playlist.Select(track => track.Id).ToList();
        Assert.Equal(originalIds.Count, resultingIds.Count);
        Assert.Equal(originalIds.OrderBy(id => id), resultingIds.OrderBy(id => id));
        Assert.Equal(resultingIds.Distinct().Count(), resultingIds.Count);
    }

    [Fact(DisplayName = "when_playlist_has_one_or_zero_tracks_it_is_a_no_op")]
    public void ArtistBalancedTrackRandomize_ShouldNoOp_WhenPlaylistHasOneOrZeroTracks()
    {
        // Arrange
        List<TrackDto> emptyPlaylist = new();
        List<TrackDto> singleTrackPlaylist = new()
        {
            new() { Id = 1, ArtistName = "Artist A" }
        };

        // Act
        TracksRandomizer.ArtistBalancedTrackRandomize(emptyPlaylist, -1, new Random(42));
        TracksRandomizer.ArtistBalancedTrackRandomize(singleTrackPlaylist, -1, new Random(42));

        // Assert
        Assert.Empty(emptyPlaylist);
        Assert.Single(singleTrackPlaylist);
        Assert.Equal(1, singleTrackPlaylist[0].Id);
    }

    [Fact]
    public void Randomize_ShouldShuffleTracks()
    {
        // Arrange
        List<TrackDto> tracks = new()
            {
                new() { Id = 1, ArtistName = "Artist A" },
                new() { Id = 2, ArtistName = "Artist B" },
                new() { Id = 3, ArtistName = "Artist C" },
                new() { Id = 4, ArtistName = "Artist D" },
                new() { Id = 5, ArtistName = "Artist E" },
            };
        // Act
        TracksRandomizer.Randomize(tracks, new Random(42));
        // Assert
        List<long> shuffledIds = tracks.Select(track => track.Id).ToList();
        Assert.NotEqual(new List<long> { 1, 2, 3, 4, 5 }, shuffledIds);
    }
}