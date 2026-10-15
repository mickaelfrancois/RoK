namespace Rok.Services;

/// <summary>State of one field of the track edit dialog.</summary>
public enum TrackEditFieldState
{
    /// <summary>The typed value equals the current one.</summary>
    Unchanged,

    /// <summary>The typed value is valid and differs from the current one.</summary>
    Changed,

    /// <summary>The typed value cannot be saved.</summary>
    Invalid
}