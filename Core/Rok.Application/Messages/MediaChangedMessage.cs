namespace Rok.Application.Messages;

public class MediaChangedMessage(TrackDto newTrack, TrackDto? previousTrack, long? durationPlayed)
{
    public TrackDto NewTrack { get; init; } = newTrack;

    public TrackDto? PreviousTrack { get; init; } = previousTrack;

    public long? DurationPlayed { get; init; } = durationPlayed;
}
