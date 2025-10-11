namespace Rok.Application.Randomizer;

public static class ArtistBalancedTrackRandomizer
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

        if (output.Count <= 1)
            return new List<TrackDto>(output);

        Random rand = Random.Shared;

        // Group and shuffle tracks per artist
        List<(string Artist, Queue<TrackDto> Queue)> artistQueues = output
            .GroupBy(t => t.ArtistName ?? string.Empty)
            .Select(g =>
            {
                // Shuffle using OrderBy with random key
                List<TrackDto> shuffled = g.OrderBy(_ => rand.Next()).ToList();
                return (Artist: g.Key, Queue: new Queue<TrackDto>(shuffled));
            })
            .ToList();

        // PriorityQueue: element = (Artist, Queue, RemainingCount)
        // .NET PriorityQueue is min-heap; use negative remaining count to simulate max-heap
        PriorityQueue<(string Artist, Queue<TrackDto> Queue), int> queue = new();
        foreach ((string Artist, Queue<TrackDto> Queue) in artistQueues)
            queue.Enqueue((Artist, Queue), -Queue.Count);

        List<TrackDto> result = new(output.Count);
        string? lastArtist = null;

        while (queue.Count > 0)
        {
            // Take top
            if (!queue.TryDequeue(out (string Artist, Queue<TrackDto> Queue) current, out int currentPriority))
                break;

            // If same artist as last and alternative exists, swap
            if (current.Artist == lastArtist && queue.Count > 0)
            {
                // Hold current, get next
                (string Artist, Queue<TrackDto> Queue) held = current;
                int heldPriority = currentPriority;

                queue.TryDequeue(out current, out currentPriority); // use second
                // Requeue held for later
                queue.Enqueue(held, heldPriority);
            }

            // Consume one track
            if (current.Queue.Count > 0)
            {
                TrackDto track = current.Queue.Dequeue();
                result.Add(track);
                lastArtist = current.Artist;

                if (current.Queue.Count > 0)
                {
                    // Re-enqueue with updated remaining (max-heap via negative)
                    queue.Enqueue((current.Artist, current.Queue), -current.Queue.Count);
                }
            }
        }

        return result;
    }
}
