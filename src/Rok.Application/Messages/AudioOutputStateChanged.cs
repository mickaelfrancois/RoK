using Rok.Application.Player.Output;

namespace Rok.Application.Messages;

/// <summary>Sent off the UI thread each time the actual state of the audio output changes.</summary>
public sealed record AudioOutputStateChanged(AudioOutputState State, bool IsLive);