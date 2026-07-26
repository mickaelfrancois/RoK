using Rok.Application.Dto;
using Rok.Application.Randomizer;

namespace Rok.ViewModels.Playlist.Services;

/// <summary>
/// Reorders a playlist with the artist-balanced randomizer while keeping the current head track in place.
/// Generic over the item type so it can be unit-tested against <see cref="TrackDto"/> directly, without
/// constructing view models.
/// </summary>
public static class PlaylistShuffleReorderer
{
    /// <summary>
    /// Produces a new list holding the same items, reordered by
    /// <see cref="TracksRandomizer.ArtistBalancedTrackRandomize"/>. The first item is preserved; the rest are
    /// shuffled with artist balancing.
    /// </summary>
    /// <typeparam name="T">The item type (e.g. a view model wrapping a <see cref="TrackDto"/>).</typeparam>
    /// <param name="items">The items to reorder.</param>
    /// <param name="dtoSelector">Projects an item onto its underlying <see cref="TrackDto"/>.</param>
    /// <param name="random">Optional random source; defaults to <see cref="Random.Shared"/>.</param>
    /// <returns>A new list with the same items in the reordered sequence.</returns>
    public static List<T> Reorder<T>(IReadOnlyList<T> items, Func<T, TrackDto> dtoSelector, Random? random = null)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(dtoSelector);

        if (items.Count <= 1)
            return items.ToList();

        Dictionary<long, Queue<T>> itemsById = new();

        foreach (T item in items)
        {
            long id = dtoSelector(item).Id;

            if (!itemsById.TryGetValue(id, out Queue<T>? queue))
            {
                queue = new Queue<T>();
                itemsById[id] = queue;
            }

            queue.Enqueue(item);
        }

        List<TrackDto> order = items.Select(dtoSelector).ToList();
        TracksRandomizer.ArtistBalancedTrackRandomize(order, shuffleStartIndex: -1, random);

        List<T> reordered = new(order.Count);

        foreach (TrackDto dto in order)
        {
            reordered.Add(itemsById[dto.Id].Dequeue());
        }

        return reordered;
    }
}