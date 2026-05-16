using Rok.Application.Interfaces.Repositories;

namespace Rok.Application.Features.Artists.Requests;

public class GetArtistByIdRequest(long id) : IRequest<Result<ArtistDto>>
{
    public long Id { get; } = id;
}

public sealed class GetArtistByIdRequestValidator : Validator<GetArtistByIdRequest>
{
    public GetArtistByIdRequestValidator() { Rule(x => x.Id).GreaterThan(0L); }
}


public class GetArtistByIdRequestHandler(IArtistRepository artistRepository) : IRequestHandler<GetArtistByIdRequest, Result<ArtistDto>>
{
    public async Task<Result<ArtistDto>> Handle(GetArtistByIdRequest query, CancellationToken cancellationToken)
    {
        ArtistEntity? artist = await artistRepository.GetByIdAsync(query.Id);
        if (artist == null)
            return Result<ArtistDto>.Fail("NotFound", "Artist not found");
        else
            return Result<ArtistDto>.Success(ArtistMapping.ToDto(artist));
    }
}
