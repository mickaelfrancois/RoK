namespace Rok.Application.Features.Albums.Requests;

/// <summary>
/// Tells whether some files of an album still wait for a tag write left half done by an earlier edit or merge.
/// </summary>
/// <param name="albumId">Id of the album.</param>
public class GetAlbumPendingTagWriteRequest(long albumId) : IRequest<Result<bool>>
{
    public long AlbumId { get; } = albumId;
}