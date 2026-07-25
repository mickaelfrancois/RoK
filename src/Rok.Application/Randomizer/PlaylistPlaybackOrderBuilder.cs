namespace Rok.Application.Randomizer;

public static class PlaylistPlaybackOrderBuilder
{
    public static List<TrackDto> BuildShuffledPlayOrder(IEnumerable<TrackDto> tracks, TrackDto? clickedTrack, Random? random = null)
    {
        List<TrackDto> ordered = tracks.ToList();

        if (ordered.Count <= 1)
            return ordered;

        random ??= Random.Shared;

        if (clickedTrack == null)
        {
            int randomIndex = random.Next(ordered.Count);
            (ordered[0], ordered[randomIndex]) = (ordered[randomIndex], ordered[0]);

            TracksRandomizer.ArtistBalancedTrackRandomize(ordered, 0, random);
            return ordered;
        }

        int clickedIndex = ordered.FindIndex(t => t.Id == clickedTrack.Id);
        if (clickedIndex > 0)
        {
            TrackDto clicked = ordered[clickedIndex];
            ordered.RemoveAt(clickedIndex);
            ordered.Insert(0, clicked);
        }

        TracksRandomizer.ArtistBalancedTrackRandomize(ordered, 0, random);
        return ordered;
    }
}