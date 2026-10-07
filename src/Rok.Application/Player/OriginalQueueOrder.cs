using System.Collections.Generic;

namespace Rok.Application.Player;

/// <summary>
/// Remembers the queue order that preceded a reversible shuffle. Works by reference identity (a track
/// can appear twice in the queue). Not thread-safe: callers hold the player transition lock.
/// </summary>
internal sealed class OriginalQueueOrder
{
    private readonly List<TrackDto> _order = [];

    public void Capture(IReadOnlyList<TrackDto> playOrder)
    {
        _order.Clear();
        _order.AddRange(playOrder);
    }

    public void Clear() => _order.Clear();

    public void Append(IEnumerable<TrackDto> tracks) => _order.AddRange(tracks);

    public void InsertAfter(TrackDto? anchor, IEnumerable<TrackDto> tracks)
    {
        int index = 0;

        if (anchor != null)
        {
            int anchorIndex = _order.FindIndex(track => ReferenceEquals(track, anchor));
            index = anchorIndex >= 0 ? anchorIndex + 1 : _order.Count;
        }

        _order.InsertRange(index, tracks);
    }

    public (List<TrackDto> Order, int CurrentIndex) Restore(IReadOnlyList<TrackDto> playOrder, TrackDto? current)
    {
        Dictionary<TrackDto, int> remaining = new(ReferenceEqualityComparer.Instance);

        foreach (TrackDto track in playOrder)
            remaining[track] = remaining.GetValueOrDefault(track) + 1;

        List<TrackDto> result = new(playOrder.Count);

        foreach (TrackDto track in _order)
        {
            if (!remaining.TryGetValue(track, out int count) || count == 0)
                continue;

            remaining[track] = count - 1;
            result.Add(track);
        }

        foreach (TrackDto track in playOrder)
        {
            if (remaining[track] == 0)
                continue;

            remaining[track]--;
            result.Add(track);
        }

        int currentIndex = current == null ? 0 : result.FindIndex(track => ReferenceEquals(track, current));

        return (result, Math.Max(currentIndex, 0));
    }
}