using Rok.Application.Interfaces;

namespace Rok.Application.Features.Albums.Query;

public class GetAlbumByIdQuery(long id) : IQuery<Result<AlbumDto>>
{
    [RequiredGreaterThanZero]
    public long Id { get; } = id;
}


public class GetAlbumByIdQueryHandler(IAlbumRepository albumRepository) : IQueryHandler<GetAlbumByIdQuery, Result<AlbumDto>>
{
    public async Task<Result<AlbumDto>> HandleAsync(GetAlbumByIdQuery query, CancellationToken cancellationToken)
    {
        AlbumEntity? album = await albumRepository.GetByIdAsync(query.Id);
        if (album == null)
            return Result<AlbumDto>.Fail("NotFound", "Album not found");
        else
            return Result<AlbumDto>.Success(AlbumMapping.ToDto(album));
    }
}