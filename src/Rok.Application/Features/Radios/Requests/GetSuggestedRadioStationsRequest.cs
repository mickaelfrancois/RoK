using CleanArch.DevKit.Mediator;
using CleanArch.DevKit.Mediator.Results;
using Rok.Application.Dto;

namespace Rok.Application.Features.Radios.Requests;

/// <summary>
/// Loads popular stations for the given country, falling back to worldwide stations.
/// An invalid or missing country code is a fallback case, not a validation error.
/// </summary>
public sealed class GetSuggestedRadioStationsRequest : IRequest<Result<RadioSuggestionsDto>>
{
    public string? CountryCode { get; set; }

    public int Limit { get; set; } = 24;
}