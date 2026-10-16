using Rok.Application.Features.Artists.Rename;

namespace Rok.Application.Features.Artists.Requests;

/// <summary>
/// Tells what renaming an artist would do, without changing anything.
/// </summary>
/// <param name="artistId">Id of the artist.</param>
/// <param name="newName">Name asked by the user.</param>
public class GetArtistRenamePreviewRequest(long artistId, string newName) : IRequest<Result<ArtistRenamePreview>>
{
    public long ArtistId { get; } = artistId;

    public string NewName { get; } = newName;
}