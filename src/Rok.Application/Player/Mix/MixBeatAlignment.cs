namespace Rok.Application.Player.Mix;

/// <summary>Describes how a Mix plan was aligned on the beats of the two tracks.</summary>
/// <param name="OutgoingBpm">Tempo of the outgoing track, in beats per minute.</param>
/// <param name="IncomingBpm">Tempo of the incoming track, in beats per minute.</param>
/// <param name="StartShiftSeconds">Offset of the start of the mix from the default start, positive when the mix starts earlier.</param>
/// <param name="BarAligned">True when the plan is aligned on bars (downbeats) and not only on beats.</param>
public sealed record MixBeatAlignment(double OutgoingBpm, double IncomingBpm, double StartShiftSeconds, bool BarAligned = false)
{
    /// <summary>Duration of one beat of the outgoing track, in seconds.</summary>
    public double OutgoingPeriodSeconds => 60 / OutgoingBpm;

    /// <summary>Period the plan is aligned on: one bar of the outgoing track when bar-aligned, otherwise one beat.</summary>
    public double AlignmentPeriodSeconds => BarAligned ? OutgoingPeriodSeconds * MixThresholds.BeatsPerBar : OutgoingPeriodSeconds;
}