namespace Rok.Application.Randomizer;

public static class TracksRandomizer
{
    public static List<TrackDto> Randomize(IEnumerable<TrackDto> input)
    {
        ArgumentNullException.ThrowIfNull(input);

        List<TrackDto> output = input switch
        {
            List<TrackDto> list => new List<TrackDto>(list), // Copy to not mutate the source
            ICollection<TrackDto> coll => new List<TrackDto>(coll),
            _ => [.. input]
        };

        Random random = Random.Shared;

        for (int i = output.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            if (j != i)
            {
                (output[i], output[j]) = (output[j], output[i]);
            }
        }

        return output;
    }
}
