using Rok.Application.Interfaces.Repositories;

namespace Rok.Application.Features.Albums.Requests;

public class GetAlbumByIdRequest(long id) : IRequest<Result<AlbumDto>>
{
    public long Id { get; } = id;
}

public sealed class GetAlbumByIdRequestValidator : Validator<GetAlbumByIdRequest>
{
    public GetAlbumByIdRequestValidator()
    {
        Rule(x => x.Id).GreaterThan(0L);
    }
}


public class GetAlbumByIdRequestHandler(IAlbumRepository albumRepository) : IRequestHandler<GetAlbumByIdRequest, Result<AlbumDto>>
{
    public async Task<Result<AlbumDto>> Handle(GetAlbumByIdRequest query, CancellationToken cancellationToken)
    {
        AlbumEntity? album = await albumRepository.GetByIdAsync(query.Id);
        if (album == null)
            return Result<AlbumDto>.Fail(NotFoundError.ForEntity("Album", query.Id));
        else
            return Result<AlbumDto>.Ok(AlbumMapping.ToDto(album));
    }
}