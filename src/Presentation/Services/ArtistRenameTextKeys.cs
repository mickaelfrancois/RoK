using Rok.Application.Features.Artists.Rename;

namespace Rok.Services;

/// <summary>Resource keys of the texts of the artist rename.</summary>
public static class ArtistRenameTextKeys
{
    /// <summary>Key of the message shown under the name field when another artist already carries the name. Takes the name, the album count and the track count.</summary>
    public const string Collision = "artistRenameCollision";

    /// <summary>Key of the line that gives the number of files and albums. Takes both numbers.</summary>
    public const string Counts = "artistRenameCounts";

    /// <summary>Key of the header of the list of reasons that block the rename.</summary>
    public const string BlockersHeader = "artistRenameBlockersHeader";

    /// <summary>Key of the header of the list of files that still carry the old name.</summary>
    public const string FailuresHeader = "artistRenameFailuresHeader";

    /// <summary>Key of the text of the button that starts the rename.</summary>
    public const string StartButton = "artistRenameStart";

    /// <summary>Key of the text of the button that finishes an interrupted rename.</summary>
    public const string ResumeButton = "artistRenameResume";

    /// <summary>Key of the text of the button that tries again after an incomplete rename.</summary>
    public const string RetryButton = "artistRenameRetry";

    /// <summary>Key of the message used when no specific one matches.</summary>
    public const string GenericError = "artistRenameError_Generic";

    /// <summary>Key of the message shown when the typed name is empty or too long.</summary>
    public const string InvalidName = "artistRenameError_InvalidName";

    private static readonly Dictionary<string, string> ErrorKeys = new()
    {
        ["artist.rename.disabled"] = "trackEditError_Disabled",
        ["artist.rename.collision"] = "artistRenameError_Collision",
        ["artist.rename.pending"] = "artistRenameError_Pending",
        ["artist.rename.unchanged"] = "artistRenameError_Unchanged",
        ["artist.rename.invalid_name"] = InvalidName,
        ["artist.not_found"] = "artistRenameError_NotFound"
    };

    /// <summary>Gets every key of the screens of the rename that the resource files must hold.</summary>
    public static IReadOnlyCollection<string> AllKeys { get; } =
    [
        Collision,
        Counts,
        BlockersHeader,
        FailuresHeader,
        StartButton,
        ResumeButton,
        RetryButton,
        GenericError,
        .. ErrorKeys.Values.Distinct(),
        .. Enum.GetValues<EArtistRenameBlock>().Select(Blocker)
    ];

    /// <summary>Maps the reason of a blocked rename to the key of its message.</summary>
    /// <param name="reason">The reason.</param>
    /// <returns>The resource key.</returns>
    public static string Blocker(EArtistRenameBlock reason) => $"artistRenameBlock_{reason}";

    /// <summary>Maps an error code of the rename to the key of its message.</summary>
    /// <param name="errorCode">The error code, for example <c>artist.rename.collision</c>.</param>
    /// <returns>The resource key, <see cref="GenericError"/> when the code is unknown.</returns>
    public static string Error(string? errorCode)
    {
        if (errorCode is not null && ErrorKeys.TryGetValue(errorCode, out string? key))
            return key;

        return GenericError;
    }
}