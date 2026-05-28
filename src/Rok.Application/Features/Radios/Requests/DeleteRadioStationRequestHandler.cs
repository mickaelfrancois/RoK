using CleanArch.DevKit.Mediator;
using CleanArch.DevKit.Mediator.Results;
using Rok.Application.Errors;
using Rok.Application.Interfaces.Repositories;

namespace Rok.Application.Features.Radios.Requests;

public class DeleteRadioStationRequestHandler(IRadioStationRepository repository)
    : IRequestHandler<DeleteRadioStationRequest, Result<bool>>
{
    public async Task<Result<bool>> Handle(DeleteRadioStationRequest message, CancellationToken cancellationToken)
    {
        RadioStationEntity? existing = await repository.GetByIdAsync(message.Id, cancellationToken);

        if (existing is null)
            return Result<bool>.Fail(new NotFoundError("radio.not_found", $"Radio station {message.Id} was not found."));

        await repository.DeleteAsync(message.Id, cancellationToken);
        return Result<bool>.Ok(true);
    }
}
