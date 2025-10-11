using Rok.Application.Interfaces;

namespace Rok.Application.Features.Artists.Query;

public class GetArtistByNameQuery(string name) : IQuery<Result<ArtistDto>>
{
    [Required]
    public string Name { get; } = name;
}


public class GetArtistByNameQueryHandler(IArtistRepository artistRepository) : IQueryHandler<GetArtistByNameQuery, Result<ArtistDto>>
{
    public async Task<Result<ArtistDto>> HandleAsync(GetArtistByNameQuery query, CancellationToken cancellationToken)
    {
        ArtistEntity? artist = await artistRepository.GetByNameAsync(query.Name);
        if (artist == null)
            return Result<ArtistDto>.Fail("NotFound", "Artist not found");
        else
            return Result<ArtistDto>.Success(ArtistDtoMapping.Map(artist));
    }
}