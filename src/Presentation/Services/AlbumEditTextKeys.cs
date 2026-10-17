namespace Rok.Services;

/// <summary>Resource keys of the texts of the album edit.</summary>
public static class AlbumEditTextKeys
{
    /// <summary>Key of the line that shows the old and the new album name. Takes both names.</summary>
    public const string ChangeName = "albumEditChangeName";

    /// <summary>Key of the line that shows the old and the new artist. Takes both names.</summary>
    public const string ChangeArtist = "albumEditChangeArtist";

    /// <summary>Key of the line that shows the old and the new year. Takes both years.</summary>
    public const string ChangeYear = "albumEditChangeYear";

    /// <summary>Key of the line that shows the old and the new genre. Takes both names.</summary>
    public const string ChangeGenre = "albumEditChangeGenre";

    /// <summary>Key of the line that gives the number of files that are written. Takes the number.</summary>
    public const string Counts = "albumEditCounts";

    /// <summary>Key of the line that tells the artist is created. Takes the name.</summary>
    public const string ArtistCreated = "albumEditArtistCreated";

    /// <summary>Key of the line that tells the genre is created. Takes the name.</summary>
    public const string GenreCreated = "albumEditGenreCreated";

    /// <summary>Key of the line that counts the tracks that keep their own genre. Takes the number.</summary>
    public const string GenreKept = "albumEditGenreKept";

    /// <summary>Key of the line that counts the tracks of another artist that keep their artist. Takes the number.</summary>
    public const string OtherArtistKept = "albumEditOtherArtistKept";

    /// <summary>Key of the warning that the old artist keeps no track. Takes the name and the number of listens.</summary>
    public const string OrphanArtist = "albumEditOrphanArtist";

    /// <summary>Key of the warning that the old artist keeps no track and is a favorite. Takes the name and the number of listens.</summary>
    public const string OrphanFavoriteArtist = "albumEditOrphanFavoriteArtist";

    /// <summary>Key of the line shown when an interrupted write is finished.</summary>
    public const string ResumeIntro = "albumEditResumeIntro";

    /// <summary>Key of the message shown when another album already carries the name. Takes the name, the artist and the track count.</summary>
    public const string Collision = "albumEditCollision";

    /// <summary>Key of the header of the list of reasons that block the write.</summary>
    public const string BlockersHeader = "albumEditBlockersHeader";

    /// <summary>Key of the header of the list of files that were not written.</summary>
    public const string FailuresHeader = "albumEditFailuresHeader";

    /// <summary>Key of the text of the button that starts the write.</summary>
    public const string StartButton = "albumEditStart";

    /// <summary>Key of the text of the button that finishes an interrupted write.</summary>
    public const string ResumeButton = "albumEditResume";

    /// <summary>Key of the text of the button that tries again after an incomplete write.</summary>
    public const string RetryButton = "albumEditRetry";

    /// <summary>Key of the message used when no specific one matches.</summary>
    public const string GenericError = "albumEditError_Generic";

    /// <summary>Key of the message shown when the typed name is empty or too long.</summary>
    public const string InvalidName = "albumEditError_InvalidName";

    /// <summary>Key of the message shown when the typed artist is empty or too long.</summary>
    public const string InvalidArtist = "albumEditError_InvalidArtist";

    /// <summary>Key of the message shown when the typed genre is too long.</summary>
    public const string InvalidGenre = "albumEditError_InvalidGenre";

    /// <summary>Key of the message shown when the typed year is not a valid year.</summary>
    public const string InvalidYear = "albumEditError_InvalidYear";

    private static readonly Dictionary<string, string> ErrorKeys = new()
    {
        ["album.edit.disabled"] = "trackEditError_Disabled",
        ["album.edit.no_artist"] = "albumEditError_NoArtist",
        ["album.edit.pending"] = "albumEditError_Pending",
        ["album.edit.compilation_artist"] = "albumEditError_CompilationArtist",
        ["album.edit.artist_pending_rename"] = "albumEditError_ArtistPendingRename",
        ["album.edit.unchanged"] = "albumEditError_Unchanged",
        ["album.edit.genre_unused"] = "albumEditError_GenreUnused",
        ["album.edit.genre_from_artist"] = "albumEditError_GenreFromArtist",
        ["album.rename.collision"] = "albumEditError_Collision",
        ["album.not_found"] = "albumEditError_NotFound"
    };

    /// <summary>Gets every key of the screens of the album edit that the resource files must hold, the blocker texts excluded.</summary>
    public static IReadOnlyCollection<string> AllKeys { get; } =
    [
        ChangeName,
        ChangeArtist,
        ChangeYear,
        ChangeGenre,
        Counts,
        ArtistCreated,
        GenreCreated,
        GenreKept,
        OtherArtistKept,
        OrphanArtist,
        OrphanFavoriteArtist,
        ResumeIntro,
        Collision,
        BlockersHeader,
        FailuresHeader,
        StartButton,
        ResumeButton,
        RetryButton,
        GenericError,
        InvalidName,
        InvalidArtist,
        InvalidGenre,
        InvalidYear,
        .. ErrorKeys.Values.Distinct()
    ];

    /// <summary>Maps an error code of the album edit to the key of its message.</summary>
    /// <param name="errorCode">The error code, for example <c>album.rename.collision</c>.</param>
    /// <returns>The resource key, <see cref="GenericError"/> when the code is unknown.</returns>
    public static string Error(string? errorCode)
    {
        if (errorCode is not null && ErrorKeys.TryGetValue(errorCode, out string? key))
            return key;

        return GenericError;
    }
}