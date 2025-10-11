using Rok.Application.Dto;
using Rok.Application.Randomizer;

namespace Rok.ApplicationTests;

public class TracksRandomizerTests
{
    [Fact]
    public void Randomize_NullInput_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => TracksRandomizer.Randomize(null!));
    }

    [Fact]
    public void Randomize_Empty_ReturnsEmptyList()
    {
        // Arrange
        IEnumerable<TrackDto> source = Array.Empty<TrackDto>();

        // Act
        List<TrackDto> result = TracksRandomizer.Randomize(source);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
        Assert.IsType<List<TrackDto>>(result);
    }

    [Fact]
    public void Randomize_SingleElement_ReturnsSameElementDifferentListInstance()
    {
        // Arrange
        TrackDto track = MakeTrack(1);
        List<TrackDto> source = new()
        { track };

        // Act
        List<TrackDto> result = TracksRandomizer.Randomize(source);

        // Assert
        Assert.NotSame(source, result);
        Assert.Single(result);
        Assert.Same(track, result[0]);

        Assert.Single(source);
        Assert.Same(track, source[0]);
    }

    [Fact]
    public void Randomize_ListInput_ReturnsCopy_DoesNotMutateSource()
    {
        // Arrange
        List<TrackDto> source = MakeTracks(10);
        List<TrackDto> sourceSnapshot = source.ToList();

        // Act
        List<TrackDto> result = TracksRandomizer.Randomize(source);

        // Assert
        Assert.NotSame(source, result);
        Assert.Equal(source.Count, result.Count);

        Assert.True(source.All(t => result.Contains(t)));
        Assert.True(result.All(t => source.Contains(t)));

        Assert.Equal(sourceSnapshot, source);
    }

    [Fact]
    public void Randomize_NonListICollectionInput_WorksAndIsPermutation()
    {
        // Arrange
        Queue<TrackDto> queue = new(MakeTracks(8));
        TrackDto[] originalOrder = queue.ToArray();

        // Act
        List<TrackDto> result = TracksRandomizer.Randomize(queue);

        // Assert
        Assert.Equal(queue.Count, result.Count);
        Assert.True(originalOrder.All(t => result.Contains(t)));

        Assert.Equal(originalOrder, queue.ToArray());
    }

    [Fact]
    public void Randomize_PermutationIntegrity_NoDuplicates_NoLoss()
    {
        // Arrange
        List<TrackDto> source = MakeTracks(25);

        // Act
        List<TrackDto> result = TracksRandomizer.Randomize(source);

        // Assert
        Assert.Equal(source.Count, result.Count);

        Assert.Equal(source.OrderBy(t => t.Title).Select(t => t),
                     result.OrderBy(t => t.Title).Select(t => t));

        Assert.Equal(result.Count, result.Distinct().Count());
    }

    [Fact]
    public void Randomize_RepeatedCalls_EventuallyProduceDifferentOrder()
    {
        // Arrange
        List<TrackDto> source = MakeTracks(6);

        // Act
        List<TrackDto> baseline = TracksRandomizer.Randomize(source).Select(t => t).ToList();

        // Assert
        bool differentFound = false;
        const int attempts = 60;
        for (int i = 0; i < attempts && !differentFound; i++)
        {
            List<TrackDto> next = TracksRandomizer.Randomize(source);
            if (!baseline.SequenceEqual(next))
                differentFound = true;
        }

        Assert.True(differentFound, "Aucun ordre différent trouvé après plusieurs randomisations (improbable).");
    }

    [Fact]
    public void Randomize_ModifyingResult_DoesNotAffectSource()
    {
        // Arrange
        List<TrackDto> source = MakeTracks(5);
        List<TrackDto> snapshot = source.ToList();

        // Act
        List<TrackDto> result = TracksRandomizer.Randomize(source);

        result.RemoveAt(0);
        result.Add(MakeTrack(999));

        // Assert        
        Assert.Equal(snapshot, source);
    }


    private static List<TrackDto> MakeTracks(int count)
    {
        List<TrackDto> list = new(capacity: count);

        for (int i = 1; i <= count; i++)
            list.Add(MakeTrack(i));

        return list;
    }

    private static TrackDto MakeTrack(long id) =>
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
            FileDate = DateTime.UtcNow
        };
}
