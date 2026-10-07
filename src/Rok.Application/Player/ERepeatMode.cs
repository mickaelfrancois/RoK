namespace Rok.Application.Player;

/// <summary>Repeat behavior of the player queue.</summary>
public enum ERepeatMode
{
    /// <summary>The queue stops after its last track.</summary>
    Off = 0,

    /// <summary>The queue restarts from its first track after the last one.</summary>
    All = 1,

    /// <summary>The current track restarts when it ends naturally.</summary>
    One = 2
}