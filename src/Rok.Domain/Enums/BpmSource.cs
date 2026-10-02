namespace Rok.Domain.Enums;

/// <summary>Origin of a track tempo.</summary>
public enum BpmSource
{
    /// <summary>Read from the BPM tag of the file.</summary>
    Tag = 1,

    /// <summary>Found by the tempo detector.</summary>
    Detected = 2
}