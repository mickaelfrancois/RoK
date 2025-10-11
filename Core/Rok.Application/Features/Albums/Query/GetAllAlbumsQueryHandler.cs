using Rok.Application.Interfaces;

namespace Rok.Application.Features.Albums.Query;

public class GetAllAlbumsQuery : IQuery<IEnumerable<AlbumDto>>
{
}

public class GetAllAlbumsQueryHandler(IAlbumRepository _albumRepository) : IQueryHandler<GetAllAlbumsQuery, IEnumerable<AlbumDto>>
{
    public async Task<IEnumerable<AlbumDto>> HandleAsync(GetAllAlbumsQuery request, CancellationToken cancellationToken)
    {
        IEnumerable<AlbumEntity> albums = await _albumRepository.GetAllAsync();

        return albums.Select(a => AlbumDtoMapping.Map(a));
    }
}
