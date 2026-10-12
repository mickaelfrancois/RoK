namespace Rok.Application.Tag;

/// <summary>
/// Tag fields to change. A null field is left untouched.
/// </summary>
public sealed record TagChanges
{
    /// <summary>
    /// New title, or null to keep the current one.
    /// </summary>
    public string? Title { get; init; }
}

/// <summary>
/// A tag write request for one file.
/// </summary>
/// <param name="FilePath">Full path of the audio file.</param>
/// <param name="Changes">Changes to apply.</param>
public sealed record TagWriteRequest(string FilePath, TagChanges Changes);