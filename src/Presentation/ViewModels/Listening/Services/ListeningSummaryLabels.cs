namespace Rok.ViewModels.Listening.Services;

/// <summary>Localized composite formats used to render the queue summary.</summary>
public sealed record ListeningSummaryLabels(
    string TrackSingular,
    string TrackPlural,
    string HoursMinutes,
    string Hours,
    string Minutes,
    string EndsAt);