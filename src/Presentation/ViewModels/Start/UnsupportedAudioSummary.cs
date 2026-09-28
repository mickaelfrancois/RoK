using System.Globalization;

namespace Rok.ViewModels.Start;

public static class UnsupportedAudioSummary
{
    public static IReadOnlyDictionary<string, int> Merge(IEnumerable<IReadOnlyDictionary<string, int>> counts)
    {
        Dictionary<string, int> merged = new(StringComparer.Ordinal);

        foreach (IReadOnlyDictionary<string, int> folderCounts in counts)
        {
            foreach ((string extension, int count) in folderCounts)
                merged[extension] = merged.GetValueOrDefault(extension) + count;
        }

        return merged;
    }

    public static bool TryGetDominant(IReadOnlyDictionary<string, int> counts, out string extension, out int count)
    {
        extension = string.Empty;
        count = 0;

        foreach ((string candidate, int candidateCount) in counts.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (candidateCount > count)
            {
                extension = candidate;
                count = candidateCount;
            }
        }

        return count > 0;
    }

    /// <summary>
    /// Builds the onboarding banner text. Templates take the file count as {0} and the extension as {1}.
    /// </summary>
    public static string BuildBannerMessage(string pluralTemplate, string singularTemplate, string fallbackMessage, IReadOnlyDictionary<string, int> counts)
    {
        if (!TryGetDominant(counts, out string extension, out int count))
            return fallbackMessage;

        string template = count == 1 ? singularTemplate : pluralTemplate;

        return string.Format(CultureInfo.CurrentCulture, template, count, extension);
    }
}