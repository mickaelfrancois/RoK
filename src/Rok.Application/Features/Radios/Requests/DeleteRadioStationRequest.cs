using CleanArch.DevKit.Mediator;
using CleanArch.DevKit.Mediator.Results;

namespace Rok.Application.Features.Radios.Requests;

public class DeleteRadioStationRequest : IRequest<Result<bool>>
{
    public long Id { get; set; }
}