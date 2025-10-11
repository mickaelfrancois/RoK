using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Rok.Application.Dto.NovaApi;
using Rok.Application.Interfaces;
using Rok.Application.Options;
using System.Reflection;

namespace Rok.Infrastructure.NovaApi;

public class NovaApiService : INovaApiService, IDisposable
{
    private const int KCacheDelayMinutes = 60 * 24;

    private readonly NovaApiOptions _novaApiOptions;

    private readonly IAppOptions _appOptions;

    private readonly HttpClient _httpClient;

    private readonly ILogger<NovaApiService> _logger;

    private static readonly SemaphoreSlim _concurrencyLimiter = new(5);

    private const int _concurrencyLimiterTimeout = 1000;

    public bool IsEnable { get; set; } = true;

    private readonly MemoryCache _artistCache = new(new MemoryCacheOptions());
    private readonly MemoryCache _albumCache = new(new MemoryCacheOptions());
    private readonly MemoryCache _lyricsCache = new(new MemoryCacheOptions());

    private bool disposedValue;

    public NovaApiService(HttpClient httpClient, IAppOptions appOptions, IOptions<NovaApiOptions> novaApiOptions, ILogger<NovaApiService> logger)
    {
        _httpClient = httpClient;
        _appOptions = appOptions;
        _novaApiOptions = novaApiOptions.Value;
        _logger = logger;

        ConfigureHttpClient();
    }

    private void ConfigureHttpClient()
    {
        if (!_appOptions.NovaApiEnabled || _novaApiOptions.BaseAddress is null)
        {
            _logger.LogInformation("Nova API is disabled.");

            IsEnable = false;
            return;
        }

        _logger.LogInformation("Nova API is enabled.");

        string appVersion = GetAppVersion();

        _httpClient.BaseAddress = new Uri(_novaApiOptions.BaseAddress);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", $"Rok/{appVersion}");
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
    }

    private static string GetAppVersion()
    {
        Assembly assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        return assembly.GetName().Version?.ToString() ?? "0.0.0";
    }


    public async Task<ApiArtistModel?> GetArtistAsync(string artistName)
    {
        if (!IsEnable)
            return null;

        Guard.Against.NullOrEmpty(artistName);

        ApiArtistModel? artist;

        if (!_artistCache.TryGetValue(artistName, out artist!))
        {
            string url = $"artists/" + Uri.EscapeDataString(artistName);
            artist = await GetASync<ApiArtistModel>(url);

            SaveArtistToCache(artistName, artist);
        }

        return artist;
    }


