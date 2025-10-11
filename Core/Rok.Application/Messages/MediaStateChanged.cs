using Rok.Application.Player;

namespace Rok.Application.Messages;

public class MediaStateChanged(EPlaybackState playerState)
{
    public EPlaybackState State { get; private set; } = playerState;
}