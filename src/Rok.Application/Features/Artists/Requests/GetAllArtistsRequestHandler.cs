using Rok.Application.Interfaces.Repositories;

namespace Rok.Application.Features.Artists.Requests;

public class GetAllArtistsRequest : IRequest<IEnumerable<ArtistDto>>
{
    public bool ExcludeArtistsWithoutAlbum { get; set; } = false;
}

public class GetAllArtistsRequestHandler(IArtistRepository _artistRepository) : IRequestHandler<GetAllArtistsRequest, IEnumerable<ArtistDto>>
{
    public async Task<IEnumerable<ArtistDto>> Handle(GetAllArtistsRequest request, CancellationToken cancellationToken)
    {
        IEnumerable<ArtistEntity> artists = await _artistRepository.GetAllAsync();

        if (request.ExcludeArtistsWithoutAlbum)
            artists = artists.Where(a => a.AlbumCount + a.LiveCount + a.BestofCount > 0);

        return artists.Select(a => ArtistMapping.ToDto(a));
    }
}