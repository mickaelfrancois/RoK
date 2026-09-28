using Rok.Application.Features.Radios.Requests;

namespace Rok.Mapping;

public static class RadioSearchResultMapping
{
    public static AddRadioStationRequest ToAddRequest(this RadioSearchResultDto station) => new()
    {
        Name = station.Name,
        StreamUrl = station.StreamUrl,
        HomepageUrl = station.HomepageUrl,
        StationUuid = station.StationUuid,
        FaviconUrl = station.FaviconUrl,
        CountryCode = station.CountryCode,
        Codec = station.Codec,
        Bitrate = station.Bitrate,
    };
}