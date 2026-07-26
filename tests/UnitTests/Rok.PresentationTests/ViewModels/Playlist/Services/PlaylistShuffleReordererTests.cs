using Rok.Application.Dto;
using Rok.ViewModels.Playlist.Services;

namespace Rok.PresentationTests.ViewModels.Playlist.Services;

public class PlaylistShuffleReordererTests
{
    private static TrackDto Track(long id, string artist) =>
        new() { Id = id, ArtistName = artist, Title = $"Track {id}" };

    [Fact(DisplayName = "Reorder preserves the item count and the exact multiset of track ids")]
    public void Reorder_ShouldPreserveMultiset()
    {
        // Arrange
        List<TrackDto> items =
        [
            Track(1, "A"), Track(2, "B"), Track(3, "A"), Track(4, "C"), Track(5, "B"),
        ];

        // Act
        List<TrackDto> result = PlaylistShuffleReorderer.Reorder(items, dto => dto, new Random(1));

        // Assert
        Assert.Equal(items.Count, result.Count);
        Assert.Equal(items.Select(t => t.Id).OrderBy(id => id), result.Select(t => t.Id).OrderBy(id => id));
    }

    [Fact(DisplayName = "Reorder keeps the head track in place")]
    public void Reorder_ShouldKeepHeadTrack()
    {
        // Arrange
        List<TrackDto> items =
        [
            Track(1, "A"), Track(2, "B"), Track(3, "C"), Track(4, "A"), Track(5, "B"), Track(6, "C"),
        ];

        // Act
        List<TrackDto> result = PlaylistShuffleReorderer.Reorder(items, dto => dto, new Random(1));

        // Assert
        Assert.Equal(items[0].Id, result[0].Id);
    }

    [Fact(DisplayName = "Reorder keeps both tracks when two share the same id")]
    public void Reorder_ShouldPreserveDuplicateIds()
    {
        // Arrange
        List<TrackDto> items =
        [
            Track(1, "A"), Track(7, "B"), Track(7, "C"), Track(4, "A"),
        ];

        // Act
        List<TrackDto> result = PlaylistShuffleReorderer.Reorder(items, dto => dto, new Random(1));

        // Assert
        Assert.Equal(4, result.Count);
        Assert.Equal(2, result.Count(t => t.Id == 7));
    }

    [Fact(DisplayName = "Reorder returns the single item unchanged for a one-element list")]
    public void Reorder_ShouldReturnSingleItemUnchanged()
    {
        // Arrange
        List<TrackDto> items = [Track(1, "A")];

        // Act
        List<TrackDto> result = PlaylistShuffleReorderer.Reorder(items, dto => dto);

        // Assert
        Assert.Single(result);
        Assert.Equal(1, result[0].Id);
    }

    [Fact(DisplayName = "Reorder returns an empty list for an empty input without throwing")]
    public void Reorder_ShouldReturnEmptyForEmptyInput()
    {
        // Arrange
        List<TrackDto> items = [];

        // Act
        List<TrackDto> result = PlaylistShuffleReorderer.Reorder(items, dto => dto);

        // Assert
        Assert.Empty(result);
    }

    [Fact(DisplayName = "Reorder eventually reorders the tail while keeping the head fixed")]
    public void Reorder_ShouldVaryTailOrderWhileKeepingHead()
    {
        // Arrange
        List<TrackDto> items =
        [
            Track(1, "A"), Track(2, "B"), Track(3, "C"), Track(4, "A"),
            Track(5, "B"), Track(6, "C"), Track(7, "A"), Track(8, "B"),
        ];
        List<long> originalTail = items.Skip(1).Select(t => t.Id).ToList();

        // Act
        bool tailReordered = false;

        for (int attempt = 0; attempt < 50 && !tailReordered; attempt++)
        {
            List<TrackDto> result = PlaylistShuffleReorderer.Reorder(items, dto => dto);

            Assert.Equal(items[0].Id, result[0].Id);
            tailReordered = !result.Skip(1).Select(t => t.Id).SequenceEqual(originalTail);
        }

        // Assert
        Assert.True(tailReordered, "Expected the tail to be reordered in at least one run.");
    }
}