namespace Rok.Application.Options;

public sealed class TranslateApiOptions
{
    public string? BaseAddress { get; set; } = "https://roktranslate-api.fpc-france.com";

    public string? ApiKey { get; set; }
}
