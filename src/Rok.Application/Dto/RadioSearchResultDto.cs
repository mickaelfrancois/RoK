namespace Rok.Application.Dto;

public record RadioSearchResultDto(
    string Name,
    string StreamUrl,
    string? HomepageUrl,
    string? StationUuid,
    string? FaviconUrl,
    string? CountryCode,
    string? Codec,
    int? Bitrate);
