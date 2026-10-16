using System.Globalization;
using Rok.Application.Features.Artists.Merge;
using Rok.Application.Features.Artists.Rename;

namespace Rok.Services;

/// <summary>Builds the texts of the artist merge screens from the resource strings.</summary>
public static class ArtistMergeTexts
{
    private const int MaxValueLength = 60;

    /// <summary>Gives the sentence shown when a rename collides with an existing artist.</summary>
    /// <param name="collision">The existing artist.</param>
    /// <param name="localize">Gives a resource string from its key.</param>
    /// <returns>The sentence.</returns>
    public static string CollisionIntro(ArtistRenameCollision collision, Func<string, string> localize)
    {
        return string.Format(CultureInfo.CurrentCulture, localize(ArtistMergeTextKeys.Collision), collision.Name, collision.AlbumCount, collision.TrackCount);
    }

    /// <summary>Gives the line "merged artist into kept artist".</summary>
    /// <param name="preview">What the merge would do.</param>
    /// <param name="localize">Gives a resource string from its key.</param>
    /// <returns>The line.</returns>
    public static string Summary(ArtistMergePreview preview, Func<string, string> localize)
    {
        return string.Format(CultureInfo.CurrentCulture, localize(ArtistMergeTextKeys.Summary), preview.SourceName, preview.TargetName);
    }

    /// <summary>Gives the line that counts the files and albums concerned.</summary>
    /// <param name="preview">What the merge would do.</param>
    /// <param name="localize">Gives a resource string from its key.</param>
    /// <returns>The line.</returns>
    public static string Counts(ArtistMergePreview preview, Func<string, string> localize)
    {
        return string.Format(CultureInfo.CurrentCulture, localize(ArtistMergeTextKeys.Counts), preview.FileCount, preview.AlbumCount);
    }

    /// <summary>Lists the albums that become one, one line each.</summary>
    /// <param name="preview">What the merge would do.</param>
    /// <param name="localize">Gives a resource string from its key.</param>
    /// <returns>The lines.</returns>
    public static IReadOnlyList<string> AlbumLines(ArtistMergePreview preview, Func<string, string> localize)
    {
        string format = localize(ArtistMergeTextKeys.AlbumLine);

        return [.. preview.MergedAlbums.Select(album => string.Format(CultureInfo.CurrentCulture, format, album.Name, album.AlbumCount))];
    }

    /// <summary>Lists the fields that differ, one line each. Long values are shortened.</summary>
    /// <param name="preview">What the merge would do.</param>
    /// <param name="localize">Gives a resource string from its key.</param>
    /// <returns>The lines.</returns>
    public static IReadOnlyList<string> ConflictLines(ArtistMergePreview preview, Func<string, string> localize)
    {
        string format = localize(ArtistMergeTextKeys.ConflictLine);

        return [.. preview.Conflicts.Select(conflict => string.Format(CultureInfo.CurrentCulture, format, conflict.Field, Shorten(conflict.TargetValue), Shorten(conflict.SourceValue)))];
    }

    /// <summary>Lists the smart playlists whose result can change, one line each.</summary>
    /// <param name="preview">What the merge would do.</param>
    /// <param name="localize">Gives a resource string from its key.</param>
    /// <returns>The lines.</returns>
    public static IReadOnlyList<string> PlaylistLines(ArtistMergePreview preview, Func<string, string> localize)
    {
        string rewritten = localize(ArtistMergeTextKeys.PlaylistRewritten);

        return [.. preview.Playlists.Select(playlist => playlist.IsRewritten ? $"{playlist.Name} ({rewritten})" : playlist.Name)];
    }

    /// <summary>Tells which picture survives.</summary>
    /// <param name="preview">What the merge would do.</param>
    /// <param name="localize">Gives a resource string from its key.</param>
    /// <returns>The line, null when neither artist has a picture.</returns>
    public static string? PictureLine(ArtistMergePreview preview, Func<string, string> localize)
    {
        return preview.Picture switch
        {
            EArtistMergePicture.Target => string.Format(CultureInfo.CurrentCulture, localize(ArtistMergeTextKeys.PictureTarget), preview.TargetName),
            EArtistMergePicture.Source => string.Format(CultureInfo.CurrentCulture, localize(ArtistMergeTextKeys.PictureSource), preview.SourceName),
            _ => null
        };
    }

    /// <summary>Gives the message of a refused request, the generic one when the code is unknown or has no text.</summary>
    /// <param name="errorCode">The error code of the request.</param>
    /// <param name="localize">Gives a resource string from its key.</param>
    /// <returns>The message.</returns>
    public static string ErrorMessage(string? errorCode, Func<string, string> localize)
    {
        string message = localize(ArtistMergeTextKeys.Error(errorCode));

        return string.IsNullOrEmpty(message) ? localize(ArtistMergeTextKeys.GenericError) : message;
    }

    private static string Shorten(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        string singleLine = value.ReplaceLineEndings(" ").Trim();

        return singleLine.Length <= MaxValueLength ? singleLine : $"{singleLine[..MaxValueLength]}…";
    }
}