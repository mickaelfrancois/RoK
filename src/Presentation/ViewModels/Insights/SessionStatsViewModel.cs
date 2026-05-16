namespace Rok.ViewModels.Insights;

public record SessionStatsViewModel(
    string MaxDurationText,
    string AvgTracksText,
    string NocturnalText,
    string MostCommonStartHourText,
    string MostIntenseSessionText,
    string MostActiveDayText
);