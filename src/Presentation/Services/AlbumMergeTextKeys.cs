namespace Rok.Services;

/// <summary>Resource keys of the texts of the album merge.</summary>
public static class AlbumMergeTextKeys
{
    /// <summary>Key of the sentence shown when a rename collides with an existing album. Takes the name, the artist and the track count.</summary>
    public const string Collision = "albumMergeCollision";

    /// <summary>Key of the line "merged album into kept album". Takes both names and both artists.</summary>
    public const string Summary = "albumMergeSummary";

    /// <summary>Key of the line that says the tracks of the merged album change artist. Takes both artists.</summary>
    public const string ArtistChange = "albumMergeArtistChange";

    /// <summary>Key of the line that says the two albums sit in different folders.</summary>
    public const string SeparateFolders = "albumMergeSeparateFolders";

    /// <summary>Key of the header of the list of fields that differ.</summary>
    public const string ConflictsHeader = "albumMergeConflictsHeader";

    /// <summary>Key of one field that differs. Takes the field, the kept value and the lost value.</summary>
    public const string ConflictLine = "albumMergeConflictLine";

    /// <summary>Key of the header of the list of tracks that share a disc and a number.</summary>
    public const string DiscConflictsHeader = "albumMergeDiscConflictsHeader";

    /// <summary>Key of one pair of tracks that share a disc and a number. Takes the disc, the number, the merged title and the kept title.</summary>
    public const string DiscConflictLine = "albumMergeDiscConflictLine";

    /// <summary>Key of the check box that shifts the discs. Takes the number of discs added.</summary>
    public const string ShiftDiscs = "albumMergeShiftDiscs";

    /// <summary>Key of the header of the list of reasons that block the merge.</summary>
    public const string BlockersHeader = "albumMergeBlockersHeader";

    /// <summary>Key of the header of the list of files that could not be updated.</summary>
    public const string FailuresHeader = "albumMergeFailuresHeader";

    /// <summary>Key of the header of the choice of the album that stays.</summary>
    public const string KeepHeader = "albumMergeKeepHeader";

    /// <summary>Key of the placeholder of the box that searches the other album.</summary>
    public const string OtherPlaceholder = "albumMergeOtherPlaceholder";

    /// <summary>Key of the text of the button that starts the merge.</summary>
    public const string StartButton = "albumMergeStart";

    /// <summary>Key of the text of the button that finishes an incomplete merge.</summary>
    public const string ResumeButton = "albumMergeResume";

    /// <summary>Key of the text of the button that leaves the dialog before the merge.</summary>
    public const string CancelButton = "albumMergeCancel";

    /// <summary>Key of the text of the button that leaves the dialog after an attempt.</summary>
    public const string CloseButton = "albumMergeClose";

    /// <summary>Key of the message used when no specific one matches.</summary>
    public const string GenericError = "albumMergeError_Generic";

    private static readonly Dictionary<string, string> ErrorKeys = new()
    {
        ["album.merge.disabled"] = "trackEditError_Disabled",
        ["album.merge.same_album"] = "albumMergeError_SameAlbum",
        ["album.merge.compilation"] = "albumMergeError_Compilation",
        ["album.merge.no_artist"] = "albumMergeError_NoArtist",
        ["album.merge.pending"] = "albumMergeError_Pending",
        ["album.merge.artist_pending_rename"] = "albumMergeError_ArtistPendingRename",
        ["album.merge.conflicts"] = "albumMergeError_Conflicts",
        ["album.not_found"] = "albumMergeError_NotFound"
    };

    /// <summary>Gets every key of the screens of the album merge that the resource files must hold.</summary>
    public static IReadOnlyCollection<string> AllKeys { get; } =
    [
        Collision,
        Summary,
        ArtistChange,
        SeparateFolders,
        ConflictsHeader,
        ConflictLine,
        DiscConflictsHeader,
        DiscConflictLine,
        ShiftDiscs,
        BlockersHeader,
        FailuresHeader,
        KeepHeader,
        OtherPlaceholder,
        StartButton,
        ResumeButton,
        CancelButton,
        CloseButton,
        GenericError,
        .. ErrorKeys.Values.Distinct()
    ];

    /// <summary>Maps an error code of the album merge to the key of its message.</summary>
    /// <param name="errorCode">The error code, for example <c>album.merge.same_album</c>.</param>
    /// <returns>The resource key, <see cref="GenericError"/> when the code is unknown.</returns>
    public static string Error(string? errorCode)
    {
        if (errorCode is not null && ErrorKeys.TryGetValue(errorCode, out string? key))
            return key;

        return GenericError;
    }
}