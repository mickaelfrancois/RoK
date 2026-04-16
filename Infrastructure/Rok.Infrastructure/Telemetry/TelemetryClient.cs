using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rok.Application.Interfaces;
using Rok.Application.Options;
using Windows.ApplicationModel;

namespace Rok.Infrastructure.Telemetry;

public class TelemetryClient : ITelemetryClient
{
    private readonly ILogger<TelemetryClient> _logger;

    private readonly HttpClient _httpClient;

    private readonly IAppOptions _appOptions;

    private readonly TelemetryOptions _telemetryOptions;

    private bool _isEnabled = true;

    private string _appVersion = string.Empty;


    public TelemetryClient(HttpClient httpClient, IAppOptions appOptions, IOptions<TelemetryOptions> telemetryOptions, ILogger<TelemetryClient> logger)
    {
        _httpClient = httpClient;
        _appOptions = appOptions;
        _telemetryOptions = telemetryOptions.Value;
        _logger = logger;

        ConfigureHttpClient();
    }


    private void ConfigureHttpClient()
    {
        if (!_appOptions.TelemetryEnabled || _telemetryOptions.BaseAddress is null || _telemetryOptions.ApiKey is null)
        {
            _logger.LogInformation("Telemetry is disabled.");

            _isEnabled = false;
            return;
        }

        _logger.LogInformation("Telemetry is enabled.");

        _appVersion = GetAppVersion();

        _httpClient.BaseAddress = new Uri(_telemetryOptions.BaseAddress);
        _httpClient.DefaultRequestHeaders.Add("X-Rok-Key", _telemetryOptions.ApiKey);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", $"Rok/{_appVersion}");
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
    }


    private static string GetAppVersion()
    {
        PackageVersion version = Package.Current.Id.Version;
        return $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
    }


    public async Task CaptureScreenAsync(string screenName)
    {
        if (!_isEnabled)
            return;

        EventDto eventDto = new()
        {
            ApplicationId = _appOptions.Id.ToString(),
            EventType = "screen",
            EventName = screenName,
            AppVersion = _appVersion,
            OsVersion = Environment.OSVersion.Platform.ToString(),
            Language = System.Globalization.CultureInfo.CurrentCulture.Name,
            TimeZone = TimeZoneInfo.Local.StandardName,
        };

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));

        try
        {
            await _httpClient.PostAsJsonAsync("/events", eventDto, cts.Token);
        }
        catch
        {
            // Ignore exception
        }
    }


    public async Task CaptureEventAsync(string eventType, string eventName, Dictionary<string, object>? properties = null)
    {
        if (!_isEnabled)
            return;

        EventDto eventDto = new()
        {
            ApplicationId = _appOptions.Id.ToString(),
            EventType = eventType,
            EventName = eventName,
            AppVersion = _appVersion,
            OsVersion = Environment.OSVersion.Platform.ToString(),
            Language = System.Globalization.CultureInfo.CurrentCulture.Name,
            TimeZone = TimeZoneInfo.Local.StandardName,
            Payload = properties is not null ? System.Text.Json.JsonSerializer.Serialize(properties) : string.Empty
        };

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));

        try
        {
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync("events", eventDto, cts.Token);
            if (response.IsSuccessStatusCode)
            {
                // test
            }
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

        ExceptionDto exceptionDto = new()
        {
            ApplicationId = _appOptions.Id.ToString(),
            Type = ex.GetType().FullName ?? ex.GetType().Name,
            Message = BuildFullMessage(ex),
            StackTrace = BuildFullStackTrace(ex),
            AppVersion = _appVersion,
            OsVersion = Environment.OSVersion.Platform.ToString(),
            Language = System.Globalization.CultureInfo.CurrentCulture.Name,
            TimeZone = TimeZoneInfo.Local.StandardName
        };

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));

        try
        {
            await _httpClient.PostAsJsonAsync("/exceptions", exceptionDto, cts.Token);
        }
        catch
        {
            // Ignore exception
        }
    }

    private static string BuildFullMessage(Exception ex)
    {
        var parts = new System.Text.StringBuilder();
        Exception? current = ex;
        while (current is not null)
        {
            if (parts.Length > 0)
                parts.Append(" ---> ");
            parts.Append($"[{current.GetType().Name}] {current.Message}");
            current = current.InnerException;
        }
        return parts.ToString();
    }

    private static string BuildFullStackTrace(Exception ex)
    {
        var parts = new System.Text.StringBuilder();
        Exception? current = ex;
        while (current is not null)
        {
            if (parts.Length > 0)
                parts.AppendLine("--- End of inner exception stack trace ---");
            if (current.StackTrace is not null)
                parts.AppendLine(current.StackTrace);
            current = current.InnerException;
        }
        return parts.ToString().TrimEnd();
    }

    private sealed record EventDto
    {
        public string ApplicationId { get; init; } = string.Empty;
        public string EventType { get; init; } = string.Empty;
        public string EventName { get; init; } = string.Empty;
        public string AppVersion { get; init; } = string.Empty;
        public string OsVersion { get; init; } = string.Empty;
        public string Language { get; init; } = string.Empty;
        public string TimeZone { get; init; } = string.Empty;
        public string Payload { get; init; } = string.Empty;
    }

    private sealed record ExceptionDto
    {
        public string ApplicationId { get; init; } = string.Empty;
        public string Type { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
        public string StackTrace { get; init; } = string.Empty;
        public string AppVersion { get; init; } = string.Empty;
        public string OsVersion { get; init; } = string.Empty;
        public string Language { get; init; } = string.Empty;
        public string TimeZone { get; init; } = string.Empty;
        public string Payload { get; init; } = string.Empty;
    }
}
