using System.Globalization;
using Rok.Application.Features.Tracks;
using Rok.Application.Features.Tracks.Services;

namespace Rok.Services;

/// <summary>Pure rules of the track edit dialog.</summary>
public static class TrackEditRules
{
    private const string Empty = "—";

    /// <summary>Removes the leading and trailing white space of a typed text.</summary>
    /// <param name="text">The typed text.</param>
    /// <returns>The trimmed text, empty when <paramref name="text"/> is null.</returns>
    public static string Normalize(string? text)
    {
        return text?.Trim() ?? string.Empty;
    }

    /// <summary>Applies the rules to what is typed.</summary>
    /// <param name="current">The values the track has now.</param>
    /// <param name="input">The typed texts.</param>
    /// <returns>The state and preview of every field, the changes to save and whether they can be saved.</returns>
    public static TrackEditEvaluation Evaluate(TrackEditValues current, TrackEditInput input)
    {
        (TrackEditField title, string? newTitle) = EvaluateTitle(current.Title, input.Title);
        (TrackEditField trackNumber, int? newTrackNumber) = EvaluateNumber(current.TrackNumber, input.TrackNumber, TrackEditLimits.MaxTrackNumber, TrackEditTextKeys.InvalidTrackNumber);
        (TrackEditField disc, int? newDisc) = EvaluateNumber(current.Disc, input.Disc, TrackEditLimits.MaxDisc, TrackEditTextKeys.InvalidDisc);
        (TrackEditField composers, string? newComposers) = EvaluateComposers(current.Composers, input.Composers);
        (TrackEditField bpm, int? newBpm) = EvaluateBpm(current.Bpm, input.Bpm);

        TrackTagsEdit edit = new(
            current.TrackId,
            title.State == TrackEditFieldState.Changed ? newTitle : null,
            trackNumber.State == TrackEditFieldState.Changed ? newTrackNumber : null,
            disc.State == TrackEditFieldState.Changed ? newDisc : null,
            composers.State == TrackEditFieldState.Changed ? newComposers : null,
            bpm.State == TrackEditFieldState.Changed ? newBpm : null);

        return new TrackEditEvaluation(title, trackNumber, disc, composers, bpm, edit);
    }

    private static (TrackEditField Field, string? Value) EvaluateTitle(string currentTitle, string? typed)
    {
        string title = Normalize(typed);

        if (title.Length == 0)
            return (TrackEditField.Invalid(TrackEditTextKeys.Required), null);

        string old = Normalize(currentTitle);

        if (string.Equals(title, old, StringComparison.Ordinal))
            return (TrackEditField.Unchanged, null);

        return (TrackEditField.Changed(FormatPreview(old, title)), title);
    }

    private static (TrackEditField Field, int? Value) EvaluateNumber(int? currentValue, string? typed, int max, string invalidKey)
    {
        int? old = currentValue > 0 ? currentValue : null;
        string text = Normalize(typed);

        if (text.Length == 0)
            return (old is null ? TrackEditField.Unchanged : TrackEditField.Invalid(TrackEditTextKeys.Required), null);

        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int value) || value < 1 || value > max)
            return (TrackEditField.Invalid(invalidKey), null);

        if (value == old)
            return (TrackEditField.Unchanged, null);

        return (TrackEditField.Changed(FormatPreview(FormatNumber(old), value.ToString(CultureInfo.InvariantCulture))), value);
    }

    private static (TrackEditField Field, int? Value) EvaluateBpm(int? currentValue, string? typed)
    {
        int? old = currentValue > 0 ? currentValue : null;
        string text = Normalize(typed);

        if (text.Length == 0)
            return old is null ? (TrackEditField.Unchanged, null) : (TrackEditField.Changed(FormatPreview(FormatNumber(old), Empty)), 0);

        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int value) || value < 1 || value > TrackEditLimits.MaxBpm)
            return (TrackEditField.Invalid(TrackEditTextKeys.InvalidBpm), null);

        if (value == old)
            return (TrackEditField.Unchanged, null);

        return (TrackEditField.Changed(FormatPreview(FormatNumber(old), value.ToString(CultureInfo.InvariantCulture))), value);
    }

    private static (TrackEditField Field, string? Value) EvaluateComposers(string? currentValue, string? typed)
    {
        string old = Normalize(currentValue);
        string composers = Normalize(typed);

        if (string.Equals(composers, old, StringComparison.Ordinal))
            return (TrackEditField.Unchanged, null);

        return (TrackEditField.Changed(FormatPreview(old, composers)), composers);
    }

    private static string FormatNumber(int? value)
    {
        return value?.ToString(CultureInfo.InvariantCulture) ?? Empty;
    }

    private static string FormatPreview(string old, string value)
    {
        return $"{(old.Length == 0 ? Empty : old)} → {(value.Length == 0 ? Empty : value)}";
    }
}