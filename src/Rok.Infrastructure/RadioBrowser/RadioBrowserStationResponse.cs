using System.Text.Json.Serialization;

namespace Rok.Infrastructure.RadioBrowser;

internal sealed class RadioBrowserStationResponse
{
    [JsonPropertyName("stationuuid")] public string? StationUuid { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("url")] public string? Url { get; set; }
    [JsonPropertyName("url_resolved")] public string? UrlResolved { get; set; }
    [JsonPropertyName("homepage")] public string? Homepage { get; set; }
    [JsonPropertyName("favicon")] public string? Favicon { get; set; }
    [JsonPropertyName("countrycode")] public string? CountryCode { get; set; }
    [JsonPropertyName("codec")] public string? Codec { get; set; }
    [JsonPropertyName("bitrate")] public int? Bitrate { get; set; }
    [JsonPropertyName("lastcheckok")] public int? LastCheckOk { get; set; }
}
