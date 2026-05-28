namespace Rok.Application.Dto;

public record RadioStationDto(
    long Id,
    string Name,
    string StreamUrl,
    string? HomepageUrl,
    DateTime AddedAt,
    DateTime? LastListen);
