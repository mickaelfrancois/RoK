using Rok.Application.Interfaces;
using Rok.Domain.Interfaces.Entities;

namespace Rok.Application.Features.Albums.Query;

public class GetAlbumsByArtistIdQuery(long artistId) : IQuery<IEnumerable<AlbumDto>>
{
    [RequiredGreaterThanZero]
    public long ArtistId { get; } = artistId;
}


public class GetAlbumsByArtistIdQueryHandler(IAlbumRepository _albumRepository) : IQueryHandler<GetAlbumsByArtistIdQuery, IEnumerable<AlbumDto>>
{
    public async Task<IEnumerable<AlbumDto>> HandleAsync(GetAlbumsByArtistIdQuery query, CancellationToken cancellationToken)
    {
        IEnumerable<IAlbumEntity> albums = await _albumRepository.GetByArtistIdAsync(query.ArtistId);

        return albums.Select(a => AlbumMapping.ToDto(a));
    }
}