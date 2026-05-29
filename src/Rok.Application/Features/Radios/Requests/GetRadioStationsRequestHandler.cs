using CleanArch.DevKit.Mediator;
using CleanArch.DevKit.Mediator.Results;
using Rok.Application.Dto;
using Rok.Application.Interfaces.Repositories;
using Rok.Application.Mapping;

namespace Rok.Application.Features.Radios.Requests;

public class GetRadioStationsRequestHandler(IRadioStationRepository repository)
    : IRequestHandler<GetRadioStationsRequest, Result<IReadOnlyList<RadioStationDto>>>
{
    public async Task<Result<IReadOnlyList<RadioStationDto>>> Handle(GetRadioStationsRequest message, CancellationToken cancellationToken)
    {
        IReadOnlyList<RadioStationEntity> entities = await repository.ListAsync(cancellationToken);
        IReadOnlyList<RadioStationDto> dtos = entities.Select(e => e.ToDto()).ToList();
        return Result<IReadOnlyList<RadioStationDto>>.Ok(dtos);
    }
}