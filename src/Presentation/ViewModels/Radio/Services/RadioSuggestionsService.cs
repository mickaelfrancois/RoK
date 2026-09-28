using Rok.Application.Errors;
using Rok.Application.Features.Radios.Requests;

namespace Rok.ViewModels.Radio.Services;

/// <summary>
/// Loads, plays and saves the radio stations suggested on an empty radio page.
/// </summary>
public sealed class RadioSuggestionsService(IMediator mediator, ITelemetryClient telemetryClient, IRegionProvider regionProvider, RadioPictureService pictureService)
{
    private const string TelemetryType = "Radio";

    private static readonly RadioSuggestionsDto NoSuggestions = new(RadioSuggestionSource.None, []);

    public async Task<RadioSuggestionsDto> LoadAsync(CancellationToken cancellationToken)
    {
        GetSuggestedRadioStationsRequest request = new() { CountryCode = regionProvider.GetCountryCode() };

        Result<RadioSuggestionsDto> result = await mediator.Send(request, cancellationToken);

        return result.IsSuccess ? result.Value : NoSuggestions;
    }

    public async Task PlayAsync(RadioSearchResultDto station, RadioSuggestionSource source)
    {
        await mediator.Send(new PlayRadioUrlRequest { Url = station.StreamUrl });

        _ = telemetryClient.CaptureEventAsync(TelemetryType, "SuggestionPlayed", BuildSourceProperties(source));
    }

    /// <summary>
    /// Saves a suggested station. Returns <c>true</c> when the station is now saved, including when it already was.
    /// </summary>
    public async Task<bool> AddAsync(RadioSearchResultDto station, RadioSuggestionSource source)
    {
        Result<long> result = await mediator.Send(station.ToAddRequest());

        if (result.IsSuccess)
        {
            _ = pictureService.DownloadAndSaveAsync(result.Value, station.FaviconUrl ?? string.Empty);
            _ = telemetryClient.CaptureEventAsync(TelemetryType, "SuggestionAdded", BuildSourceProperties(source));
            return true;
        }

        return result.Errors.FirstOrDefault() is ConflictError;
    }

    private static Dictionary<string, object> BuildSourceProperties(RadioSuggestionSource source) =>
        new() { ["source"] = source.ToString() };
}