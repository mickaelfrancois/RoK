using Rok.Application.Dto;
using Rok.Application.Randomizer;

namespace Rok.ApplicationTests;

public class PlaylistPlaybackOrderBuilderTests
{
    [Fact(DisplayName = "when_clicked_track_provided_it_starts_first")]
    public void BuildShuffledPlayOrder_ShouldPlaceClickedTrackFirst_WhenTrackIsClicked()
    {
        // Arrange
        List<TrackDto> tracks = new()
        {
            new() { Id = 1, ArtistName = "Artist A" },
            new() { Id = 2, ArtistName = "Artist B" },
            new() { Id = 3, ArtistName = "Artist C" },
            new() { Id = 4, ArtistName = "Artist D" },
            new() { Id = 5, ArtistName = "Artist E" }
        };
        TrackDto clicked = tracks[3];

        // Act
        List<TrackDto> ordered = PlaylistPlaybackOrderBuilder.BuildShuffledPlayOrder(tracks, clicked, new Random(42));

        // Assert
        Assert.Equal(4, ordered[0].Id);
    }

    [Fact(DisplayName = "when_clicked_track_already_first_it_stays_first")]
    public void BuildShuffledPlayOrder_ShouldKeepClickedTrackFirst_WhenAlreadyFirst()
    {
        // Arrange
        List<TrackDto> tracks = new()
        {
            new() { Id = 1, ArtistName = "Artist A" },
            new() { Id = 2, ArtistName = "Artist B" },
            new() { Id = 3, ArtistName = "Artist C" }
        };
        TrackDto clicked = tracks[0];

        // Act
        List<TrackDto> ordered = PlaylistPlaybackOrderBuilder.BuildShuffledPlayOrder(tracks, clicked, new Random(42));

        // Assert
        Assert.Equal(1, ordered[0].Id);
    }

    [Fact(DisplayName = "when_clicked_track_not_found_it_does_not_crash")]
    public void BuildShuffledPlayOrder_ShouldNotCrash_WhenClickedTrackNotFound()
    {
        // Arrange
        List<TrackDto> tracks = new()
        {
            new() { Id = 1, ArtistName = "Artist A" },
            new() { Id = 2, ArtistName = "Artist B" },
            new() { Id = 3, ArtistName = "Artist C" }
        };
        TrackDto notInList = new() { Id = 99, ArtistName = "Artist Z" };

        // Act
        List<TrackDto> ordered = PlaylistPlaybackOrderBuilder.BuildShuffledPlayOrder(tracks, notInList, new Random(42));

        // Assert
        Assert.Equal(3, ordered.Count);
        Assert.Equal(new List<long> { 1, 2, 3 }, ordered.Select(t => t.Id).OrderBy(id => id).ToList());
    }

    [Fact(DisplayName = "when_no_track_clicked_first_track_varies_across_seeds")]
    public void BuildShuffledPlayOrder_ShouldVaryFirstTrack_WhenNoTrackClickedAcrossSeeds()
    {
        // Arrange
        List<TrackDto> tracks = new()
        {
            new() { Id = 1, ArtistName = "Artist A" },
            new() { Id = 2, ArtistName = "Artist B" },
            new() { Id = 3, ArtistName = "Artist C" },
            new() { Id = 4, ArtistName = "Artist D" },
            new() { Id = 5, ArtistName = "Artist E" }
        };

        // Act
        HashSet<long> firstTrackIds = new();
        for (int seed = 0; seed < 20; seed++)
        {
            List<TrackDto> ordered = PlaylistPlaybackOrderBuilder.BuildShuffledPlayOrder(tracks, null, new Random(seed));
            firstTrackIds.Add(ordered[0].Id);
        }

        // Assert
        Assert.True(firstTrackIds.Count > 1);
    }

    [Fact(DisplayName = "when_no_track_clicked_multiset_is_preserved")]
    public void BuildShuffledPlayOrder_ShouldPreserveMultiset_WhenNoTrackClicked()
    {
        // Arrange
        List<TrackDto> tracks = new()
        {
            new() { Id = 1, ArtistName = "Artist A" },
            new() { Id = 2, ArtistName = "Artist B" },
            new() { Id = 3, ArtistName = "Artist C" },
            new() { Id = 4, ArtistName = "Artist D" },
            new() { Id = 5, ArtistName = "Artist E" }
        };

        // Act
        List<TrackDto> ordered = PlaylistPlaybackOrderBuilder.BuildShuffledPlayOrder(tracks, null, new Random(42));

        // Assert
        Assert.Equal(5, ordered.Count);
        Assert.Equal(ordered.Select(t => t.Id).Distinct().Count(), ordered.Count);
        Assert.Equal(new List<long> { 1, 2, 3, 4, 5 }, ordered.Select(t => t.Id).OrderBy(id => id).ToList());
    }

    [Fact(DisplayName = "when_tracks_have_one_or_zero_elements_it_is_a_no_op")]
    public void BuildShuffledPlayOrder_ShouldNoOp_WhenTracksHaveOneOrZeroElements()
    {
        // Arrange
        List<TrackDto> emptyTracks = new();
        List<TrackDto> singleTrack = new()
        {
            new() { Id = 1, ArtistName = "Artist A" }
        };

        // Act
        List<TrackDto> orderedEmpty = PlaylistPlaybackOrderBuilder.BuildShuffledPlayOrder(emptyTracks, null, new Random(42));
        List<TrackDto> orderedSingle = PlaylistPlaybackOrderBuilder.BuildShuffledPlayOrder(singleTrack, null, new Random(42));

        // Assert
        Assert.Empty(orderedEmpty);
        Assert.Single(orderedSingle);
        Assert.Equal(1, orderedSingle[0].Id);
    }
}