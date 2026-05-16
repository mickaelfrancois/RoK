namespace Rok.ViewModels.Insights;

public record HeatmapRowViewModel(string DayLabel, IReadOnlyList<HeatmapCellViewModel> Cells);