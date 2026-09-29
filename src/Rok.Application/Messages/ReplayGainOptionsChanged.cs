namespace Rok.Application.Messages;

/// <summary>Sent when the ReplayGain mode or preamp changes, so the player recomputes the gain of the live tracks.</summary>
public sealed record ReplayGainOptionsChanged;