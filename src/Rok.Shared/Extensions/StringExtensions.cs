using System.Globalization;
using System.Text;

namespace Rok.Shared.Extensions;

public static class StringExtensions
{
    public static string ToFileName(this string value)
    {
        char[] invalids = Path.GetInvalidPathChars();
        string newName = String.Join("_", value.Split(invalids, StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.').Replace("/", "").Replace("?", "").Replace(":", "");

        invalids = Path.GetInvalidFileNameChars();
        newName = String.Join("_", newName.Split(invalids, StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');

        return newName;
    }

    public static string Capitalize(this string value)
    {
        if (value.Length > 1)
        {
            value = value[0].ToString().ToUpper() + value[1..].ToLower();
        }
        return value;
    }


    public static string GetNameFirstLetter(this string? name)
    {
        string result = "#123";

        if (string.IsNullOrEmpty(name) == false)
        {
            result = name[..1].ToUpperInvariant();

            if (char.IsLetter(result, 0) == false)
                result = "#123";
        }

        return result;
    }


    public static bool IsDifferent(this string value, string value2)
    {
        return string.Compare(value, value2, StringComparison.OrdinalIgnoreCase) != 0;
    }


    /// <summary>
    /// Determines whether two specified strings are equal, treating null as an empty string.
    /// </summary>
    /// <param name="a">The first string to compare, or <see langword="null"/>.</param>
    /// <param name="b">The second string to compare, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the strings are equal or both are <see langword="null"/>; otherwise, <see
    /// langword="false"/>.</returns>
    public static bool AreEquals(this string? a, string? b) => (a ?? "") == (b ?? "");

    /// <summary>
    /// Determines whether two strings are different, treating null as an empty string.
    /// </summary>
    /// <param name="a">The first string to compare. Can be <see langword="null"/>.</param>
    /// <param name="b">The second string to compare. Can be <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the strings are different; otherwise, <see langword="false"/>.</returns>
    public static bool AreDifferent(this string? a, string? b) => (a ?? "") != (b ?? "");

    /// <summary>
    /// Normalizes a name used as a case-insensitive index or lookup key.
    /// Trims surrounding whitespace, applies Unicode NFC composition, folds the
    /// common dash and hyphen variants (U+2010 to U+2015, U+2212, U+FE58, U+FE63, U+FF0D)
    /// to ASCII '-' (U+002D), and folds non-breaking and zero-width spaces
    /// (U+00A0, U+202F, U+2007, U+200B) to a regular space.
    /// Returns the input unchanged if null or empty.
    /// </summary>
    public static string NormalizeIndexedName(this string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        string trimmed = value.Trim();

        if (trimmed.Length == 0)
            return trimmed;

        string normalized = trimmed.Normalize(NormalizationForm.FormC);

        StringBuilder sb = new(normalized.Length);

        foreach (char c in normalized)
        {
            char folded = c switch
            {
                '‐' or '‑' or '‒' or '–' or '—' or '―' or '−' or '﹘' or '﹣' or '－' => '-',
                ' ' or ' ' or ' ' or '​' => ' ',
                _ => c
            };
            sb.Append(folded);
        }

        return sb.ToString();
    }
}