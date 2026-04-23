using Microsoft.Extensions.Logging.Abstractions;
using Rok.Application.Tag;
using Rok.Import.Services;

namespace Rok.ImportTests;

public class TrackFileProcessorTests
{
    [Fact]
    public void DetectCompilations_Should_FlagTrackAsCompilation_When_MultipleArtists()
    {
        // Arrange
        var sut = new TrackFileProcessor(null!, NullLogger<TrackFileProcessor>.Instance);
        List<TrackFile> tracks = new()
        {
             new TrackFile() { Artist = "Artist 1", Album = "Album A" },
             new TrackFile() { Artist = "Artist 2", Album = "Album A" },
             new TrackFile() { Artist = "Artist 1", Album = "Album A" },
        };

        // Act
        sut.DetectCompilations(tracks);

        // Assert
        Assert.All(tracks, c => Assert.True(c.IsCompilation));
    }

    [Fact]
    public void DetectCompilations_Should_FlagTrackAsNotCompilation_When_SameArtist()
    {
        // Arrange
        var sut = new TrackFileProcessor(null!, NullLogger<TrackFileProcessor>.Instance);
        List<TrackFile> tracks = new()
        {
             new TrackFile() { Artist = "Artist 1", Album = "Album A" },
             new TrackFile() { Artist = "Artist 1", Album = "Album B" },
        };

        // Act
        sut.DetectCompilations(tracks);

        // Assert
        Assert.All(tracks, c => Assert.False(c.IsCompilation));
    }

    [Fact]
    public void DetectCompilations_Should_FlagTrackAsNotCompilation_When_DifferentArtists_And_DifferentAlbums()
    {
        // Arrange
        var sut = new TrackFileProcessor(null!, NullLogger<TrackFileProcessor>.Instance);
        List<TrackFile> tracks = new()
        {
             new TrackFile() { Artist = "Artist 1", Album = "Album A" },
             new TrackFile() { Artist = "Artist 2", Album = "Album B" },
        };

        // Act
        sut.DetectCompilations(tracks);

        // Assert
        Assert.All(tracks, c => Assert.False(c.IsCompilation));
    }
}
