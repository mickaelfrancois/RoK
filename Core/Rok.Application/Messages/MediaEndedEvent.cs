using Rok.Application.Player;

namespace Rok.Application.Messages;

public class MediaEndedEvent(EPlaybackState playerState, TrackDto track)
{
    public EPlaybackState State { get; private set; } = playerState;

    public TrackDto Track { get; private set; } = track;
}
