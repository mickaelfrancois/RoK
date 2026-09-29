using System.Collections.Frozen;

namespace Rok.Shared;

/// <summary>
/// Single source of truth for the audio file extensions Rok imports and plays.
/// </summary>
public static class AudioFormats
{
    /// <summary>
    /// Extensions imported into the library and played through NAudio / Media Foundation.
    /// </summary>
    public static IReadOnlySet<string> Supported { get; } = new[]
    {
        ".mp3",
        ".flac",
        ".m4a",
        ".aac",
        ".wma",
        ".wav",
        ".aiff",
        ".aif"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Audio extensions recognized as music but not supported yet, reported during onboarding.
    /// </summary>
    public static IReadOnlySet<string> KnownUnsupported { get; } = new[]
    {
        ".ogg",
        ".opus",
        ".ape",
        ".wv"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Returns <c>true</c> when the file extension of <paramref name="filePath"/> is a supported audio format.
    /// </summary>
    /// <param name="filePath">The file path or file name to check.</param>
    public static bool IsSupported(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return false;

        return Supported.Contains(Path.GetExtension(filePath));
    }
}