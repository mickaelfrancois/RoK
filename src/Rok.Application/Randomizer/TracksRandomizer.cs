namespace Rok.Application.Randomizer;

public static class TracksRandomizer
{
    public static void ArtistBalancedTrackRandomize(List<TrackDto> playlist, int shuffleStartIndex, Random? random = null)
    {
        if (playlist == null || playlist.Count <= 1)
            return;

        if (shuffleStartIndex < 0) shuffleStartIndex = 0;
        if (shuffleStartIndex >= playlist.Count) shuffleStartIndex = playlist.Count - 1;

        int prefixCount = Math.Clamp(shuffleStartIndex + 1, 0, playlist.Count);

        if (prefixCount >= playlist.Count)
            return;

        List<TrackDto> tracksToShuffle = playlist.Skip(prefixCount).ToList();

        random ??= Random.Shared;
        for (int i = tracksToShuffle.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (tracksToShuffle[j], tracksToShuffle[i]) = (tracksToShuffle[i], tracksToShuffle[j]);
        }

        List<TrackDto> shuffledTracks = new();
        Dictionary<string, Queue<TrackDto>> artistGroups = tracksToShuffle
            .GroupBy(track => track.ArtistName)
            .ToDictionary(group => group.Key, group => new Queue<TrackDto>(group));

        string? lastArtist = null;

        while (shuffledTracks.Count < tracksToShuffle.Count)
        {
            List<string> availableArtists = artistGroups.Keys.Where(artist => artist != lastArtist).ToList();

            if (availableArtists.Count == 0)
            {
                availableArtists = artistGroups.Keys.ToList();
            }

            string selectedArtist = availableArtists[random.Next(availableArtists.Count)];
            TrackDto nextTrack = artistGroups[selectedArtist].Dequeue();
            shuffledTracks.Add(nextTrack);
            lastArtist = selectedArtist;

            if (artistGroups[selectedArtist].Count == 0)
            {
                artistGroups.Remove(selectedArtist);
            }
        }

        playlist.RemoveRange(prefixCount, playlist.Count - prefixCount);
        playlist.InsertRange(prefixCount, shuffledTracks);
    }


    public static void Randomize(List<TrackDto> tracks, Random? random = null)
    {
        if (tracks == null || tracks.Count <= 1)
            return;

        random ??= Random.Shared;

        for (int i = tracks.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (tracks[i], tracks[j]) = (tracks[j], tracks[i]);
        }
    }
}
