using Rok.Application.Interfaces.Repositories;

namespace Rok.Application.Features.Albums.Requests;

public class GetAllAlbumsRequest : IRequest<IEnumerable<AlbumDto>>
{
}

public class GetAllAlbumsRequestHandler(IAlbumRepository _albumRepository) : IRequestHandler<GetAllAlbumsRequest, IEnumerable<AlbumDto>>
{
    public async Task<IEnumerable<AlbumDto>> Handle(GetAllAlbumsRequest request, CancellationToken cancellationToken)
    {
        IEnumerable<AlbumEntity> albums = await _albumRepository.GetAllAsync();

        return albums.Select(a => AlbumMapping.ToDto(a));
    }
}