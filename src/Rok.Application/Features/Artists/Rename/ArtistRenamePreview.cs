namespace Rok.Application.Features.Artists.Rename;

/// <summary>
/// What an artist rename would do, shown before the user confirms.
/// </summary>
/// <param name="ArtistId">Id of the artist; it never changes.</param>
/// <param name="OldName">Name the files are renamed from. For a resume it is the pending-rename marker.</param>
/// <param name="NewName">Normalized name the artist will carry.</param>
/// <param name="FileCount">Number of track files of the artist.</param>
/// <param name="AlbumCount">Number of distinct albums of these files.</param>
/// <param name="Blockers">Reasons the rename cannot start. Empty when it can.</param>
/// <param name="Collision">Another artist that already carries this name, null otherwise.</param>
/// <param name="IsResume">True when the rename was started earlier and some files still carry the old name.</param>
public sealed record ArtistRenamePreview(
    long ArtistId,
    string OldName,
    string NewName,
    int FileCount,
    int AlbumCount,
    IReadOnlyList<ArtistRenameBlocker> Blockers,
    ArtistRenameCollision? Collision,
    bool IsResume);

/// <summary>
/// Another artist the new name would be merged with.
/// </summary>
/// <param name="Name">Name of the existing artist.</param>
/// <param name="AlbumCount">Number of albums of the existing artist.</param>
/// <param name="TrackCount">Number of tracks of the existing artist.</param>
public sealed record ArtistRenameCollision(string Name, int AlbumCount, int TrackCount);

/// <summary>
/// A reason that stops the whole rename before anything is written.
/// </summary>
/// <param name="FilePath">File or folder concerned, empty when the reason is not about a file.</param>
/// <param name="Reason">Why the rename is blocked.</param>
/// <param name="Detail">Extra information, for example the artist an out-of-sync file carries.</param>
public sealed record ArtistRenameBlocker(string FilePath, EArtistRenameBlock Reason, string? Detail);

/// <summary>
/// Why an artist rename is blocked.
/// </summary>
public enum EArtistRenameBlock
{
    NotFound,
    ReadOnly,
    CloudPlaceholder,
    UnsupportedFormat,
    Locked,
    InsufficientDiskSpace,
    UnsupportedTagVersion,
    Playing,
    ImportInProgress,
    OutOfSync,
    PictureFolder
}