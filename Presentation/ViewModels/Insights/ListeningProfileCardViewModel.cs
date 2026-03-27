namespace Rok.ViewModels.Insights;

public record ListeningProfileCardViewModel(
    string Icon,
    string Label,
    string Description,
    string SkipRateText,
    string ReplayRateText,
    string DiversityText
);