    public async Task GetArtistPictureAsync(string musicBrainzID, string category, string artistFile)
    {
        if (!IsEnable)
            return;

        Guard.Against.NullOrEmpty(musicBrainzID, nameof(musicBrainzID));
        Guard.Against.NullOrEmpty(category, nameof(category));
        Guard.Against.NullOrEmpty(artistFile, nameof(artistFile));

        string pictureUri = $"artists/picture/{musicBrainzID}/{category}";

        try
        {
            await DownloadFileAsync(new Uri(pictureUri), artistFile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading artist picture {Url}", pictureUri);
        }
    }


    public async Task GetArtistBackdropsAsync(string musicBrainzId, int fanartsCount, string artistFolder)
    {
        if (!IsEnable)
            return;

        string pictureUrl = $"artists/fanart/{musicBrainzId}";

        for (int i = 0; i < fanartsCount; i++)
        {
            try
            {
                string fileName = Path.Combine(artistFolder, "backdrop" + Guid.NewGuid().ToString("d") + ".jpg");
                await DownloadFileAsync(new Uri(pictureUrl + $"/{i}"), fileName);
            }
            catch
            {
                // Ignore errors
            }
        }
    }


    public async Task<bool> GetArtistBackdropAsync(string url, string artistFolder)
    {
        if (!IsEnable)
            return false;

        try
        {
            string fileName = Path.Combine(artistFolder, "backdrop" + Guid.NewGuid().ToString("d") + ".jpg");
            await DownloadFileAsync(new Uri(url), fileName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading artist backdrop {Url}", url);
        }

        return false;
    }


    public async Task<ApiAlbumModel?> GetAlbumAsync(string albumName, string artistName)
    {
        if (!IsEnable)
            return null;
        if (string.IsNullOrEmpty(albumName))
            return null;
        if (string.IsNullOrEmpty(artistName))
            return null;

        ApiAlbumModel? album;
        string key = $"{artistName}_{albumName}";

        if (_albumCache.TryGetValue(key, out album) == false)
        {
            string url = $"albums/" + Uri.EscapeDataString(albumName) + "/" + Uri.EscapeDataString(artistName);
            album = await GetASync<ApiAlbumModel>(url);

            SaveAlbumToCache(key, album);
        }

        return album;
    }


    public async Task GetAlbumPicturesAsync(string musicBrainzID, string albumFile)
    {
        if (!IsEnable)
            return;

        Guard.Against.NullOrEmpty(musicBrainzID, nameof(musicBrainzID));
        Guard.Against.NullOrEmpty(albumFile, nameof(albumFile));

        string pictureUri = $"albums/cover/{musicBrainzID}";

        try
        {
            await DownloadFileAsync(new Uri(pictureUri), albumFile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading album picture {MusicBrainzID}", musicBrainzID);
        }
    }


    public async Task<ApiLyricsModel?> GetLyricsAsync(string artistName, string title)
    {
        if (!IsEnable)
            return null;

        if (string.IsNullOrEmpty(artistName))
            return null;

        if (string.IsNullOrEmpty(title))
            return null;

        ApiLyricsModel lyrics;
        string key = $"{artistName}_{title}";

        if (_lyricsCache.TryGetValue(key, out lyrics) == false)
        {
            string url = $"lyrics/" + Uri.EscapeDataString(artistName) + "/" + Uri.EscapeDataString(title);
            lyrics = await GetASync<ApiLyricsModel>(url);

            SaveLyricsToCache(key, lyrics);
        }

        return lyrics;
    }


    private void SaveArtistToCache(string key, ApiArtistModel? artist)
    {
        using ICacheEntry entry = _artistCache.CreateEntry(key);

        entry.Value = artist;
        entry.AbsoluteExpiration = DateTime.UtcNow.AddMinutes(KCacheDelayMinutes);
    }


    private void SaveAlbumToCache(string key, ApiAlbumModel? album)
    {
        using ICacheEntry entry = _albumCache.CreateEntry(key);

        entry.Value = album;
        entry.AbsoluteExpiration = DateTime.UtcNow.AddMinutes(KCacheDelayMinutes);
    }


    private void SaveLyricsToCache(string key, ApiLyricsModel? lyrics)
    {
        using ICacheEntry entry = _lyricsCache.CreateEntry(key);

        entry.Value = lyrics;
        entry.AbsoluteExpiration = DateTime.UtcNow.AddMinutes(KCacheDelayMinutes);
    }


    private async Task<T?> GetASync<T>(string url, CancellationToken cancellationToken = default)
    {
        T result = default!;
        bool acquired;

        try
        {
            acquired = await _concurrencyLimiter.WaitAsync(_concurrencyLimiterTimeout, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Request cancelled before entering limiter {Url}", url);
            return result;
        }

        if (acquired)
        {
            try
            {
                HttpResponseMessage response = await _httpClient.GetAsync(url, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync(cancellationToken);
                    result = JsonConvert.DeserializeObject<T>(json);
                }
                else
                {
                    _logger.LogError("Nova API response {Error} {Url}", response.StatusCode, url);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Request cancelled {Url}", url);
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
                catch (SemaphoreFullException)
                {
                    _logger.LogWarning("Semaphore release called when full for {Url}", url);
                }
            }
        }

        return result;
    }



    private async Task DownloadFileAsync(Uri uri, string path, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));

        bool acquired;

        try
        {
            acquired = await _concurrencyLimiter.WaitAsync(_concurrencyLimiterTimeout, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Request cancelled before entering limiter {Url}", uri);
            return;
        }

        if (!acquired)
            return;

        try
        {
            using HttpResponseMessage response = await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed download {Status} {Url}", response.StatusCode, uri);
                return;
            }

            using Stream inputStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using FileStream writer = new(path, FileMode.Create, FileAccess.Write, FileShare.None, 8192, useAsync: true);

            await inputStream.CopyToAsync(writer, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Request cancelled {Url}", uri);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HTTP error {Url}", uri);
        }
        finally
        {
            try
            {
                _concurrencyLimiter.Release();
            }
            catch (SemaphoreFullException)
            {
                _logger.LogWarning("Semaphore release called when full for {Url}", uri);
            }
        }
    }


    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            disposedValue = true;
        }
    }


    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
