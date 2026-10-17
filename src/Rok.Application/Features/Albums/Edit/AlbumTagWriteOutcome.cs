using Rok.Application.Features.Artists.Rename;

namespace Rok.Application.Features.Albums.Edit;

/// <summary>
/// Result of an album edit, an album merge or a resume of an interrupted write.
/// </summary>
/// <param name="State">Final state.</param>
/// <param name="AlbumId">Album that carries the edited values.</param>
/// <param name="Blockers">Reasons the work was refused, when <see cref="EArtistRenameState.Blocked"/>.</param>
/// <param name="Failures">Files that could not be written, when <see cref="EArtistRenameState.Partial"/>.</param>
/// <param name="UpdatedArtistIds">Artists whose counters changed.</param>
/// <param name="DeletedAlbumIds">Albums that disappeared.</param>
public sealed record AlbumTagWriteOutcome(
    EArtistRenameState State,
    long AlbumId,
    IReadOnlyList<ArtistRenameBlocker> Blockers,
    IReadOnlyList<ArtistRenameFailure> Failures,
    IReadOnlyList<long> UpdatedArtistIds,
    IReadOnlyList<long> DeletedAlbumIds);