using Rok.Application.Dto;

namespace Rok.Infrastructure.RadioBrowser.Mapping;

internal static class RadioBrowserStationMapping
{
    public static RadioSearchResultDto? ToDto(this RadioBrowserStationResponse r)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) return null;

        string? stream = !string.IsNullOrWhiteSpace(r.UrlResolved) ? r.UrlResolved : r.Url;
        if (string.IsNullOrWhiteSpace(stream)) return null;

        return new RadioSearchResultDto(
            Name: r.Name.Trim(),
            StreamUrl: stream.Trim(),
            HomepageUrl: NullIfEmpty(r.Homepage),
            StationUuid: NullIfEmpty(r.StationUuid),
            FaviconUrl: NullIfEmpty(r.Favicon),
            CountryCode: NullIfEmpty(r.CountryCode)?.ToLowerInvariant(),
            Codec: NullIfEmpty(r.Codec),
            Bitrate: r.Bitrate is > 0 ? r.Bitrate : null);
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
