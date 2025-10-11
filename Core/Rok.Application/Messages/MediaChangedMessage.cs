namespace Rok.Application.Messages;

public class MediaChangedMessage(TrackDto newTrack, TrackDto? previousTrack)
{
    public TrackDto NewTrack { get; init; } = newTrack;

    public TrackDto? PreviousTrack { get; init; } = previousTrack;
}
