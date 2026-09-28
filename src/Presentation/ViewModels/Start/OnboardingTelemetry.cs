namespace Rok.ViewModels.Start;

public static class OnboardingTelemetry
{
    public const string UnsupportedTotalKey = "unsupportedTotal";

    public const string ReasonKey = "reason";

    public static Dictionary<string, object> BuildUnsupportedFormatProperties(IReadOnlyDictionary<string, int> counts)
    {
        Dictionary<string, object> properties = [];
        int total = 0;

        foreach (string extension in FolderValidator.UnsupportedAudioExtensions.Order(StringComparer.Ordinal))
        {
            string key = extension.ToLowerInvariant();
            int count = counts.GetValueOrDefault(key);
            properties[key.TrimStart('.')] = count;
            total += count;
        }

        properties[UnsupportedTotalKey] = total;

        return properties;
    }

    public static Dictionary<string, object> BuildRadioFallbackProperties(string reason) =>
        new() { [ReasonKey] = reason };
}