using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rok.Application.Interfaces;
using Rok.Application.Options;
using System.Net.Http.Json;
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
        _httpClient.DefaultRequestHeaders.Add("X-Api-Key", _telemetryOptions.ApiKey);
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

        Dictionary<string, object> payload = new()
        {
            ["event"] = "$screen",
            ["distinct_id"] = _appOptions.Id,
            ["properties"] = new Dictionary<string, object>
            {
                ["$screen_name"] = screenName,
                ["$browser_version"] = _appVersion
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


    public async Task CaptureEventAsync(string eventName, Dictionary<string, object>? properties = null)
    {
        if (!_isEnabled)
            return;

        Dictionary<string, object> payload = new()
        {
            ["event"] = eventName,
            ["distinct_id"] = _appOptions.Id
        };

        if (properties is not null && properties.Count > 0)
            payload["properties"] = properties;

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

        List<object> exceptionList = BuildExceptionList(ex);

        Dictionary<string, object> payload = new()
        {
            ["event"] = "$exception",
            ["distinct_id"] = _appOptions.Id,
            ["properties"] = new Dictionary<string, object>
            {
                ["$exception_type"] = ex.GetType().FullName ?? ex.GetType().Name,
                ["$exception_message"] = ex.Message,
                ["$exception_list"] = exceptionList,
                ["$exception_fingerprint"] = GenerateFingerprint(ex),
                ["$exception_level"] = "error",
                ["$exception_handled"] = true,
                ["$browser_version"] = _appVersion,
                ["$os"] = Environment.OSVersion.Platform.ToString(),
                ["platform"] = "dotnet"
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


    private static List<object> BuildExceptionList(Exception ex)
    {
        List<object> exceptionList = [];
        Exception? currentException = ex;

        while (currentException is not null)
        {
            Dictionary<string, object> exceptionData = new()
            {
                ["type"] = currentException.GetType().FullName ?? currentException.GetType().Name,
                ["value"] = currentException.Message,
                ["mechanism"] = new Dictionary<string, object>
                {
                    ["handled"] = true,
                    ["synthetic"] = false
                }
            };

            if (!string.IsNullOrEmpty(currentException.StackTrace))
            {
                exceptionData["stacktrace"] = new Dictionary<string, object>
                {
                    ["type"] = "raw",
                    ["frames"] = ParseStackTrace(currentException)
                };
            }

            exceptionList.Add(exceptionData);
            currentException = currentException.InnerException;
        }

        return exceptionList;
    }


    private static string GenerateFingerprint(Exception ex)
    {
        Exception rootException = ex;

        while (rootException.InnerException is not null)
            rootException = rootException.InnerException;

        return $"{rootException.GetType().Name}:{GetFirstLineOfMessage(rootException.Message)}";
    }


    private static string GetFirstLineOfMessage(string message)
    {
        int newLineIndex = message.IndexOfAny(['\r', '\n']);
        return newLineIndex > 0 ? message[..newLineIndex] : message;
    }


    private static List<Dictionary<string, object>> ParseStackTrace(Exception ex)
    {
        List<Dictionary<string, object>> frames = [];

        if (string.IsNullOrEmpty(ex.StackTrace))
            return frames;

        string[] lines = ex.StackTrace.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries);

        foreach (string line in lines)
        {
            string trimmedLine = line.Trim();
            string filename = ExtractFileName(trimmedLine);

            Dictionary<string, object> frame = new()
            {
                ["platform"] = "custom",
                ["lang"] = "csharp",
                ["function"] = ExtractMethodName(trimmedLine),
                ["filename"] = filename,
                ["lineno"] = ExtractLineNumber(trimmedLine),
                ["in_app"] = IsInAppFrame(filename)
            };

            frames.Add(frame);
        }

        return frames;
    }


    private static bool IsInAppFrame(string filename)
    {
        if (string.IsNullOrEmpty(filename))
            return false;

        return filename.Contains("\\Rok.", StringComparison.OrdinalIgnoreCase);
    }


    private static string ExtractMethodName(string stackFrame)
    {
        int atIndex = stackFrame.IndexOf(" at ", StringComparison.Ordinal);
        if (atIndex == -1)
            return stackFrame;

        int inIndex = stackFrame.IndexOf(" in ", StringComparison.Ordinal);
        if (inIndex > atIndex)
            return stackFrame[(atIndex + 4)..inIndex].Trim();

        return stackFrame[(atIndex + 4)..].Trim();
    }


    private static string ExtractFileName(string stackFrame)
    {
        int inIndex = stackFrame.IndexOf(" in ", StringComparison.Ordinal);
        if (inIndex == -1)
            return string.Empty;

        int lineIndex = stackFrame.IndexOf(":line ", StringComparison.Ordinal);
        if (lineIndex > inIndex)
            return stackFrame[(inIndex + 4)..lineIndex].Trim();

        return stackFrame[(inIndex + 4)..].Trim();
    }


    private static int ExtractLineNumber(string stackFrame)
    {
        int lineIndex = stackFrame.IndexOf(":line ", StringComparison.Ordinal);
        if (lineIndex == -1)
            return 0;

        string lineNumberStr = stackFrame[(lineIndex + 6)..].Trim();
        return int.TryParse(lineNumberStr, out int lineNumber) ? lineNumber : 0;
    }
}
