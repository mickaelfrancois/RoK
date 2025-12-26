using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rok.Application.Interfaces;
using Rok.Application.Options;

namespace Rok.Infrastructure.NovaApi;

public class MusicDataApiService : IMusicDataApiService, IDisposable
{
    private const int KCacheDelayMinutes = 60 * 24;

    private const int KMinApiDelayDays = 7;

    private readonly MusicDataApiOptions _musicDataApiOptions;

    private readonly IAppOptions _appOptions;

    private readonly HttpClient _httpClient;

    private readonly ILogger<MusicDataApiService> _logger;

    private static readonly SemaphoreSlim _concurrencyLimiter = new(5);

    private const int _concurrencyLimiterTimeout = 1000;

    public bool IsEnable { get; set; } = true;

    private readonly MemoryCache _artistCache = new(new MemoryCacheOptions());
    private readonly MemoryCache _albumCache = new(new MemoryCacheOptions());
    private readonly MemoryCache _lyricsCache = new(new MemoryCacheOptions());

    private bool _disposedValue;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };


    public MusicDataApiService(HttpClient httpClient, IAppOptions appOptions, IOptions<MusicDataApiOptions> musicDataApiOptions, ILogger<MusicDataApiService> logger)
    {
        _httpClient = httpClient;
        _appOptions = appOptions;
        _musicDataApiOptions = musicDataApiOptions.Value;
        _logger = logger;

        ConfigureHttpClient();
    }


    private void ConfigureHttpClient()
    {
        if (!_appOptions.NovaApiEnabled || _musicDataApiOptions.BaseAddress is null)
        {
            _logger.LogInformation("RoK Music API is disabled.");

            IsEnable = false;
            return;
        }

        _logger.LogInformation("RoK Music API is enabled.");

        string appVersion = GetAppVersion();

        _httpClient.BaseAddress = new Uri(_musicDataApiOptions.BaseAddress);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", $"Rok/{appVersion}");
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
    }


    private static string GetAppVersion()
    {
        Assembly assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        return assembly.GetName().Version?.ToString() ?? "0.0.0";
    }


    public async Task<MusicDataArtistDto?> GetArtistAsync(string artistName, string? musicBrainzId)
    {
        if (!IsEnable)
            return null;

        if (artistName is null)
            return null;

        MusicDataArtistDto? artist;
        string url;

        if (!_artistCache.TryGetValue(artistName, out artist!))
        {
            if (string.IsNullOrEmpty(musicBrainzId))
                url = $"v1/artists/byname/{Uri.EscapeDataString(artistName)}";
            else
                url = $"v1/artists/bymbid{Uri.EscapeDataString(artistName)}";

            artist = await GetASync<MusicDataArtistDto>(url);

            SaveArtistToCache(artistName, artist);
        }

        return artist;
    }


    public async Task<MusicDataAlbumDto?> GetAlbumAsync(string albumName, string artistName, string? musicBrainzId)
    {
        if (!IsEnable)
            return null;

        if (artistName is null || albumName is null)
            return null;

        MusicDataAlbumDto? album;
        string url;

        string key = $"{artistName}_{albumName}";

        if (!_albumCache.TryGetValue(key, out album!))
        {
            if (string.IsNullOrEmpty(musicBrainzId))
                url = $"v1/albums/byname/{Uri.EscapeDataString(artistName)}/{Uri.EscapeDataString(albumName)}";
            else
                url = $"v1/albums/bymbid{Uri.EscapeDataString(musicBrainzId)}";

            album = await GetASync<MusicDataAlbumDto>(url);

            SaveAlbumToCache(key, album);
        }

        return album;
    }


    public async Task<MusicDataLyricsDto?> GetLyricsAsync(string artistName, string albumName, string title, int duration)
    {
        if (!IsEnable)
            return null;

        if (string.IsNullOrEmpty(artistName))
            return null;

        if (string.IsNullOrEmpty(title))
            return null;

        MusicDataLyricsDto? lyrics;
        string key = $"{artistName}_{title}";

        if (!_lyricsCache.TryGetValue(key, out lyrics))
        {
            string url = $"v1/lyrics/artistName={Uri.EscapeDataString(artistName)}&albumName={albumName}&title={Uri.EscapeDataString(title)}&duration={duration}";
            lyrics = await GetASync<MusicDataLyricsDto>(url);

            SaveLyricsToCache(key, lyrics);
        }

        return lyrics;
    }



    public static bool IsApiRetryAllowed(DateTime? lastAttempt)
    {
        if (!lastAttempt.HasValue)
            return true;

        DateTime threshold = lastAttempt.Value.AddDays(KMinApiDelayDays);
        return DateTime.UtcNow >= threshold;
    }


    private void SaveArtistToCache(string key, MusicDataArtistDto? artist)
    {
        using ICacheEntry entry = _artistCache.CreateEntry(key);

        entry.Value = artist;
        entry.AbsoluteExpiration = DateTime.UtcNow.AddMinutes(KCacheDelayMinutes);
    }


    private void SaveAlbumToCache(string key, MusicDataAlbumDto? album)
    {
        using ICacheEntry entry = _albumCache.CreateEntry(key);

        entry.Value = album;
        entry.AbsoluteExpiration = DateTime.UtcNow.AddMinutes(KCacheDelayMinutes);
    }


    private void SaveLyricsToCache(string key, MusicDataLyricsDto? lyrics)
    {
        using ICacheEntry entry = _lyricsCache.CreateEntry(key);

        entry.Value = lyrics;
        entry.AbsoluteExpiration = DateTime.UtcNow.AddMinutes(KCacheDelayMinutes);
    }


    private async Task<T?> GetASync<T>(string url, CancellationToken cancellationToken = default)
    {
        T? result = default;
        bool acquired;

        try
        {
            acquired = await _concurrencyLimiter.WaitAsync(_concurrencyLimiterTimeout, cancellationToken);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "Request cancelled before entering limiter {Url}", url);
            return result;
        }

        if (acquired)
        {
            try
            {
                HttpResponseMessage response = await _httpClient.GetAsync(url, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                    result = await JsonSerializer.DeserializeAsync<T>(stream, _jsonOptions, cancellationToken);
                }
                else
                {
                    _logger.LogError("Nova API response {Error} {Url}", response.StatusCode, url);
                }
            }
            catch (OperationCanceledException)
            {
                // Ignore errors
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HTTP error {Url}", url);
            }
            finally
            {
                try
                {
                    _concurrencyLimiter.Release();
                }
                catch (SemaphoreFullException ex)
                {
                    _logger.LogWarning(ex, "Semaphore release called when full for {Url}", url);
                }
            }
        }

        return result;
    }



    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            _disposedValue = true;
        }
    }


    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
