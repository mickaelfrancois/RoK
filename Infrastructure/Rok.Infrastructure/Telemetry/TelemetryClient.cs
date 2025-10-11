using Microsoft.Extensions.Options;
using Rok.Application.Interfaces;
using Rok.Application.Options;
using System.Net.Http.Json;
using System.Reflection;

namespace Rok.Infrastructure.Telemetry;

public class TelemetryClient : ITelemetryClient
{
    private readonly HttpClient _httpClient;

    private readonly IAppOptions _appOptions;

    private readonly TelemetryOptions _telemetryOptions;
    
    private bool _isEnabled = true;


    public TelemetryClient(HttpClient httpClient, IAppOptions appOptions, IOptions<TelemetryOptions> telemetryOptions)
    {
        _httpClient = httpClient;
        _appOptions = appOptions;
        _telemetryOptions = telemetryOptions.Value;

        ConfigureHttpClient();
    }


    private void ConfigureHttpClient()
    {
        if(_telemetryOptions.BaseAddress is null || _telemetryOptions.ApiKey is null)
        {
            _isEnabled = false;
            return;
        }

        string appVersion = GetAppVersion();

        _httpClient.BaseAddress = new Uri(_telemetryOptions.BaseAddress);
        _httpClient.DefaultRequestHeaders.Add("X-Api-Key", _telemetryOptions.ApiKey);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", $"Rok/{appVersion}");
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
    }


    private static string GetAppVersion()
    {
        Assembly assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        return assembly.GetName().Version?.ToString() ?? "0.0.0";
    }


    public async Task CaptureScreenAsync(string screenName)
    {
        if (!_isEnabled)
            return;

        Dictionary<string, object> payload = new()
        {
            ["event"] = "$screen",
            ["distinct_id"] = _appOptions.Id,
            ["properties"] = new Dictionary<string, object>
            {
                ["$screen_name"] = screenName
            }
        };

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));

        try
        {
            await _httpClient.PostAsJsonAsync("api/v1/capture", payload, cts.Token);
        }
        catch
        {
            // Ignore exception
        }
    }


    public async Task CaptureEventAsync(string eventName)
    {
        if (!_isEnabled)
            return;

        Dictionary<string, object> payload = new()
        {
            ["event"] = eventName,
            ["distinct_id"] = _appOptions.Id
        };

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));

        try
        {
            await _httpClient.PostAsJsonAsync("/api/v1/capture", payload, cts.Token);
        }
        catch
        {
            // Ignore exception
        }
    }


    public async Task CaptureExceptionAsync(Exception ex)
    {
        if (!_isEnabled)
            return;

        Dictionary<string, object> payload = new()
        {
            ["event"] = "$exception",
            ["distinct_id"] = _appOptions.Id,
            ["properties"] = new Dictionary<string, object>
            {
                ["$exception_list"] = new object[]
                {
                    new Dictionary<string, object>
                    {
                        ["type"] = ex.GetType().Name,
                        ["value"] = ex.Message,
                        ["stacktrace"] = ex.StackTrace ?? string.Empty
                    }
                },
                ["$exception_fingerprint"] = $"{ex.GetType().Name}:{ex.Message}",
                ["$exception_level"] = "error",
            }
        };

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));

        try
        {
            await _httpClient.PostAsJsonAsync("api/v1/capture", payload, cts.Token);
        }
        catch
        {
            // Ignore exception
        }
    }
}
