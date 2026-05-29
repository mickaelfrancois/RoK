namespace Rok.Domain.Entities;

[Table("RadioStations")]
public class RadioStationEntity : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string StreamUrl { get; set; } = string.Empty;

    public string? HomepageUrl { get; set; }

    public string? StationUuid { get; set; }

    public string? FaviconUrl { get; set; }

    public string? CountryCode { get; set; }

    public string? Codec { get; set; }

    public int? Bitrate { get; set; }

    public DateTime AddedAt { get; set; }

    public DateTime? LastListen { get; set; }
}
