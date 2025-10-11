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
            value = value[0].ToString().ToUpper() + value.Substring(1).ToLower();
        }
        return value;
    }


    public static string GetNameFirstLetter(string name)
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
        return string.Compare(value, value2, true) != 0;
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
    public static bool AreDifferents(this string? a, string? b) => (a ?? "") != (b ?? "");

}
