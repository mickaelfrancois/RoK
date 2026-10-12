namespace Rok.Application.Tag;

/// <summary>
/// Final state of a tag write on one file.
/// </summary>
public enum ETagWriteOutcome
{
    Succeeded,
    Skipped,
    Failed
}

/// <summary>
/// Why a tag write was skipped or failed.
/// </summary>
public enum ETagWriteReason
{
    None,
    NotFound,
    ReadOnly,
    CloudPlaceholder,
    UnsupportedFormat,
    Locked,
    InsufficientDiskSpace,
    UnsupportedTagVersion,
    Playing,
    ImportInProgress,
    Cancelled,
    VerificationFailed,
    IoError
}

/// <summary>
/// Result of a tag write on one file.
/// </summary>
/// <param name="FilePath">Full path of the audio file.</param>
/// <param name="Outcome">Final state.</param>
/// <param name="Reason">Reason when not succeeded, otherwise <see cref="ETagWriteReason.None"/>.</param>
/// <param name="FileDateModified">Last write time of the file after the write, when succeeded.</param>
/// <param name="Size">Size in bytes of the file after the write, when succeeded.</param>
public sealed record TagWriteResult(
    string FilePath,
    ETagWriteOutcome Outcome,
    ETagWriteReason Reason,
    DateTimeOffset? FileDateModified,
    long? Size);