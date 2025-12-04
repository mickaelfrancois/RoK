using Rok.Logic.Services.Player;
using Rok.Logic.ViewModels.Tracks;

namespace Rok.Logic.ViewModels.Listening.Services;

public class ListeningPlaybackService(IPlayerService playerService, ListeningDataLoader dataLoader)
{
    public void ShuffleTracks()
    {
        playerService.ShuffleTracks();
    }

    public async Task AddMoreFromArtistAsync(TrackViewModel track, IEnumerable<long> currentTrackIds, int maxTracks = 3)
    {
        if (!track.Track.ArtistId.HasValue)
            return;

        List<TrackDto> tracksToAdd = await dataLoader.GetTracksByArtistAsync(
            track.Track.ArtistId.Value,
            maxTracks,
            currentTrackIds);

        if (tracksToAdd.Count > 0)
        {
            playerService.InsertTracksToPlaylist(tracksToAdd);
        }
    }
}