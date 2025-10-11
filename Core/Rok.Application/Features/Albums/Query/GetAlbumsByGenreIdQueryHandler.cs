using Rok.Application.Interfaces;
using Rok.Domain.Interfaces.Entities;

namespace Rok.Application.Features.Albums.Query;

public class GetAlbumsByGenreIdQuery(int genreId) : IQuery<IEnumerable<AlbumDto>>
{
    [RequiredGreaterThanZero]
    public int GenreId { get; } = genreId;
}


public class GetAlbumsByGenreIdQueryHandler(IAlbumRepository _albumRepository) : IQueryHandler<GetAlbumsByGenreIdQuery, IEnumerable<AlbumDto>>
{
    public async Task<IEnumerable<AlbumDto>> HandleAsync(GetAlbumsByGenreIdQuery query, CancellationToken cancellationToken)
    {
        IEnumerable<IAlbumEntity> albums = await _albumRepository.GetByGenreIdAsync(query.GenreId);

        return albums.Select(a => AlbumDtoMapping.Map(a));
    }
}