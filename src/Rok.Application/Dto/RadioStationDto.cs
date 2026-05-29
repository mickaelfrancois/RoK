namespace Rok.Application.Dto;

public record RadioStationDto(
    long Id,
    string Name,
    string StreamUrl,
    string? HomepageUrl,
    string? StationUuid,
    string? FaviconUrl,
    string? CountryCode,
    string? Codec,
    int? Bitrate,
    DateTime AddedAt,
    DateTime? LastListen);