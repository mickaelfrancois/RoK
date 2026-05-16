using Rok.Application.Interfaces.Repositories;

namespace Rok.Application.Features.Genres.Requests;

public class UpdateGenretLastListenRequest(long id) : IRequest<Result<bool>>
{
    public long Id { get; init; } = id;
}

public sealed class UpdateGenretLastListenRequestValidator : Validator<UpdateGenretLastListenRequest>
{
    public UpdateGenretLastListenRequestValidator() { Rule(x => x.Id).GreaterThan(0L); }
}

public class UpdateGenretLastListenRequestHandler(IGenreRepository _genreRepository) : IRequestHandler<UpdateGenretLastListenRequest, Result<bool>>
{
    public async Task<Result<bool>> Handle(UpdateGenretLastListenRequest message, CancellationToken cancellationToken)
    {
        bool result = await _genreRepository.UpdateLastListenAsync(message.Id);

        if (result)
            return Result<bool>.Success(result);
        else
            return Result<bool>.Fail("Failed to update genre last listen status.");
    }
}
