using Rok.Application.Interfaces;

namespace Rok.Application.Features.Artists.Query;

public class GetArtistByIdQuery(long id) : IQuery<Result<ArtistDto>>
{
    [RequiredGreaterThanZero]
    public long Id { get; } = id;
}


public class GetArtistByIdQueryHandler(IArtistRepository artistRepository) : IQueryHandler<GetArtistByIdQuery, Result<ArtistDto>>
{
    public async Task<Result<ArtistDto>> HandleAsync(GetArtistByIdQuery query, CancellationToken cancellationToken)
    {
        ArtistEntity? artist = await artistRepository.GetByIdAsync(query.Id);
        if (artist == null)
            return Result<ArtistDto>.Fail("NotFound", "Artist not found");
        else
            return Result<ArtistDto>.Success(ArtistMapping.ToDto(artist));
    }
}