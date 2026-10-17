using System.Globalization;
using Rok.Application.Features.Albums.Edit;
using Rok.Application.Features.Artists.Rename;

namespace Rok.Services;

/// <summary>Builds the texts of the album edit screens from the resource strings.</summary>
public static class AlbumEditTexts
{
    private const string NoYear = "—";

    /// <summary>Builds what the write dialog shows for an edit that is about to start.</summary>
    /// <param name="preview">What the edit would do.</param>
    /// <param name="localize">Gives a resource string from its key.</param>
    /// <returns>The prompt.</returns>
    public static AlbumTagWritePrompt Prompt(AlbumEditPreview preview, Func<string, string> localize)
    {
        return new AlbumTagWritePrompt(ChangeLines(preview, localize), NoteLines(preview, localize), ArtistRenameTexts.BlockerLines(preview.Blockers, localize), false);
    }

    /// <summary>Builds what the write dialog shows for an interrupted write that is about to be finished.</summary>
    /// <param name="localize">Gives a resource string from its key.</param>
    /// <returns>The prompt.</returns>
    public static AlbumTagWritePrompt ResumePrompt(Func<string, string> localize)
    {
        return new AlbumTagWritePrompt([], [localize(AlbumEditTextKeys.ResumeIntro)], [], true);
    }

    /// <summary>Builds what the write dialog shows when the compilation status of an album is about to change.</summary>
    /// <param name="isCompilation">The status asked by the user.</param>
    /// <param name="localize">Gives a resource string from its key.</param>
    /// <returns>The prompt.</returns>
    public static AlbumTagWritePrompt CompilationPrompt(bool isCompilation, Func<string, string> localize)
    {
        string key = isCompilation ? AlbumEditTextKeys.CompilationOn : AlbumEditTextKeys.CompilationOff;

        return new AlbumTagWritePrompt([], [localize(key)], [], false);
    }

    /// <summary>Lists the fields that change, one line each, old value to new value.</summary>
    /// <param name="preview">What the edit would do.</param>
    /// <param name="localize">Gives a resource string from its key.</param>
    /// <returns>The lines.</returns>
    public static IReadOnlyList<string> ChangeLines(AlbumEditPreview preview, Func<string, string> localize)
    {
        List<string> lines = [];

        if (preview.OldName != preview.NewName)
            lines.Add(Format(localize, AlbumEditTextKeys.ChangeName, preview.OldName, preview.NewName));

        if (preview.OldArtist != preview.NewArtist)
            lines.Add(Format(localize, AlbumEditTextKeys.ChangeArtist, preview.OldArtist, preview.NewArtist));

        if (preview.OldYear != preview.NewYear)
            lines.Add(Format(localize, AlbumEditTextKeys.ChangeYear, YearText(preview.OldYear), YearText(preview.NewYear)));

        if (preview.OldGenre != preview.NewGenre)
            lines.Add(Format(localize, AlbumEditTextKeys.ChangeGenre, preview.OldGenre, preview.NewGenre));

        return lines;
    }

    /// <summary>Lists the counts and the warnings of an edit, one line each.</summary>
    /// <param name="preview">What the edit would do.</param>
    /// <param name="localize">Gives a resource string from its key.</param>
    /// <returns>The lines.</returns>
    public static IReadOnlyList<string> NoteLines(AlbumEditPreview preview, Func<string, string> localize)
    {
        List<string> lines = [Format(localize, AlbumEditTextKeys.Counts, preview.FileCount)];

        if (preview.CreatesArtist)
            lines.Add(Format(localize, AlbumEditTextKeys.ArtistCreated, preview.NewArtist));

        if (preview.CreatesGenre)
            lines.Add(Format(localize, AlbumEditTextKeys.GenreCreated, preview.NewGenre));

        if (preview.GenreKeptCount > 0)
            lines.Add(Format(localize, AlbumEditTextKeys.GenreKept, preview.GenreKeptCount));

        if (preview.OtherArtistTrackCount > 0)
            lines.Add(Format(localize, AlbumEditTextKeys.OtherArtistKept, preview.OtherArtistTrackCount));

        if (preview.OrphanArtist is not null)
        {
            string key = preview.OrphanArtist.IsFavorite ? AlbumEditTextKeys.OrphanFavoriteArtist : AlbumEditTextKeys.OrphanArtist;

            lines.Add(Format(localize, key, preview.OrphanArtist.Name, preview.OrphanArtist.ListenCount));
        }

        return lines;
    }

    /// <summary>Gives the message shown when another album already carries the name.</summary>
    /// <param name="collision">The other album.</param>
    /// <param name="localize">Gives a resource string from its key.</param>
    /// <returns>The message.</returns>
    public static string CollisionMessage(AlbumCollision collision, Func<string, string> localize)
    {
        return Format(localize, AlbumEditTextKeys.Collision, collision.Name, collision.ArtistName, collision.TrackCount);
    }

    /// <summary>Gives the message of a refused request, the generic one when the code is unknown or has no text.</summary>
    /// <param name="errorCode">The error code of the request.</param>
    /// <param name="localize">Gives a resource string from its key.</param>
    /// <returns>The message.</returns>
    public static string ErrorMessage(string? errorCode, Func<string, string> localize)
    {
        string message = localize(AlbumEditTextKeys.Error(errorCode));

        return string.IsNullOrEmpty(message) ? localize(AlbumEditTextKeys.GenericError) : message;
    }

    private static string YearText(int? year) => year?.ToString(CultureInfo.InvariantCulture) ?? NoYear;

    private static string Format(Func<string, string> localize, string key, params object[] arguments)
    {
        return string.Format(CultureInfo.CurrentCulture, localize(key), arguments);
    }
}