using Rok.Application.Interfaces.Repositories;

namespace Rok.Application.Features.Genres.Requests;

public class GetGenreByIdRequest(long id) : IRequest<Result<GenreDto>>
{
    public long Id { get; } = id;
}

public sealed class GetGenreByIdRequestValidator : Validator<GetGenreByIdRequest>
{
    public GetGenreByIdRequestValidator() { Rule(x => x.Id).GreaterThan(0L); }
}


public class GetGenreByIdRequestHandler(IGenreRepository genreRepository) : IRequestHandler<GetGenreByIdRequest, Result<GenreDto>>
{
    public async Task<Result<GenreDto>> Handle(GetGenreByIdRequest query, CancellationToken cancellationToken)
    {
        GenreEntity? genre = await genreRepository.GetByIdAsync(query.Id);
        if (genre == null)
            return Result<GenreDto>.Fail("NotFound", "Genre not found");
        else
            return Result<GenreDto>.Success(GenreDtoMapping.Map(genre));
    }
}
