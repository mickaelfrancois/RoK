using CleanArch.DevKit.Mediator;
using CleanArch.DevKit.Mediator.Results;
using Rok.Application.Dto;
using Rok.Application.Errors;
using Rok.Application.Features.Radios.Services;

namespace Rok.Application.Features.Radios.Requests;

public sealed class SearchRadioStationsRequestHandler(IRadioBrowserClient client)
    : IRequestHandler<SearchRadioStationsRequest, Result<IReadOnlyList<RadioSearchResultDto>>>
{
    public async Task<Result<IReadOnlyList<RadioSearchResultDto>>> Handle(
        SearchRadioStationsRequest message,
        CancellationToken cancellationToken)
    {
        try
        {
            IReadOnlyList<RadioSearchResultDto> results =
                await client.SearchByNameAsync(message.Query, message.Limit, cancellationToken);

            return Result<IReadOnlyList<RadioSearchResultDto>>.Ok(results);
        }
        catch (HttpRequestException ex)
        {
            return Result<IReadOnlyList<RadioSearchResultDto>>.Fail(
                new OperationError("radio.search_failed", ex.Message));
        }
        catch (TaskCanceledException)
        {
            return Result<IReadOnlyList<RadioSearchResultDto>>.Fail(
                new OperationError("radio.search_timeout", "Radio search timed out."));
        }
    }
}
