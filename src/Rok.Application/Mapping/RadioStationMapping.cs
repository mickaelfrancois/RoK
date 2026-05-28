namespace Rok.Application.Mapping;

public static class RadioStationMapping
{
    public static RadioStationDto ToDto(this RadioStationEntity entity) =>
        new(entity.Id, entity.Name, entity.StreamUrl, entity.HomepageUrl, entity.AddedAt, entity.LastListen);

    public static RadioStationEntity ToEntity(this RadioStationDto dto) =>
        new()
        {
            Id = dto.Id,
            Name = dto.Name,
            StreamUrl = dto.StreamUrl,
            HomepageUrl = dto.HomepageUrl,
            AddedAt = dto.AddedAt,
            LastListen = dto.LastListen
        };
}
