namespace Rok.Application.Dto;

/// <summary>
/// Where a list of suggested radio stations comes from.
/// </summary>
public enum RadioSuggestionSource
{
    None,
    Country,
    Worldwide
}

/// <summary>
/// Radio stations suggested to a user who has no saved station yet.
/// </summary>
/// <param name="Source">Origin of the stations, <see cref="RadioSuggestionSource.None"/> when nothing could be loaded.</param>
/// <param name="Stations">Suggested stations, most voted first.</param>
public sealed record RadioSuggestionsDto(RadioSuggestionSource Source, IReadOnlyList<RadioSearchResultDto> Stations);