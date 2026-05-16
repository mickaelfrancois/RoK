using Rok.Application.Interfaces.Repositories;
using Rok.Domain.Interfaces.Entities;

namespace Rok.Application.Features.Albums.Requests;

public class GetAlbumsByGenreIdRequest(long genreId) : IRequest<IEnumerable<AlbumDto>>
{
    public long GenreId { get; } = genreId;
}

public sealed class GetAlbumsByGenreIdRequestValidator : Validator<GetAlbumsByGenreIdRequest>
{
    public GetAlbumsByGenreIdRequestValidator()
    {
        Rule(x => x.GenreId).GreaterThan(0L);
    }
}


public class GetAlbumsByGenreIdRequestHandler(IAlbumRepository _albumRepository) : IRequestHandler<GetAlbumsByGenreIdRequest, IEnumerable<AlbumDto>>
{
    public async Task<IEnumerable<AlbumDto>> Handle(GetAlbumsByGenreIdRequest query, CancellationToken cancellationToken)
    {
        IEnumerable<IAlbumEntity> albums = await _albumRepository.GetByGenreIdAsync(query.GenreId);

        return albums.Select(a => AlbumMapping.ToDto(a));
    }
}