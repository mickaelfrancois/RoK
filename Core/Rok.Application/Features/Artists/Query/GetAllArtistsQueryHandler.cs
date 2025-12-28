using Rok.Application.Interfaces;

namespace Rok.Application.Features.Artists.Query;

public class GetAllArtistsQuery : IQuery<IEnumerable<ArtistDto>>
{
    public bool ExcludeArtistsWithoutAlbum { get; set; } = false;
}

public class GetAllArtistsQueryHandler(IArtistRepository _artistRepository) : IQueryHandler<GetAllArtistsQuery, IEnumerable<ArtistDto>>
{
    public async Task<IEnumerable<ArtistDto>> HandleAsync(GetAllArtistsQuery request, CancellationToken cancellationToken)
    {
        IEnumerable<ArtistEntity> artists = await _artistRepository.GetAllAsync();

        if (request.ExcludeArtistsWithoutAlbum)
            artists = artists.Where(a => a.AlbumCount + a.LiveCount + a.BestofCount > 0);

        return artists.Select(a => ArtistMapping.ToDto(a));
    }
}
