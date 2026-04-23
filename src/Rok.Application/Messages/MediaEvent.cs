using Rok.Application.Player;

namespace Rok.Application.Messages;

public class MediaEvent(EPlaybackState playerState, TrackDto track)
{
    public EPlaybackState State { get; init; } = playerState;

    public TrackDto Track { get; init; } = track;
}
