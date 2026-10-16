using Rok.Application.Tag;

namespace Rok.Application.Features.Artists.Rename;

/// <summary>
/// Result of an artist rename.
/// </summary>
/// <param name="State">Final state.</param>
/// <param name="Name">Name the artist carries in the database after the rename.</param>
/// <param name="Blockers">Reasons the rename was refused, when <see cref="EArtistRenameState.Blocked"/>.</param>
/// <param name="Failures">Files that could not be written, when <see cref="EArtistRenameState.Partial"/>.</param>
/// <param name="ChangedPlaylistIds">Playlists whose picture or criteria were rewritten.</param>
public sealed record ArtistRenameOutcome(
    EArtistRenameState State,
    string Name,
    IReadOnlyList<ArtistRenameBlocker> Blockers,
    IReadOnlyList<ArtistRenameFailure> Failures,
    IReadOnlyList<long> ChangedPlaylistIds);

/// <summary>
/// A file the new artist name could not be written to.
/// </summary>
/// <param name="FilePath">Full path of the audio file.</param>
/// <param name="Reason">Why the write failed.</param>
public sealed record ArtistRenameFailure(string FilePath, ETagWriteReason Reason);

/// <summary>
/// Final state of an artist rename.
/// </summary>
public enum EArtistRenameState
{
    /// <summary>The database and every file carry the new name.</summary>
    Completed,

    /// <summary>Refused before anything was changed.</summary>
    Blocked,

    /// <summary>The database carries the new name but some files do not. The pending-rename marker is kept.</summary>
    Partial,

    /// <summary>The database update failed and was rolled back.</summary>
    Failed
}