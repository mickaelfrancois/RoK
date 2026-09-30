namespace Rok.Application.Player.Output;

/// <summary>Raised when the device of the open output disappears or fails.</summary>
/// <param name="wasPlaying">Playback was running when the output was lost.</param>
/// <param name="wasOnPreferred">The lost output was on the requested device.</param>
public sealed class OutputLostEventArgs(bool wasPlaying, bool wasOnPreferred) : EventArgs
{
    public bool WasPlaying { get; } = wasPlaying;

    public bool WasOnPreferred { get; } = wasOnPreferred;
}