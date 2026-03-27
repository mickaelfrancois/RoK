using CommunityToolkit.Mvvm.ComponentModel;
using Rok.Application.Features.Insights.Query;

namespace Rok.ViewModels.Insights;

public record HeatmapRowViewModel(string DayLabel, IReadOnlyList<HeatmapCellViewModel> Cells);
public record HeatmapCellViewModel(int Hour, int Count, double Intensity);
public record HourLabelViewModel(string Label);


public partial class InsightsViewModel(IMediator mediator) : ObservableObject
{
    private static readonly string[] _dayLabels = new string[] { "Lun", "Mar", "Mer", "Jeu", "Ven", "Sam", "Dim" };

    public InsightsDto Insights { get; private set; } = new InsightsDto();

    public IReadOnlyList<HeatmapRowViewModel> HeatmapRows { get; private set; } = new List<HeatmapRowViewModel>();

    public IReadOnlyList<HourLabelViewModel> HourAxisLabels { get; } = Enumerable.Range(0, 24)
        .Select(h => new HourLabelViewModel(h % 4 == 0 ? $"{h}h" : string.Empty))
        .ToList();

    public string SessionDiffText =>
        Insights.DifferenceSessionCount >= 0
            ? $" (+{Insights.DifferenceSessionCount})"
            : $" ({Insights.DifferenceSessionCount})";

    public string PlayDurationDiffText =>
        Insights.DifferencePlayDuration >= 0
            ? $" (+{FormatSeconds(Insights.DifferencePlayDuration)})"
            : $" (-{FormatSeconds(Insights.DifferencePlayDuration)})";

    public string TracksPlayedDiffText =>
        Insights.DifferenceTracksPlayed >= 0
            ? $" (+{Insights.DifferenceTracksPlayed})"
            : $" ({Insights.DifferenceTracksPlayed})";

    public string ArtistsPlayedDiffText =>
        Insights.DifferenceArtistsPlayed >= 0
            ? $" (+{Insights.DifferenceArtistsPlayed})"
            : $" ({Insights.DifferenceArtistsPlayed})";

    public string AlbumsPlayedDiffText =>
        Insights.DifferenceAlbumsPlayed >= 0
            ? $" (+{Insights.DifferenceAlbumsPlayed})"
            : $" ({Insights.DifferenceAlbumsPlayed})";

    public string GenresPlayedDiffText =>
        Insights.DifferenceGenresPlayed >= 0
            ? $" (+{Insights.DifferenceGenresPlayed})"
            : $" ({Insights.DifferenceGenresPlayed})";


    public async Task LoadDataAsync()
    {
        Insights = await mediator.SendMessageAsync(new GetInsightsQuery() { Month = DateTime.UtcNow });
        HeatmapRows = BuildHeatmapRows(Insights.HeatmapCells);
        OnPropertyChanged(nameof(Insights));
        OnPropertyChanged(nameof(HeatmapRows));
    }

    private static IReadOnlyList<HeatmapRowViewModel> BuildHeatmapRows(IReadOnlyList<HeatmapCellDto> cells)
    {
        int maxCount = cells.Count > 0 ? cells.Max(c => c.Count) : 1;
        if (maxCount == 0) maxCount = 1;

        return Enumerable.Range(0, 7)
            .Select(day => new HeatmapRowViewModel(
                _dayLabels[day],
                Enumerable.Range(0, 24)
                    .Select(hour =>
                    {
                        int count = cells.FirstOrDefault(c => c.DayOfWeek == day && c.Hour == hour)?.Count ?? 0;
                        return new HeatmapCellViewModel(hour, count, (double)count / maxCount);
                    })
                    .ToList()
            ))
            .ToList();
    }

    private static string FormatSeconds(double seconds)
    {
        TimeSpan t = TimeSpan.FromSeconds(Math.Abs(seconds));
        return $"{(int)t.TotalDays:D2}:{t.Hours:D2}:{t.Minutes:D2}";
    }
}
