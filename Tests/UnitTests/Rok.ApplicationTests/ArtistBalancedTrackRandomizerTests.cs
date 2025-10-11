using Rok.Application.Dto;
using Rok.Application.Randomizer;

namespace Rok.ApplicationTests;

public class ArtistBalancedTrackRandomizerTests
{
    [Fact]
    public void Randomize_NullInput_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ArtistBalancedTrackRandomizer.Randomize(null!));
    }

    [Fact]
    public void Randomize_Empty_ReturnsEmptyList()
    {
        // Arrange
        IEnumerable<TrackDto> source = Array.Empty<TrackDto>();

        // Act
        List<TrackDto> result = ArtistBalancedTrackRandomizer.Randomize(source);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
        Assert.IsType<List<TrackDto>>(result);
    }

    [Fact]
    public void Randomize_SingleElement_ReturnsSameElementDifferentListInstance()
    {
        // Arrange
        TrackDto track = MakeTrack(1, "ArtistA");
        List<TrackDto> source = new() { track };

        // Act
        List<TrackDto> result = ArtistBalancedTrackRandomizer.Randomize(source);

        // Assert
        Assert.NotSame(source, result);
        Assert.Single(result);
        Assert.Same(track, result[0]);

        Assert.Single(source);
        Assert.Same(track, source[0]);
    }

    [Fact]
    public void Randomize_SourceList_NotMutated()
    {
        // Arrange
        List<TrackDto> source = new();
        for (int i = 1; i <= 10; i++)
            source.Add(MakeTrack(i, i % 2 == 0 ? "A" : "B"));
        List<TrackDto> snapshot = source.ToList();

        // Act
        _ = ArtistBalancedTrackRandomizer.Randomize(source);

        // Assert
        Assert.Equal(snapshot, source);
    }

    [Fact]
    public void Randomize_AllSameArtist_ReturnsPermutationOnlyThatArtist()
    {
        // Arrange
        List<TrackDto> source = Enumerable.Range(1, 8)
            .Select(i => MakeTrack(i, "Solo"))
            .ToList();

        // Act
        List<TrackDto> result = ArtistBalancedTrackRandomizer.Randomize(source);

        // Assert
        Assert.Equal(source.Count, result.Count);
        Assert.All(result, t => Assert.Equal("Solo", t.ArtistName));
        AssertPermutation(source, result);
    }

    [Fact]
    public void Randomize_BalancedArtists_NoConsecutiveDuplicates()
    {
        // Arrange
        List<TrackDto> source = new();
        long id = 1;
        foreach (string artist in new[] { "A", "B", "C" })
            for (int i = 0; i < 5; i++)
                source.Add(MakeTrack(id++, artist));

        // Act
        List<TrackDto> result = ArtistBalancedTrackRandomizer.Randomize(source);

        // Assert
        AssertPermutation(source, result);
        for (int i = 1; i < result.Count; i++)
            Assert.NotEqual(result[i - 1].ArtistName, result[i].ArtistName);
    }

    [Fact]
    public void Randomize_UnbalancedArtists_MinimizesConsecutiveDuplicates()
    {
        // Arrange
        List<TrackDto> source = new();
        long id = 1;
        // Dominant artist A (7 tracks), others B (3), C (2)
        for (int i = 0; i < 7; i++) source.Add(MakeTrack(id++, "A"));
        for (int i = 0; i < 3; i++) source.Add(MakeTrack(id++, "B"));
        for (int i = 0; i < 2; i++) source.Add(MakeTrack(id++, "C"));

        Dictionary<string, int> remaining = source
            .GroupBy(t => t.ArtistName!)
            .ToDictionary(g => g.Key, g => g.Count());

        // Act
        List<TrackDto> result = ArtistBalancedTrackRandomizer.Randomize(source);

        // Assert
        AssertPermutation(source, result);

        string? last = null;
        foreach (TrackDto track in result)
        {
            if (last == track.ArtistName)
            {
                // A duplicate only acceptable if no other artist had remaining tracks at that moment
                bool otherAvailable = remaining.Any(kvp => kvp.Key != track.ArtistName && kvp.Value > 0);
                Assert.False(otherAvailable, $"Found avoidable consecutive duplicate for artist '{track.ArtistName}'.");
            }
            remaining[track.ArtistName!]--;
            last = track.ArtistName;
        }

        Assert.All(remaining.Values, v => Assert.Equal(0, v));
    }

    [Fact]
    public void Randomize_PermutationIntegrity_NoDuplicates_NoLoss()
    {
        // Arrange
        List<TrackDto> source = new();
        long id = 1;
        foreach (string artist in new[] { "A", "B", "C", "D" })
            for (int i = 0; i < 4; i++)
                source.Add(MakeTrack(id++, artist));

        // Act
        List<TrackDto> result = ArtistBalancedTrackRandomizer.Randomize(source);

        // Assert
        AssertPermutation(source, result);
        Assert.Equal(result.Count, result.Distinct().Count());
    }

    [Fact]
    public void Randomize_NonListICollectionInput_WorksAndRespectsBalancing()
    {
        // Arrange
        Queue<TrackDto> queue = new(new[]
        {
            MakeTrack(1,"A"), MakeTrack(2,"A"),
            MakeTrack(3,"B"), MakeTrack(4,"B"),
            MakeTrack(5,"C"), MakeTrack(6,"C")
        });
        TrackDto[] snapshot = queue.ToArray();

        // Act
        List<TrackDto> result = ArtistBalancedTrackRandomizer.Randomize(queue);

        // Assert
        AssertPermutation(snapshot, result);
        // Check mostly no consecutive duplicate (should be possible to avoid all)
        for (int i = 1; i < result.Count; i++)
            Assert.NotEqual(result[i - 1].ArtistName, result[i].ArtistName);

        Assert.Equal(snapshot, queue.ToArray()); // source not mutated
    }

    [Fact]
    public void Randomize_RepeatedCalls_ProduceDifferentOrders()
    {
        // Arrange
        List<TrackDto> source = new();
        long id = 1;
        foreach (string artist in new[] { "A", "B", "C" })
            for (int i = 0; i < 3; i++)
                source.Add(MakeTrack(id++, artist));

        // Act
        List<TrackDto> baseline = ArtistBalancedTrackRandomizer.Randomize(source);
        bool different = false;
        const int attempts = 40;
        for (int i = 0; i < attempts && !different; i++)
        {
            List<TrackDto> next = ArtistBalancedTrackRandomizer.Randomize(source);
            if (!baseline.SequenceEqual(next))
                different = true;
        }

        // Assert
        Assert.True(different, "No different balanced ordering found after several attempts (unlikely).");
    }

    [Fact]
    public void Randomize_ModifyingResult_DoesNotAffectSource()
    {
        // Arrange
        List<TrackDto> source = new();
        long id = 1;
        for (int i = 0; i < 5; i++) source.Add(MakeTrack(id++, "A"));
        for (int i = 0; i < 5; i++) source.Add(MakeTrack(id++, "B"));
        List<TrackDto> snapshot = source.ToList();

        // Act
        List<TrackDto> result = ArtistBalancedTrackRandomizer.Randomize(source);
        result.RemoveAt(0);
        result.Add(MakeTrack(9999, "Z"));

        // Assert
        Assert.Equal(snapshot, source);
    }

    private static TrackDto MakeTrack(long id, string artist) =>
        new()
        {
            Title = $"Title {id}",
            MusicFile = $"file{id}.mp3",
            Duration = id * 1000,
            Bitrate = 320,
            Size = id * 10,
            Score = 0,
            ListenCount = 0,
            SkipCount = 0,
            Listening = false,
            FileDate = DateTime.UtcNow,
            ArtistName = artist
        };

    private static void AssertPermutation(IReadOnlyCollection<TrackDto> original, IReadOnlyCollection<TrackDto> candidate)
    {
        Assert.Equal(original.Count, candidate.Count);
        // Compare multiset by reference occurrences
        Assert.True(original.All(t => candidate.Contains(t)));
        Assert.True(candidate.All(t => original.Contains(t)));
    }
}