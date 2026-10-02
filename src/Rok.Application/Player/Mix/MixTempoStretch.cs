namespace Rok.Application.Player.Mix;

/// <summary>Time-stretch applied to the incoming track of a Mix crossfade.</summary>
/// <param name="Ratio">Playback-rate factor of the incoming track during the mix (above 1 speeds it up).</param>
/// <param name="Bars">Number of outgoing bars the mix lasts.</param>
/// <param name="ReturnSeconds">Duration of the linear return to the original tempo once the mix is over.</param>
public sealed record MixTempoStretch(double Ratio, int Bars, double ReturnSeconds);