using System.Globalization;

namespace Rok.Shared;

public static class LanguageHelpers
{
    public static string GetCurrentLanguage()
    {
        return CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
    }

    public static string NormalizeLanguageForLibreTranslate(string? languageTag, string defaultLang = "fr")
    {
        if (string.IsNullOrWhiteSpace(languageTag))
            return defaultLang;

        try
        {
            CultureInfo culture = new(languageTag);
            return culture.TwoLetterISOLanguageName.ToLowerInvariant();
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