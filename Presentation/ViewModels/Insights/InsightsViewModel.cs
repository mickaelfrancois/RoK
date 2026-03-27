using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Rok.Application.Features.Insights.Query;

namespace Rok.ViewModels.Insights;

public partial class InsightsViewModel(IMediator mediator, IResourceService resourceLoader) : ObservableObject
{
    private static readonly string[] _dayLabels = Enumerable.Range(1, 7)
        .Select(i => CultureInfo.CurrentUICulture.DateTimeFormat.AbbreviatedDayNames[i % 7])
        .ToArray();

    public InsightsDto Insights { get; private set; } = new InsightsDto();

    public IReadOnlyList<HeatmapRowViewModel> HeatmapRows { get; private set; } = new List<HeatmapRowViewModel>();

    public ListeningProfileCardViewModel ListeningProfileCard { get; private set; } = BuildProfileCard(new InsightsDto(), resourceLoader);

    public IReadOnlyList<BadgeViewModel> Badges { get; private set; } = new List<BadgeViewModel>();

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
        ListeningProfileCard = BuildProfileCard(Insights, resourceLoader);
        Badges = BuildBadgeViewModels(Insights.Badges);

        OnPropertyChanged(nameof(Insights));
        OnPropertyChanged(nameof(HeatmapRows));
        OnPropertyChanged(nameof(ListeningProfileCard));
        OnPropertyChanged(nameof(Badges));
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

    private static ListeningProfileCardViewModel BuildProfileCard(InsightsDto insights, IResourceService resourceLoader)
    {
        (string icon, string label, string description) = insights.ListeningProfile switch
        {
            ListeningProfile.CuriousExplorer => ("🧭", resourceLoader.GetString("Insights_ProfileCuriousExplorer"),
                resourceLoader.GetString("Insights_ProfileCuriousExplorerDescription")),

            ListeningProfile.FaithfulIntense => ("❤️", resourceLoader.GetString("Insights_ProfileFaithfulIntense"),
               resourceLoader.GetString("Insights_ProfileFaithfulIntenseDescription")),

            ListeningProfile.NightOwl => ("🌙", resourceLoader.GetString("Insights_ProfileNightOwl"),
              resourceLoader.GetString("Insights_ProfileNightOwlDescription")),

            ListeningProfile.FocusMode => ("🎯", resourceLoader.GetString("Insights_ProfileFocusMode"),
               resourceLoader.GetString("Insights_ProfileFocusModeDescription")),

            ListeningProfile.ChannelSurfer => ("⚡", resourceLoader.GetString("Insights_ProfileChannelSurfer"),
              resourceLoader.GetString("Insights_ProfileChannelSurferDescription")),

            _ => ("🎵", string.Empty, string.Empty)
        };

        return new ListeningProfileCardViewModel(
            Icon: icon,
            Label: label,
            Description: description,
            SkipRateText: $"{insights.SkipRate:F0}%",
            ReplayRateText: $"{insights.ReplayRate:F0}%",
            DiversityText: $"{insights.ArtistsPlayed} " + resourceLoader.GetString("Insights_ArtistByMonth")
        );
    }

    private IReadOnlyList<BadgeViewModel> BuildBadgeViewModels(IReadOnlyList<BadgeDto> badges)
    {
        return badges.Select(b =>
        {
            (string name, string description) = b.Id switch
            {
                Badge.SmoothListener => (resourceLoader.GetString("Insights_BadgeSmoothListenerName"), resourceLoader.GetString("Insights_BadgeSmoothListenerDescription")),
                Badge.LowSkip => (resourceLoader.GetString("Insights_BadgeLowSkipName"), resourceLoader.GetString("Insights_BadgeLowSkipDescription")),
                Badge.HyperZapper => (resourceLoader.GetString("Insights_BadgeHyperZapperName"), resourceLoader.GetString("Insights_BadgeHyperZapperDescription")),
                Badge.Zapper => (resourceLoader.GetString("Insights_BadgeZapperName"), resourceLoader.GetString("Insights_BadgeZapperDescription")),
                Badge.Obsessed => (resourceLoader.GetString("Insights_BadgeObsessedName"), resourceLoader.GetString("Insights_BadgeObsessedDescription")),
                Badge.ReplayLover => (resourceLoader.GetString("Insights_BadgeReplayLoverName"), resourceLoader.GetString("Insights_BadgeReplayLoverDescription")),
                Badge.FreshSeeker => (resourceLoader.GetString("Insights_BadgeFreshSeekerName"), resourceLoader.GetString("Insights_BadgeFreshSeekerDescription")),
                Badge.Explorer => (resourceLoader.GetString("Insights_BadgeExplorerName"), resourceLoader.GetString("Insights_BadgeExplorerDescription")),
                Badge.Curious => (resourceLoader.GetString("Insights_BadgeCuriousName"), resourceLoader.GetString("Insights_BadgeCuriousDescription")),
                Badge.RestrictedCircle => (resourceLoader.GetString("Insights_BadgeRestrictedCircleName"), resourceLoader.GetString("Insights_BadgeRestrictedCircleDescription")),
                Badge.UltraFocus => (resourceLoader.GetString("Insights_BadgeUltraFocusName"), resourceLoader.GetString("Insights_BadgeUltraFocusDescription")),
                Badge.DeepListener => (resourceLoader.GetString("Insights_BadgeDeepListenerName"), resourceLoader.GetString("Insights_BadgeDeepListenerDescription")),
                Badge.LongPlayer => (resourceLoader.GetString("Insights_BadgeLongPlayerName"), resourceLoader.GetString("Insights_BadgeLongPlayerDescription")),
                Badge.ShortSessions => (resourceLoader.GetString("Insights_BadgeShortSessionsName"), resourceLoader.GetString("Insights_BadgeShortSessionsDescription")),
                Badge.NightOwl => (resourceLoader.GetString("Insights_BadgeNightOwlName"), resourceLoader.GetString("Insights_BadgeNightOwlDescription")),
                Badge.Nocturne => (resourceLoader.GetString("Insights_BadgeNocturneName"), resourceLoader.GetString("Insights_BadgeNocturneDescription")),
                Badge.EarlyBird => (resourceLoader.GetString("Insights_BadgeEarlyBirdName"), resourceLoader.GetString("Insights_BadgeEarlyBirdDescription")),
                Badge.Afterwork => (resourceLoader.GetString("Insights_BadgeAfterworkName"), resourceLoader.GetString("Insights_BadgeAfterworkDescription")),
                Badge.UltraLoyal => (resourceLoader.GetString("Insights_BadgeUltraLoyalName"), resourceLoader.GetString("Insights_BadgeUltraLoyalDescription")),
                Badge.Loyal => (resourceLoader.GetString("Insights_BadgeLoyalName"), resourceLoader.GetString("Insights_BadgeLoyalDescription")),
                Badge.Eclectic => (resourceLoader.GetString("Insights_BadgeEclecticName"), resourceLoader.GetString("Insights_BadgeEclecticDescription")),
                _ => (string.Empty, string.Empty)
            };
            return new BadgeViewModel(b.Icon, name, description);
        }).ToList();
    }

    private static string FormatSeconds(double seconds)
    {
        TimeSpan t = TimeSpan.FromSeconds(Math.Abs(seconds));
        return $"{(int)t.TotalDays:D2}:{t.Hours:D2}:{t.Minutes:D2}";
    }
}
