using Rok.Application.Player;
using Rok.Application.Randomizer;

namespace Rok.ViewModels.Tracks.Services;

public class TracksPlaybackService(IPlayerService playerService, ILogger<TracksPlaybackService> logger)
{
    public void PlayTracks(IEnumerable<TrackDto> tracks)
    {
        if (!tracks.Any())
        {
            logger.LogDebug("No track to listen.");
            return;
        }

        List<TrackDto> trackList = tracks.ToList();

        if (trackList.Count > 1)
            TracksRandomizer.Randomize(trackList);

        playerService.LoadPlaylist(trackList);
    }
}