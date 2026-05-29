using CleanArch.DevKit.Mediator;
using CleanArch.DevKit.Mediator.Results;
using Rok.Application.Dto;

namespace Rok.Application.Features.Radios.Requests;

public class GetRadioStationsRequest : IRequest<Result<IReadOnlyList<RadioStationDto>>> { }