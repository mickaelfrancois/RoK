using Rok.Application.Tag;

namespace Rok.Infrastructure.Tag.Formats;

/// <summary>
/// Format-specific part of a tag write: pre-checks, title write and the fingerprint used to verify the copy.
/// </summary>
internal interface ITagFormat
{
    /// <summary>
    /// Checks format-specific preconditions on the original file.
    /// </summary>
    /// <param name="path">File to check.</param>
    /// <returns><see cref="ETagWriteReason.None"/> when the file can be written, otherwise the reason to skip it.</returns>
    ETagWriteReason CheckFile(string path);

    /// <summary>
    /// MIME type used to open files of this format, needed because a copy has a temporary extension.
    /// </summary>
    string MimeType { get; }

    /// <summary>
    /// Writes the changes into the file.
    /// </summary>
    /// <param name="path">File to write.</param>
    /// <param name="changes">Changes to apply.</param>
    void Write(string path, TagChanges changes);

    /// <summary>
    /// Reads what must be identical between the original and the written copy.
    /// </summary>
    /// <param name="path">File to read.</param>
    /// <param name="changes">Changes being written: the fields they alter are left out of the preserved fields.</param>
    /// <returns>The fingerprint of the file.</returns>
    TagFingerprint ReadFingerprint(string path, TagChanges changes);
}

/// <summary>
/// Properties of a file compared before and after a tag write.
/// </summary>
/// <param name="Values">Editable fields read back from the file, as the import reads them.</param>
/// <param name="Duration">Audio duration.</param>
/// <param name="Bitrate">Audio bitrate.</param>
/// <param name="SampleRate">Audio sample rate.</param>
/// <param name="AudioHash">Hash of the audio stream.</param>
/// <param name="PreservedFields">Sorted descriptors of the fields the write must not alter.</param>
internal sealed record TagFingerprint(
    TagFieldSnapshot Values,
    TimeSpan Duration,
    int Bitrate,
    int SampleRate,
    string AudioHash,
    IReadOnlyList<string> PreservedFields);