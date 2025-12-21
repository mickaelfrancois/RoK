using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rok.Application.Interfaces;
using Rok.Application.Options;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace Rok.Infrastructure.Translate;

public class TranslateService : ITranslateService
{
    private readonly HttpClient _httpClient;

    private readonly ILogger<TranslateService> _logger;

    public bool IsEnable { get; set; } = true;

    private readonly TranslateApiOptions _apiOptions;

    private readonly IAppOptions _appOptions;


    public TranslateService(HttpClient httpClient, IAppOptions appOptions, IOptions<TranslateApiOptions> apiOptions, ILogger<TranslateService> logger)
    {
        _httpClient = httpClient;
        _appOptions = appOptions;
        _apiOptions = apiOptions.Value;
        _logger = logger;

        ConfigureHttpClient();
    }


    private void ConfigureHttpClient()
    {
        if (!_appOptions.NovaApiEnabled || _apiOptions.BaseAddress is null)
        {
            _logger.LogInformation("Translate API is disabled.");

            IsEnable = false;
            return;
        }

        _logger.LogInformation("Translate API is enabled.");

        string appVersion = GetAppVersion();

        _httpClient.BaseAddress = new Uri(_apiOptions.BaseAddress);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", $"Rok/{appVersion}");
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }


    private static string GetAppVersion()
    {
        Assembly assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        return assembly.GetName().Version?.ToString() ?? "0.0.0";
    }


    public async Task<string?> TranslateAsync(string text, string targetLang, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;
        if (string.IsNullOrEmpty(targetLang))
            return text;
        if (!IsEnable)
            return text;

        string sourceLang = "auto";

        var payload = new
        {
            q = text,
            source = sourceLang,
            target = targetLang,
            format = "text",
            api_key = _apiOptions.ApiKey
        };

        string json = JsonSerializer.Serialize(payload);
        using StringContent content = new(json, Encoding.UTF8, "application/json");

        using HttpResponseMessage resp = await _httpClient.PostAsync("/translate", content, cancellationToken).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
            return null;

        using Stream stream = await resp.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using JsonDocument doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (doc.RootElement.TryGetProperty("translatedText", out JsonElement translated))
            return translated.GetString();

        return string.Empty;

        // {"detectedLanguage":{"confidence":90.0,"language":"en"},"translatedText":"Bonjour"}
    }


    public static string NormalizeLanguageForLibreTranslate(string? languageTag, string defaultLang = "fr")
    {
        if (string.IsNullOrWhiteSpace(languageTag))
            return defaultLang;

        try
        {
            CultureInfo culture = new(languageTag);
            string two = culture.TwoLetterISOLanguageName?.ToLowerInvariant() ?? defaultLang;
            return string.IsNullOrWhiteSpace(two) ? defaultLang : two;
        }
        catch (CultureNotFoundException)
        {
            string[] parts = languageTag.Split(new[] { '-' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return defaultLang;

            string primary = parts[0].ToLowerInvariant();
            if (primary.Length == 2)
                return primary;

            return primary.Length >= 2 ? primary.Substring(0, 2) : defaultLang;
        }
    }
}
