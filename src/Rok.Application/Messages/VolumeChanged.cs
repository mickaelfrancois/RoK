namespace Rok.Application.Messages;

/// <summary>
/// Sent when the player volume or its muted state changes.
/// </summary>
/// <param name="Volume">The current volume, from 0 to 100.</param>
/// <param name="IsMuted">Whether the player is muted.</param>
public sealed record VolumeChanged(double Volume, bool IsMuted);