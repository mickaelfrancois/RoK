namespace Rok.Application.Interfaces;

/// <summary>Describes a gapless switch from the playing track to the queued one.</summary>
/// <param name="Track">Track now playing.</param>
/// <param name="PreviousTrackPosition">Final position, in seconds, of the track that just ended.</param>
public sealed record GaplessTransitionEventArgs(TrackDto Track, double PreviousTrackPosition);