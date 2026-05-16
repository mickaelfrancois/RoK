using Rok.Application.Interfaces.Repositories;

namespace Rok.Application.Features.Genres.Requests;

public class GetAllGenresRequest : IRequest<IEnumerable<GenreDto>>
{
}

public class GetAllGenresRequestHandler(IGenreRepository _genreRepository) : IRequestHandler<GetAllGenresRequest, IEnumerable<GenreDto>>
{
    public async Task<IEnumerable<GenreDto>> Handle(GetAllGenresRequest request, CancellationToken cancellationToken)
    {
        IEnumerable<GenreEntity> genres = await _genreRepository.GetAllAsync();

        return genres.Select(a => GenreDtoMapping.Map(a));
    }
}