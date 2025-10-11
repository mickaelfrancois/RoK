using Rok.Application.Interfaces;

namespace Rok.Application.Features.Genres.Query;

public class GetGenreByIdQuery(long id) : IQuery<Result<GenreDto>>
{
    [RequiredGreaterThanZero]
    public long Id { get; } = id;
}


public class GetGenreByIdQueryHandler(IGenreRepository genreRepository) : IQueryHandler<GetGenreByIdQuery, Result<GenreDto>>
{
    public async Task<Result<GenreDto>> HandleAsync(GetGenreByIdQuery query, CancellationToken cancellationToken)
    {
        GenreEntity? genre = await genreRepository.GetByIdAsync(query.Id);
        if (genre == null)
            return Result<GenreDto>.Fail("NotFound", "Genre not found");
        else
            return Result<GenreDto>.Success(GenreDtoMapping.Map(genre));
    }
}