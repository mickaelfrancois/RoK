using Rok.Application.Interfaces.Repositories;

namespace Rok.Application.Features.Artists.Requests;

public class GetArtistByNameRequest(string name) : IRequest<Result<ArtistDto>>
{
    public string Name { get; } = name;
}

public sealed class GetArtistByNameRequestValidator : Validator<GetArtistByNameRequest>
{
    public GetArtistByNameRequestValidator() { Rule(x => x.Name).Required(); }
}


public class GetArtistByNameRequestHandler(IArtistRepository artistRepository) : IRequestHandler<GetArtistByNameRequest, Result<ArtistDto>>
{
    public async Task<Result<ArtistDto>> Handle(GetArtistByNameRequest query, CancellationToken cancellationToken)
    {
        ArtistEntity? artist = await artistRepository.GetByNameAsync(query.Name);
        if (artist == null)
            return Result<ArtistDto>.Fail(NotFoundError.ForEntity("Artist", query.Name));
        else
            return Result<ArtistDto>.Ok(ArtistMapping.ToDto(artist));
    }
}