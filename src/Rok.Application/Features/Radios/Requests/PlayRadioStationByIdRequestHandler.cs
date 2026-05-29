using CleanArch.DevKit.Mediator;
using CleanArch.DevKit.Mediator.Results;
using Rok.Application.Errors;
using Rok.Application.Interfaces.Repositories;
using Rok.Application.Mapping;
using Rok.Application.Player;

namespace Rok.Application.Features.Radios.Requests;

public class PlayRadioStationByIdRequestHandler(
    IRadioStationRepository repository,
    IPlayerService playerService,
    TimeProvider timeProvider)
    : IRequestHandler<PlayRadioStationByIdRequest, Result<bool>>
{
    public async Task<Result<bool>> Handle(PlayRadioStationByIdRequest message, CancellationToken cancellationToken)
    {
        RadioStationEntity? entity = await repository.GetByIdAsync(message.Id, cancellationToken);

        if (entity is null)
            return Result<bool>.Fail(new NotFoundError("radio.not_found", $"Radio station {message.Id} was not found."));

        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        entity.LastListen = utcNow;

        playerService.PlayRadioStation(entity.ToDto());
        await repository.TouchLastListenAsync(entity.Id, utcNow, cancellationToken);

        return Result<bool>.Ok(true);
    }
}