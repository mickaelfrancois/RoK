namespace Rok.Application.Messages;

/// <summary>Sent when the output device or mode changes, so the player reopens its output on the new target.</summary>
public sealed record AudioOutputOptionsChanged;