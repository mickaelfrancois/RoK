namespace Rok.Application.Messages;

public class MediaAboutToEndEvent(TrackDto track)
{
    public TrackDto Track { get; private set; } = track;
}