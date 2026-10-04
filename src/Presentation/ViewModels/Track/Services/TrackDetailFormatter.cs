using System.Globalization;

namespace Rok.ViewModels.Track.Services;

/// <summary>Formats the optional values shown on the track page.</summary>
public static class TrackDetailFormatter
{
    public const string Missing = "—";

    /// <summary>Returns the date in the short date and time pattern of the culture, or a dash when unknown.</summary>
    public static string FormatDate(DateTime? date, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);

        return date.HasValue ? date.Value.ToString("g", culture) : Missing;
    }

    /// <summary>Returns the trimmed text, or a dash when it is empty.</summary>
    public static string FormatOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? Missing : value.Trim();
    }

    /// <summary>Joins the non-empty parts with a middle dot, in the same style as the other headers.</summary>
    public static string JoinSummary(params string?[] parts)
    {
        ArgumentNullException.ThrowIfNull(parts);

        return string.Join(" · ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }
}