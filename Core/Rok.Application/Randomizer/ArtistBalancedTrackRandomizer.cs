namespace Rok.Application.Randomizer;

public static class ArtistBalancedTrackRandomizer
{
    public static List<TrackDto> Randomize(IEnumerable<TrackDto> input)
    {
        ArgumentNullException.ThrowIfNull(input);

        List<TrackDto> output = input switch
        {
            List<TrackDto> list => new List<TrackDto>(list),
            ICollection<TrackDto> coll => new List<TrackDto>(coll),
            _ => input.ToList()
        };

        if (output.Count <= 1)
            return new List<TrackDto>(output);

        Random rand = Random.Shared;

        List<(string Artist, Queue<TrackDto> Queue)> artistQueues = output
            .GroupBy(t => t.ArtistName ?? string.Empty)
            .Select(g =>
            {
                List<TrackDto> shuffled = g.ToList();
                FisherYatesShuffle(shuffled, rand);
                return (Artist: g.Key, Queue: new Queue<TrackDto>(shuffled));
            })
            .ToList();

        // PriorityQueue where priority is negative remaining count to simulate max-heap
        PriorityQueue<(string Artist, Queue<TrackDto> Queue), int> pq = new();
        foreach ((string Artist, Queue<TrackDto> Queue) pair in artistQueues)
        {
            if (pair.Queue.Count > 0)
                pq.Enqueue(pair, -pair.Queue.Count);
        }

        List<TrackDto> result = new(output.Count);
        string? lastArtist = null;

        while (pq.Count > 0)
        {
            (string Artist, Queue<TrackDto> Queue) first = pq.Dequeue();

            if (first.Artist == lastArtist && pq.Count > 0)
            {
                (string Artist, Queue<TrackDto> Queue) second = pq.Dequeue();

                TrackDto track = second.Queue.Dequeue();
                result.Add(track);
                lastArtist = second.Artist;

                if (second.Queue.Count > 0)
                    pq.Enqueue(second, -second.Queue.Count);

                if (first.Queue.Count > 0)
                    pq.Enqueue(first, -first.Queue.Count);
            }
            else
            {
                TrackDto track = first.Queue.Dequeue();
                result.Add(track);
                lastArtist = first.Artist;

                if (first.Queue.Count > 0)
                    pq.Enqueue(first, -first.Queue.Count);
            }
        }

        return result;
    }


    private static void FisherYatesShuffle<T>(List<T> list, Random rnd)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rnd.Next(i + 1);
            T tmp = list[i];
            list[i] = list[j];
            list[j] = tmp;
        }
    }
}
