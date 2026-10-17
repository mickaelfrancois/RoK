using Rok.Application.Features.Albums.Merge;

namespace Rok.ApplicationTests.Features.Albums.Merge;

public class AlbumDiscConflictsTests
{
    private static TrackEntity Track(string title, int? disc, int? number)
    {
        return new TrackEntity { Title = title, Disc = disc, TrackNumber = number };
    }

    [Fact(DisplayName = "disc_conflicts_are_found_and_the_offset_is_the_last_target_disc")]
    public void Find_NullDiscCountsAsOne_AndOffsetIsLastTargetDisc()
    {
        // Arrange
        List<TrackEntity> target = [Track("T1", null, 1), Track("T2", 2, 5)];
        List<TrackEntity> source = [Track("S1", 1, 1), Track("S2", 2, 6)];

        // Act
        AlbumDiscConflictsResult result = AlbumDiscConflicts.Find(target, source);

        // Assert
        DiscConflict conflict = Assert.Single(result.Conflicts);
        Assert.Equal(new DiscConflict(1, 1, "S1", "T1"), conflict);
        Assert.Equal(2, result.Offset);
    }

    [Fact(DisplayName = "disc_conflicts_ignore_tracks_without_number")]
    public void Find_NullTrackNumbers_NeverCollide()
    {
        // Arrange
        List<TrackEntity> target = [Track("T1", 1, null)];
        List<TrackEntity> source = [Track("S1", 1, null)];

        // Act
        AlbumDiscConflictsResult result = AlbumDiscConflicts.Find(target, source);

        // Assert
        Assert.Empty(result.Conflicts);
        Assert.Equal(1, result.Offset);
    }

    [Fact(DisplayName = "disc_conflicts_are_empty_when_discs_differ")]
    public void Find_DifferentDiscs_NoConflict()
    {
        // Arrange
        List<TrackEntity> target = [Track("T1", 1, 1)];
        List<TrackEntity> source = [Track("S1", 2, 1)];

        // Act
        AlbumDiscConflictsResult result = AlbumDiscConflicts.Find(target, source);

        // Assert
        Assert.Empty(result.Conflicts);
    }

    [Fact(DisplayName = "disc_conflicts_offset_is_zero_for_an_empty_target")]
    public void Find_EmptyTarget_OffsetIsZero()
    {
        // Arrange
        List<TrackEntity> source = [Track("S1", 1, 1)];

        // Act
        AlbumDiscConflictsResult result = AlbumDiscConflicts.Find([], source);

        // Assert
        Assert.Empty(result.Conflicts);
        Assert.Equal(0, result.Offset);
    }
}