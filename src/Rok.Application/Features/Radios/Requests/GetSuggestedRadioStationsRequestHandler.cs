using CleanArch.DevKit.Mediator;
using CleanArch.DevKit.Mediator.Results;
using Microsoft.Extensions.Logging;
using Rok.Application.Dto;
using Rok.Application.Features.Radios.Services;

namespace Rok.Application.Features.Radios.Requests;

public sealed class GetSuggestedRadioStationsRequestHandler(IRadioBrowserClient client, ILogger<GetSuggestedRadioStationsRequestHandler> logger)
    : IRequestHandler<GetSuggestedRadioStationsRequest, Result<RadioSuggestionsDto>>
{
    private const int MaxLimit = 100;

    public async Task<Result<RadioSuggestionsDto>> Handle(GetSuggestedRadioStationsRequest message, CancellationToken cancellationToken)
    {
        int limit = Math.Clamp(message.Limit, 1, MaxLimit);
        string? countryCode = NormalizeCountryCode(message.CountryCode);

        if (countryCode is not null)
        {
            IReadOnlyList<RadioSearchResultDto>? countryStations = await TryFetchAsync(
                () => client.GetTopByCountryAsync(countryCode, limit, cancellationToken), "country", cancellationToken);

            if (countryStations is { Count: > 0 })
                return Result<RadioSuggestionsDto>.Ok(new RadioSuggestionsDto(RadioSuggestionSource.Country, countryStations));
        }

        IReadOnlyList<RadioSearchResultDto>? worldStations = await TryFetchAsync(
            () => client.GetTopWorldwideAsync(limit, cancellationToken), "worldwide", cancellationToken);

        if (worldStations is { Count: > 0 })
            return Result<RadioSuggestionsDto>.Ok(new RadioSuggestionsDto(RadioSuggestionSource.Worldwide, worldStations));

        return Result<RadioSuggestionsDto>.Ok(new RadioSuggestionsDto(RadioSuggestionSource.None, []));
    }

    internal static string? NormalizeCountryCode(string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
            return null;

        string code = countryCode.Trim().ToUpperInvariant();

        if (code.Length != 2 || !code.All(char.IsAsciiLetterUpper))
            return null;

        if (code is "ZZ" or "XX")
            return null;

        return code;
    }

    private async Task<IReadOnlyList<RadioSearchResultDto>?> TryFetchAsync(
        Func<Task<IReadOnlyList<RadioSearchResultDto>>> fetch, string scope, CancellationToken cancellationToken)
    {
        try
        {
            return await fetch();
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Radio suggestions ({Scope}) request failed.", scope);
            return null;
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Radio suggestions ({Scope}) request timed out.", scope);
            return null;
        }
    }
}