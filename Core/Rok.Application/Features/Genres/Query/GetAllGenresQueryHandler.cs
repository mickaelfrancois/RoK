using Rok.Application.Interfaces;

namespace Rok.Application.Features.Genres.Query;

public class GetAllGenresQuery : IQuery<IEnumerable<GenreDto>>
{
}

public class GetAllGenresQueryHandler(IGenreRepository _genreRepository) : IQueryHandler<GetAllGenresQuery, IEnumerable<GenreDto>>
{
    public async Task<IEnumerable<GenreDto>> HandleAsync(GetAllGenresQuery request, CancellationToken cancellationToken)
    {
        IEnumerable<GenreEntity> genres = await _genreRepository.GetAllAsync();

        return genres.Select(a => GenreDtoMapping.Map(a));
    }
}
