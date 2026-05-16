using Windows.ApplicationModel.Resources;

namespace Rok.Services;

internal sealed class WindowsStringResourceProvider(ResourceLoader resourceLoader) : IStringResourceProvider
{
    public string GetString(string key) => resourceLoader.GetString(key) ?? string.Empty;
}