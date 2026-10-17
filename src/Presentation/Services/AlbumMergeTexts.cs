using System.Globalization;
using Rok.Application.Features.Albums.Edit;
using Rok.Application.Features.Albums.Merge;

namespace Rok.Services;

/// <summary>Builds the texts of the album merge screens from the resource strings.</summary>
public static class AlbumMergeTexts
{
    private const int MaxValueLength = 60;

    /// <summary>Gives the sentence shown when a rename collides with an existing album.</summary>
    /// <param name="collision">The existing album.</param>
    /// <param name="localize">Gives a resource string from its key.</param>
    /// <returns>The sentence.</returns>
    public static string CollisionIntro(AlbumCollision collision, Func<string, string> localize)
    {
        return Format(localize, AlbumMergeTextKeys.Collision, collision.Name, collision.ArtistName, collision.TrackCount);
    }

    /// <summary>Gives the line "merged album into kept album".</summary>
    /// <param name="preview">What the merge would do.</param>
    /// <param name="localize">Gives a resource string from its key.</param>
    /// <returns>The line.</returns>
    public static string Summary(AlbumMergePreview preview, Func<string, string> localize)
    {
        return Format(localize, AlbumMergeTextKeys.Summary, preview.SourceName, preview.SourceArtist, preview.TargetName, preview.TargetArtist);
    }

    /// <summary>Gives the line that counts the files that are written.</summary>
    /// <param name="preview">What the merge would do.</param>
    /// <param name="localize">Gives a resource string from its key.</param>
    /// <returns>The line.</returns>
    public static string Counts(AlbumMergePreview preview, Func<string, string> localize)
    {
        return Format(localize, AlbumEditTextKeys.Counts, preview.FileCount);
    }

    /// <summary>Lists the notes of a merge, one line each: artist change, separate folders, orphan artist.</summary>
    /// <param name="preview">What the merge would do.</param>
    /// <param name="localize">Gives a resource string from its key.</param>
    /// <returns>The lines.</returns>
    public static IReadOnlyList<string> NoteLines(AlbumMergePreview preview, Func<string, string> localize)
    {
        List<string> lines = [];

        if (preview.ArtistChanges)
            lines.Add(Format(localize, AlbumMergeTextKeys.ArtistChange, preview.SourceArtist, preview.TargetArtist));

        if (preview.SeparateFolders)
            lines.Add(localize(AlbumMergeTextKeys.SeparateFolders));

        if (preview.OrphanArtist is not null)
        {
            string key = preview.OrphanArtist.IsFavorite ? AlbumEditTextKeys.OrphanFavoriteArtist : AlbumEditTextKeys.OrphanArtist;

            lines.Add(Format(localize, key, preview.OrphanArtist.Name, preview.OrphanArtist.ListenCount));
        }

        return lines;
    }

    /// <summary>Lists the fields that differ, one line each. Long values are shortened.</summary>
    /// <param name="preview">What the merge would do.</param>
    /// <param name="localize">Gives a resource string from its key.</param>
    /// <returns>The lines.</returns>
    public static IReadOnlyList<string> ConflictLines(AlbumMergePreview preview, Func<string, string> localize)
    {
        return [.. preview.FieldConflicts.Select(conflict => Format(localize, AlbumMergeTextKeys.ConflictLine, conflict.Field, Shorten(conflict.TargetValue), Shorten(conflict.SourceValue)))];
    }

    /// <summary>Lists the pairs of tracks that share a disc and a number, one line each.</summary>
    /// <param name="preview">What the merge would do.</param>
    /// <param name="localize">Gives a resource string from its key.</param>
    /// <returns>The lines.</returns>
    public static IReadOnlyList<string> DiscConflictLines(AlbumMergePreview preview, Func<string, string> localize)
    {
        return [.. preview.DiscConflicts.Select(conflict => Format(localize, AlbumMergeTextKeys.DiscConflictLine, conflict.Disc, conflict.TrackNumber, conflict.SourceTitle, conflict.TargetTitle))];
    }

    /// <summary>Gives the text of the check box that shifts the discs.</summary>
    /// <param name="preview">What the merge would do.</param>
    /// <param name="localize">Gives a resource string from its key.</param>
    /// <returns>The text.</returns>
    public static string ShiftDiscs(AlbumMergePreview preview, Func<string, string> localize)
    {
        return Format(localize, AlbumMergeTextKeys.ShiftDiscs, preview.SuggestedDiscOffset);
    }

    /// <summary>Gives the message of a refused request, the generic one when the code is unknown or has no text.</summary>
    /// <param name="errorCode">The error code of the request.</param>
    /// <param name="localize">Gives a resource string from its key.</param>
    /// <returns>The message.</returns>
    public static string ErrorMessage(string? errorCode, Func<string, string> localize)
    {
        string message = localize(AlbumMergeTextKeys.Error(errorCode));

        return string.IsNullOrEmpty(message) ? localize(AlbumMergeTextKeys.GenericError) : message;
    }

    private static string Format(Func<string, string> localize, string key, params object[] arguments)
    {
        return string.Format(CultureInfo.CurrentCulture, localize(key), arguments);
    }

    private static string Shorten(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        string singleLine = value.ReplaceLineEndings(" ").Trim();

        return singleLine.Length <= MaxValueLength ? singleLine : $"{singleLine[..MaxValueLength]}…";
    }
}