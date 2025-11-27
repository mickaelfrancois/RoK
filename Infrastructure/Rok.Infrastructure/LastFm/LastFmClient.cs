using Microsoft.Extensions.Logging;
using Rok.Application.Interfaces;
using System.Reflection;

namespace Rok.Infrastructure.LastFm;

internal class LastFmClient : ILastFmClient
{
    private const string UriString = "https://www.last.fm/fr/music/";
    private readonly HttpClient _httpClient;

    private readonly ILogger<LastFmClient> _logger;

    public LastFmClient(HttpClient httpClient, ILogger<LastFmClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        ConfigureHttpClient();
    }


    public string GetArtistPageUrl(string artistName)
    {
        if (string.IsNullOrWhiteSpace(artistName))
            return string.Empty;

        return _httpClient.BaseAddress + Uri.EscapeDataString(artistName.Replace(' ', '+'));
    }


    public string GetAlbumPageUrl(string artistName, string albumName)
    {
        if (string.IsNullOrWhiteSpace(artistName) || string.IsNullOrEmpty(albumName))
            return string.Empty;

        return _httpClient.BaseAddress
                            + Uri.EscapeDataString(artistName.Replace(' ', '+'))
                            + "/"
                            + Uri.EscapeDataString(albumName.Replace(' ', '+'));
    }


    public async Task<bool> IsArtistPageAvailableAsync(string artistName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(artistName))
            return false;

        string url = GetArtistPageUrl(artistName);

        return await CheckPageAvailableAsync(url, cancellationToken);
    }


    public async Task<bool> IsAlbumPageAvailableAsync(string artistName, string albumName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(artistName) || string.IsNullOrEmpty(albumName))
            return false;

        string url = GetAlbumPageUrl(artistName, albumName);

        return await CheckPageAvailableAsync(url, cancellationToken);
    }


    private async Task<bool> CheckPageAvailableAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            HttpRequestMessage headRequest = new(HttpMethod.Head, url);
            HttpResponseMessage response = await _httpClient.SendAsync(headRequest, cancellationToken);

            _logger.LogDebug("Checked Last.fm page: {Url}, Status Code: {StatusCode}", url, response.StatusCode);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking Last.fm page: {Url}", url);

            return false;
        }
    }


    private void ConfigureHttpClient()
    {
        string appVersion = GetAppVersion();

        _httpClient.BaseAddress = new Uri(UriString);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", $"Rok/{appVersion}");
        _httpClient.Timeout = TimeSpan.FromSeconds(5);
    }

    private static string GetAppVersion()
    {
        Assembly assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        return assembly.GetName().Version?.ToString() ?? "0.0.0";
    }
}
