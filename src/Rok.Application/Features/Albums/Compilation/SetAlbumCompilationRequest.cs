using Rok.Application.Features.Albums.Edit;
using Rok.Application.Features.Artists.Rename;

namespace Rok.Application.Features.Albums.Compilation;

/// <summary>
/// Sets by hand whether an album is a compilation, in the database (origin Manual) and in the tags of its files.
/// </summary>
/// <param name="albumId">Id of the album.</param>
/// <param name="isCompilation">The status asked by the user.</param>
public class SetAlbumCompilationRequest(long albumId, bool isCompilation) : IRequest<Result<AlbumTagWriteOutcome>>
{
    public long AlbumId { get; } = albumId;

    public bool IsCompilation { get; } = isCompilation;

    /// <summary>
    /// Receives the progress of the file writing.
    /// </summary>
    public IProgress<ArtistRenameProgress>? Progress { get; init; }
}