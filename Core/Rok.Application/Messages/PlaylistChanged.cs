namespace Rok.Application.Messages;

public class PlaylistChanged(List<TrackDto> tracks)
{
    public List<TrackDto> Tracks { get; private set; } = tracks;
}
