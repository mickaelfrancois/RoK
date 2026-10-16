namespace Rok.Application.Tag;

/// <summary>
/// Outcome of a write pre-check on one file.
/// </summary>
/// <param name="FilePath">Full path of the audio file.</param>
/// <param name="Reason"><see cref="ETagWriteReason.None"/> when the file can be written, otherwise why it cannot.</param>
/// <param name="CurrentArtist">Artist read as the library import reads it, null when the file cannot be read.</param>
public sealed record TagWriteCheck(string FilePath, ETagWriteReason Reason, string? CurrentArtist)
{
    /// <summary>
    /// Last write time of the file, when it could be read.
    /// </summary>
    public DateTimeOffset? FileDateModified { get; init; }

    /// <summary>
    /// Size in bytes of the file, when it could be read.
    /// </summary>
    public long? Size { get; init; }
}