using System.Globalization;
using Rok.Application.Features.Artists.Rename;
using Rok.Application.Features.Tracks.Services;

namespace Rok.Services;

/// <summary>Builds the texts of the artist rename screens from the resource strings.</summary>
public static class ArtistRenameTexts
{
    private const string Separator = " — ";

    /// <summary>Gives the line "old name to new name".</summary>
    /// <param name="preview">What the rename would do.</param>
    /// <returns>The line.</returns>
    public static string Summary(ArtistRenamePreview preview) => $"{preview.OldName} → {preview.NewName}";

    /// <summary>Gives the line that counts the files and albums concerned.</summary>
    /// <param name="preview">What the rename would do.</param>
    /// <param name="localize">Gives a resource string from its key.</param>
    /// <returns>The line.</returns>
    public static string Counts(ArtistRenamePreview preview, Func<string, string> localize)
    {
        return string.Format(CultureInfo.CurrentCulture, localize(ArtistRenameTextKeys.Counts), preview.FileCount, preview.AlbumCount);
    }

    /// <summary>Gives the message shown when another artist already carries the name.</summary>
    /// <param name="collision">The other artist.</param>
    /// <param name="localize">Gives a resource string from its key.</param>
    /// <returns>The message.</returns>
    public static string CollisionMessage(ArtistRenameCollision collision, Func<string, string> localize)
    {
        return string.Format(CultureInfo.CurrentCulture, localize(ArtistRenameTextKeys.Collision), collision.Name, collision.AlbumCount, collision.TrackCount);
    }

    /// <summary>Lists the reasons that block the rename, one line each.</summary>
    /// <param name="blockers">The reasons.</param>
    /// <param name="localize">Gives a resource string from its key.</param>
    /// <returns>The lines.</returns>
    public static IReadOnlyList<string> BlockerLines(IEnumerable<ArtistRenameBlocker> blockers, Func<string, string> localize)
    {
        return [.. blockers.Select(blocker => BlockerLine(blocker, localize))];
    }

    /// <summary>Lists the files that could not be written, one line each.</summary>
    /// <param name="failures">The files.</param>
    /// <param name="localize">Gives a resource string from its key.</param>
    /// <returns>The lines.</returns>
    public static IReadOnlyList<string> FailureLines(IEnumerable<ArtistRenameFailure> failures, Func<string, string> localize)
    {
        return [.. failures.Select(failure => $"{failure.FilePath}{Separator}{localize(TrackEditTextKeys.Error(TrackTagEditResult.CodeFor(failure.Reason)))}")];
    }

    /// <summary>Gives the message of a refused request, the generic one when the code is unknown or has no text.</summary>
    /// <param name="errorCode">The error code of the request.</param>
    /// <param name="localize">Gives a resource string from its key.</param>
    /// <returns>The message.</returns>
    public static string ErrorMessage(string? errorCode, Func<string, string> localize)
    {
        string message = localize(ArtistRenameTextKeys.Error(errorCode));

        return string.IsNullOrEmpty(message) ? localize(ArtistRenameTextKeys.GenericError) : message;
    }

    private static string BlockerLine(ArtistRenameBlocker blocker, Func<string, string> localize)
    {
        string reason = localize(ArtistRenameTextKeys.Blocker(blocker.Reason));

        if (blocker.Reason == EArtistRenameBlock.OutOfSync && !string.IsNullOrEmpty(blocker.Detail))
            reason = $"{reason} ({blocker.Detail})";

        return string.IsNullOrEmpty(blocker.FilePath) ? reason : $"{blocker.FilePath}{Separator}{reason}";
    }
}