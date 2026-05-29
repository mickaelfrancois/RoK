using CleanArch.DevKit.Mediator;
using CleanArch.DevKit.Mediator.Results;
using CleanArch.DevKit.Mediator.Validation;
using Rok.Application.Dto;

namespace Rok.Application.Features.Radios.Requests;

public sealed class SearchRadioStationsRequest : IRequest<Result<IReadOnlyList<RadioSearchResultDto>>>
{
    public string Query { get; set; } = string.Empty;

    public int Limit { get; set; } = 50;
}

public sealed class SearchRadioStationsRequestValidator : Validator<SearchRadioStationsRequest>
{
    public SearchRadioStationsRequestValidator()
    {
        Rule(x => x.Query).Required().MinLength(2).MaxLength(100);
        Rule(x => x.Limit).Must(l => l is > 0 and <= 200).Message("Limit must be between 1 and 200.");
    }
}