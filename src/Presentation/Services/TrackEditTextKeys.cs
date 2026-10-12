using Rok.Application.Features.Tracks.Services;
using Rok.Application.Tag;

namespace Rok.Services;

/// <summary>Resource keys of the texts of the track title edit.</summary>
public static class TrackEditTextKeys
{
    /// <summary>Key of the message used when no specific one matches.</summary>
    public const string GenericError = "trackEditError_Generic";

    private static readonly Dictionary<string, string> ErrorKeys = new()
    {
        [TrackTagEditResult.CodeFor(ETagWriteReason.Playing)] = "trackEditError_Playing",
        [TrackTagEditResult.CodeFor(ETagWriteReason.ImportInProgress)] = "trackEditError_ImportInProgress",
        [TrackTagEditResult.CodeFor(ETagWriteReason.UnsupportedFormat)] = "trackEditError_UnsupportedFormat",
        [TrackTagEditResult.CodeFor(ETagWriteReason.ReadOnly)] = "trackEditError_ReadOnly",
        [TrackTagEditResult.CodeFor(ETagWriteReason.Locked)] = "trackEditError_Locked",
        [TrackTagEditResult.CodeFor(ETagWriteReason.CloudPlaceholder)] = "trackEditError_CloudPlaceholder",
        [TrackTagEditResult.CodeFor(ETagWriteReason.NotFound)] = "trackEditError_NotFound",
        [TrackTagEditResult.CodeFor(ETagWriteReason.InsufficientDiskSpace)] = "trackEditError_InsufficientDiskSpace",
        [TrackTagEditResult.CodeFor(ETagWriteReason.UnsupportedTagVersion)] = "trackEditError_UnsupportedTagVersion",
        [TrackTagEditResult.CodeFor(ETagWriteReason.VerificationFailed)] = "trackEditError_VerificationFailed",
        [TrackTagEditResult.CodeFor(ETagWriteReason.IoError)] = "trackEditError_IoError",
        [TrackTagEditResult.DisabledCode] = "trackEditError_Disabled"
    };

    /// <summary>Gets every error message key, the generic one included.</summary>
    public static IReadOnlyCollection<string> AllErrorKeys { get; } = [.. ErrorKeys.Values, GenericError];

    /// <summary>Maps an error code of the title update to the key of its message.</summary>
    /// <param name="errorCode">The error code, for example <c>track.tag_write.read_only</c>.</param>
    /// <returns>The resource key, <see cref="GenericError"/> when the code is unknown.</returns>
    public static string Error(string? errorCode)
    {
        if (errorCode is not null && ErrorKeys.TryGetValue(errorCode, out string? key))
            return key;

        return GenericError;
    }
}