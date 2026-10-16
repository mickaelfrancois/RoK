using Rok.Application.Features.Artists.Rename;

namespace Rok.Application.Features.Artists.Merge;

/// <summary>
/// Result of an artist merge.
/// </summary>
/// <param name="State">Final state.</param>
/// <param name="ArtistId">Id of the kept artist.</param>
/// <param name="Name">Name of the kept artist.</param>
/// <param name="Blockers">Reasons the merge was refused, when <see cref="EArtistRenameState.Blocked"/>.</param>
/// <param name="Failures">Files that could not be written, when <see cref="EArtistRenameState.Partial"/>.</param>
/// <param name="ChangedPlaylistIds">Playlists whose picture or criteria were rewritten.</param>
/// <param name="DeletedArtistId">Id of the artist that disappeared; 0 when nothing was merged.</param>
/// <param name="DeletedAlbumIds">Albums folded into another album and deleted.</param>
/// <param name="UpdatedAlbumIds">Albums that now belong to the kept artist, survivors included.</param>
public sealed record ArtistMergeOutcome(
    EArtistRenameState State,
    long ArtistId,
    string Name,
    IReadOnlyList<ArtistRenameBlocker> Blockers,
    IReadOnlyList<ArtistRenameFailure> Failures,
    IReadOnlyList<long> ChangedPlaylistIds,
    long DeletedArtistId,
    IReadOnlyList<long> DeletedAlbumIds,
    IReadOnlyList<long> UpdatedAlbumIds);