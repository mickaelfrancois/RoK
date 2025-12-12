using Rok.Application.Randomizer;
using Rok.Logic.Services.Player;

namespace Rok.Logic.ViewModels.Tracks.Services;

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
            trackList = TracksRandomizer.Randomize(trackList);

        playerService.LoadPlaylist(trackList);
    }
}