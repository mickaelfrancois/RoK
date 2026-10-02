namespace Rok.Application.Player.Mix;

/// <summary>Describes how a Mix plan was aligned on the beats of the two tracks.</summary>
/// <param name="OutgoingBpm">Tempo of the outgoing track, in beats per minute.</param>
/// <param name="IncomingBpm">Tempo of the incoming track, in beats per minute.</param>
/// <param name="StartShiftSeconds">How far the start of the mix was moved back to land on a beat of the outgoing track.</param>
public sealed record MixBeatAlignment(double OutgoingBpm, double IncomingBpm, double StartShiftSeconds)
{
    /// <summary>Duration of one beat of the outgoing track, in seconds.</summary>
    public double OutgoingPeriodSeconds => 60 / OutgoingBpm;
}