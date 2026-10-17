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

    [Fact(DisplayName = "detect_compilations_flags_the_whole_group_when_one_file_is_tagged")]
    public void DetectCompilations_Should_FlagGroupAsTaggedCompilation_When_SingleArtist_And_OneFileHasFlag()
    {
        // Arrange
        var sut = new TrackFileProcessor(null!, NullLogger<TrackFileProcessor>.Instance);
        List<TrackFile> tracks = new()
        {
            new TrackFile() { Artist = "Artist 1", Album = "Album A", HasCompilationFlag = true },
            new TrackFile() { Artist = "Artist 1", Album = "Album A" },
            new TrackFile() { Artist = "Artist 1", Album = "Album A" },
        };

        // Act
        sut.DetectCompilations(tracks);

        // Assert
        Assert.All(tracks, c =>
        {
            Assert.True(c.IsCompilation);
            Assert.True(c.IsTaggedCompilation);
            Assert.False(c.IsMultiArtist);
        });
    }

    [Fact(DisplayName = "detect_compilations_does_not_tag_a_group_without_flag")]
    public void DetectCompilations_Should_NotTagGroup_When_NoFileHasFlag()
    {
        // Arrange
        var sut = new TrackFileProcessor(null!, NullLogger<TrackFileProcessor>.Instance);
        List<TrackFile> tracks = new()
        {
            new TrackFile() { Artist = "Artist 1", Album = "Album A" },
            new TrackFile() { Artist = "Artist 2", Album = "Album A" },
            new TrackFile() { Artist = "Artist 1", Album = "Album B", HasCompilationFlag = true },
        };

        // Act
        sut.DetectCompilations(tracks);

        // Assert
        Assert.All(tracks.Take(2), c =>
        {
            Assert.True(c.IsCompilation);
            Assert.False(c.IsTaggedCompilation);
            Assert.True(c.IsMultiArtist);
        });
        Assert.True(tracks[2].IsTaggedCompilation);
    }
}