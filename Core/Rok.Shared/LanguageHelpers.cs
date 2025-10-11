using System.Globalization;

namespace Rok.Shared;

public static class LanguageHelpers
{
    public static string GetCurrentLanguage()
    {
        return CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
    }
}