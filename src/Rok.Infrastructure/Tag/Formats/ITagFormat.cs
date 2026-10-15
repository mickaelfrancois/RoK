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
    /// Writes the title into the file, opened with an explicit MIME type because the copy has a temporary extension.
    /// </summary>
    /// <param name="path">File to write.</param>
    /// <param name="title">New title.</param>
    void WriteTitle(string path, string title);

    /// <summary>
    /// Reads what must be identical between the original and the written copy.
    /// </summary>
    /// <param name="path">File to read.</param>
    /// <returns>The fingerprint of the file.</returns>
    TagFingerprint ReadFingerprint(string path);
}

/// <summary>
/// Properties of a file compared before and after a tag write.
/// </summary>
/// <param name="Title">Title read back from the file.</param>
/// <param name="Duration">Audio duration.</param>
/// <param name="Bitrate">Audio bitrate.</param>
/// <param name="SampleRate">Audio sample rate.</param>
/// <param name="AudioHash">Hash of the audio stream.</param>
/// <param name="PreservedFields">Sorted descriptors of the fields the write must not alter.</param>
internal sealed record TagFingerprint(
    string? Title,
    TimeSpan Duration,
    int Bitrate,
    int SampleRate,
    string AudioHash,
    IReadOnlyList<string> PreservedFields);