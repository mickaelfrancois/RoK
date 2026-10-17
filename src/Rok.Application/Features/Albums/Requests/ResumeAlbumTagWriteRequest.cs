using Rok.Application.Features.Albums.Edit;
using Rok.Application.Features.Artists.Rename;

namespace Rok.Application.Features.Albums.Requests;

/// <summary>
/// Writes again, from the database values, the files of an album that an earlier edit or merge left half done.
/// </summary>
/// <param name="albumId">Id of the album.</param>
public class ResumeAlbumTagWriteRequest(long albumId) : IRequest<Result<AlbumTagWriteOutcome>>
{
    public long AlbumId { get; } = albumId;

    /// <summary>
    /// Receives the progress of the file writing.
    /// </summary>
    public IProgress<ArtistRenameProgress>? Progress { get; init; }
}