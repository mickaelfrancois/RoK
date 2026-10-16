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

    /// <summary>
    /// New track number (positive), or null to keep the current one. The track total is kept.
    /// </summary>
    public int? TrackNumber { get; init; }

    /// <summary>
    /// New disc number (positive), or null to keep the current one. The disc total is kept.
    /// </summary>
    public int? Disc { get; init; }

    /// <summary>
    /// New composers, an empty string to remove them, or null to keep the current ones.
    /// </summary>
    public string? Composers { get; init; }

    /// <summary>
    /// New beats per minute, 0 to remove it, or null to keep the current one.
    /// </summary>
    public int? Bpm { get; init; }

    /// <summary>
    /// New name for the field that defines the artist (the album artist when the file has one, otherwise the performer),
    /// or null to keep it. Only the first value is replaced, the other ones are kept.
    /// </summary>
    public string? Artist { get; init; }

    /// <summary>
    /// Tells whether at least one field is to be changed.
    /// </summary>
    public bool HasChanges => Title is not null || TrackNumber is not null || Disc is not null || Composers is not null || Bpm is not null || Artist is not null;
}

/// <summary>
/// A tag write request for one file.
/// </summary>
/// <param name="FilePath">Full path of the audio file.</param>
/// <param name="Changes">Changes to apply.</param>
public sealed record TagWriteRequest(string FilePath, TagChanges Changes);