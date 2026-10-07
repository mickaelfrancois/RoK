using Rok.Application.Player;

namespace Rok.ApplicationTests.Player;

public class OriginalQueueOrderTests
{
    private static TrackDto BuildTrack(long id) => new() { Id = id, Title = $"t{id}", ArtistName = "artist" };

    private static List<long> Ids(IEnumerable<TrackDto> tracks) => [.. tracks.Select(t => t.Id)];

    [Fact(DisplayName = "when_tracks_were_removed_restore_drops_them_and_keeps_the_original_order")]
    public void Restore_drops_removed_tracks_and_keeps_original_order()
    {
        // Arrange
        TrackDto[] original = [BuildTrack(1), BuildTrack(2), BuildTrack(3), BuildTrack(4), BuildTrack(5)];
        OriginalQueueOrder sut = new();
        sut.Capture(original);
        List<TrackDto> playOrder = [original[3], original[0], original[4], original[1]];

        // Act
        (List<TrackDto> order, int currentIndex) = sut.Restore(playOrder, original[4]);

        // Assert
        Assert.Equal([1L, 2L, 4L, 5L], Ids(order));
        Assert.Equal(3, currentIndex);
    }

    [Fact(DisplayName = "when_a_reference_appears_twice_restore_respects_the_multiset_and_the_first_occurrence")]
    public void Restore_respects_duplicated_references()
    {
        // Arrange
        TrackDto a = BuildTrack(1);
        TrackDto b = BuildTrack(2);
        OriginalQueueOrder sut = new();
        sut.Capture([a, b, a]);

        // Act
        (List<TrackDto> order, int currentIndex) = sut.Restore([a, a, b], a);

        // Assert
        Assert.Equal([1L, 2L, 1L], Ids(order));
        Assert.Equal(0, currentIndex);
    }

    [Fact(DisplayName = "when_the_queue_holds_unknown_tracks_restore_appends_them_at_the_end")]
    public void Restore_appends_unknown_tracks()
    {
        // Arrange
        TrackDto a = BuildTrack(1);
        TrackDto b = BuildTrack(2);
        TrackDto stranger = BuildTrack(9);
        OriginalQueueOrder sut = new();
        sut.Capture([a, b]);

        // Act
        (List<TrackDto> order, _) = sut.Restore([stranger, b, a], a);

        // Assert
        Assert.Equal([1L, 2L, 9L], Ids(order));
    }

    [Fact(DisplayName = "when_appending_tracks_they_go_at_the_end_of_the_original_order")]
    public void Append_adds_tracks_at_the_end()
    {
        // Arrange
        TrackDto a = BuildTrack(1);
        TrackDto b = BuildTrack(2);
        TrackDto c = BuildTrack(3);
        OriginalQueueOrder sut = new();
        sut.Capture([a, b]);
        sut.Append([c]);

        // Act
        (List<TrackDto> order, _) = sut.Restore([c, b, a], a);

        // Assert
        Assert.Equal([1L, 2L, 3L], Ids(order));
    }

    [Fact(DisplayName = "when_inserting_after_an_anchor_tracks_land_right_after_it")]
    public void InsertAfter_places_tracks_after_the_anchor()
    {
        // Arrange
        TrackDto a = BuildTrack(1);
        TrackDto b = BuildTrack(2);
        TrackDto c = BuildTrack(3);
        TrackDto inserted = BuildTrack(7);
        OriginalQueueOrder sut = new();
        sut.Capture([a, b, c]);
        sut.InsertAfter(a, [inserted]);

        // Act
        (List<TrackDto> order, _) = sut.Restore([c, inserted, b, a], a);

        // Assert
        Assert.Equal([1L, 7L, 2L, 3L], Ids(order));
    }

    [Fact(DisplayName = "when_inserting_after_a_null_anchor_tracks_land_at_the_head")]
    public void InsertAfter_with_null_anchor_inserts_at_the_head()
    {
        // Arrange
        TrackDto a = BuildTrack(1);
        TrackDto b = BuildTrack(2);
        TrackDto inserted = BuildTrack(7);
        OriginalQueueOrder sut = new();
        sut.Capture([a, b]);
        sut.InsertAfter(null, [inserted]);

        // Act
        (List<TrackDto> order, _) = sut.Restore([b, inserted, a], a);

        // Assert
        Assert.Equal([7L, 1L, 2L], Ids(order));
    }
}