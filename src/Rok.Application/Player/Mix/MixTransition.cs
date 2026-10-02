namespace Rok.Application.Player.Mix;

/// <summary>Engine-side parameters of a Mix crossfade.</summary>
/// <param name="IncomingStartSeconds">
/// Position in the incoming track where it starts playing. A value that is not strictly between zero and the
/// incoming length is ignored.
/// </param>
/// <param name="BassSwap">Whether the bass of the two tracks is swapped during the mix.</param>
/// <param name="BassSwapAtSeconds">
/// Offset in seconds from the start of the mix at which the bass swap happens; null means the middle of the mix.
/// A value that is not strictly inside the mix is ignored.
/// </param>
public sealed record MixTransition(double IncomingStartSeconds, bool BassSwap, double? BassSwapAtSeconds = null);