namespace Rok.Application.Options;

public sealed class RadioBrowserOptions
{
    public string BaseAddress { get; set; } = "https://de1.api.radio-browser.info/";

    public int TimeoutSeconds { get; set; } = 8;

    public string UserAgent { get; set; } = "Rok/1.0";
}
