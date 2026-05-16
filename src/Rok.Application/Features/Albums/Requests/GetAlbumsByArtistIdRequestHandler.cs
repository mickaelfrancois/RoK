using Rok.Application.Interfaces.Repositories;
using Rok.Domain.Interfaces.Entities;

namespace Rok.Application.Features.Albums.Requests;

public class GetAlbumsByArtistIdRequest(long artistId) : IRequest<IEnumerable<AlbumDto>>
{
    public long ArtistId { get; } = artistId;
}

public sealed class GetAlbumsByArtistIdRequestValidator : Validator<GetAlbumsByArtistIdRequest>
{
    public GetAlbumsByArtistIdRequestValidator()
    {
        Rule(x => x.ArtistId).GreaterThan(0L);
    }
}


public class GetAlbumsByArtistIdRequestHandler(IAlbumRepository _albumRepository) : IRequestHandler<GetAlbumsByArtistIdRequest, IEnumerable<AlbumDto>>
{
    public async Task<IEnumerable<AlbumDto>> Handle(GetAlbumsByArtistIdRequest query, CancellationToken cancellationToken)
    {
        IEnumerable<IAlbumEntity> albums = await _albumRepository.GetByArtistIdAsync(query.ArtistId);

        return albums.Select(a => AlbumMapping.ToDto(a));
    }
}
