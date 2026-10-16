namespace Rok.Services;

/// <summary>Resource keys of the texts of the artist merge.</summary>
public static class ArtistMergeTextKeys
{
    /// <summary>Key of the sentence shown when a rename collides with an existing artist. Takes the name, the album count and the track count.</summary>
    public const string Collision = "artistMergeCollision";

    /// <summary>Key of the line "merged artist into kept artist". Takes both names.</summary>
    public const string Summary = "artistMergeSummary";

    /// <summary>Key of the line that gives the number of files and albums. Takes both numbers.</summary>
    public const string Counts = "artistMergeCounts";

    /// <summary>Key of the header of the list of albums that become one.</summary>
    public const string AlbumsHeader = "artistMergeAlbumsHeader";

    /// <summary>Key of one merged album. Takes the album name and the number of albums that become one.</summary>
    public const string AlbumLine = "artistMergeAlbumLine";

    /// <summary>Key of the header of the list of fields that differ.</summary>
    public const string ConflictsHeader = "artistMergeConflictsHeader";

    /// <summary>Key of one field that differs. Takes the field, the kept value and the lost value.</summary>
    public const string ConflictLine = "artistMergeConflictLine";

    /// <summary>Key of the header of the list of smart playlists that can change.</summary>
    public const string PlaylistsHeader = "artistMergePlaylistsHeader";

    /// <summary>Key of the note added to a smart playlist whose criteria are rewritten.</summary>
    public const string PlaylistRewritten = "artistMergePlaylistRewritten";

    /// <summary>Key of the line that says the picture of the kept artist stays. Takes the name.</summary>
    public const string PictureTarget = "artistMergePictureTarget";

    /// <summary>Key of the line that says the picture of the merged artist is used. Takes the name.</summary>
    public const string PictureSource = "artistMergePictureSource";

    /// <summary>Key of the header of the list of reasons that block the merge.</summary>
    public const string BlockersHeader = "artistMergeBlockersHeader";

    /// <summary>Key of the header of the list of files that still carry the old name.</summary>
    public const string FailuresHeader = "artistMergeFailuresHeader";

    /// <summary>Key of the header of the choice of the artist that stays.</summary>
    public const string KeepHeader = "artistMergeKeepHeader";

    /// <summary>Key of the placeholder of the box that searches the other artist.</summary>
    public const string OtherPlaceholder = "artistMergeOtherPlaceholder";

    /// <summary>Key of the text of the button that starts the merge.</summary>
    public const string StartButton = "artistMergeStart";

    /// <summary>Key of the text of the button that finishes an incomplete merge.</summary>
    public const string ResumeButton = "artistMergeResume";

    /// <summary>Key of the text of the button that leaves the dialog before the merge.</summary>
    public const string CancelButton = "artistMergeCancel";

    /// <summary>Key of the text of the button that leaves the dialog after an attempt.</summary>
    public const string CloseButton = "artistMergeClose";

    /// <summary>Key of the message used when no specific one matches.</summary>
    public const string GenericError = "artistMergeError_Generic";

    private static readonly Dictionary<string, string> ErrorKeys = new()
    {
        ["artist.merge.disabled"] = "trackEditError_Disabled",
        ["artist.merge.same_artist"] = "artistMergeError_SameArtist",
        ["artist.merge.pending_rename"] = "artistMergeError_PendingRename",
        ["artist.not_found"] = "artistMergeError_NotFound"
    };

    /// <summary>Gets every key of the screens of the merge that the resource files must hold.</summary>
    public static IReadOnlyCollection<string> AllKeys { get; } =
    [
        Collision,
        Summary,
        Counts,
        AlbumsHeader,
        AlbumLine,
        ConflictsHeader,
        ConflictLine,
        PlaylistsHeader,
        PlaylistRewritten,
        PictureTarget,
        PictureSource,
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

    /// <summary>Maps an error code of the merge to the key of its message.</summary>
    /// <param name="errorCode">The error code, for example <c>artist.merge.same_artist</c>.</param>
    /// <returns>The resource key, <see cref="GenericError"/> when the code is unknown.</returns>
    public static string Error(string? errorCode)
    {
        if (errorCode is not null && ErrorKeys.TryGetValue(errorCode, out string? key))
            return key;

        return GenericError;
    }
}