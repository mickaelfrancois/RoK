namespace Rok.Application.Player.Mix;

/// <summary>A planned Mix transition between two tracks.</summary>
/// <param name="OutgoingTrackId">Track that fades out.</param>
/// <param name="IncomingTrackId">Track that fades in.</param>
/// <param name="StartSeconds">Position in the outgoing track where the fade starts.</param>
/// <param name="DurationSeconds">Duration of the fade.</param>
/// <param name="MusicEndSeconds">Position in the outgoing track where the music ends.</param>
/// <param name="IncomingStartSeconds">Position in the incoming track where it starts playing.</param>
public sealed record MixPlan(
    long OutgoingTrackId,
    long IncomingTrackId,
    double StartSeconds,
    double DurationSeconds,
    double MusicEndSeconds,
    double IncomingStartSeconds);