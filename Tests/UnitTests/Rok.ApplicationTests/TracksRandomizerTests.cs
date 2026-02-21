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