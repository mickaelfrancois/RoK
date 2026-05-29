using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Rok.Application.Dto;
using Rok.Application.Features.Radios.Services;
using Rok.Infrastructure.RadioBrowser.Mapping;

namespace Rok.Infrastructure.RadioBrowser;

internal sealed class RadioBrowserClient(HttpClient http, ILogger<RadioBrowserClient> logger)
    : IRadioBrowserClient
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<RadioSearchResultDto>> SearchByNameAsync(
        string query, int limit, CancellationToken ct)
    {
        string encoded = Uri.EscapeDataString(query);
        string path = $"json/stations/byname/{encoded}?limit={limit}&hidebroken=true&order=votes&reverse=true";

        logger.LogDebug("Radio-Browser search: query='{Query}' limit={Limit}", query, limit);

        using HttpResponseMessage response = await http.GetAsync(path, ct);
        response.EnsureSuccessStatusCode();

        RadioBrowserStationResponse[]? raw =
            await response.Content.ReadFromJsonAsync<RadioBrowserStationResponse[]>(JsonOpts, ct);

        if (raw is null) return [];

        return raw.Select(r => r.ToDto())
                  .Where(d => d is not null)
                  .Select(d => d!)
                  .ToArray();
    }
}