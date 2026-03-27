using CommunityToolkit.Mvvm.ComponentModel;
using Rok.Application.Features.Insights.Query;

namespace Rok.ViewModels.Insights;

public record HeatmapRowViewModel(string DayLabel, IReadOnlyList<HeatmapCellViewModel> Cells);
public record HeatmapCellViewModel(int Hour, int Count, double Intensity);
public record HourLabelViewModel(string Label);
public record ListeningProfileCardViewModel(
    string Icon,
    string Label,
    string Description,
    string SkipRateText,
    string ReplayRateText,
    string DiversityText
);

public record BadgeViewModel(string Icon, string Name, string Description);

public partial class InsightsViewModel(IMediator mediator) : ObservableObject
{
    private static readonly string[] _dayLabels = new string[] { "Lun", "Mar", "Mer", "Jeu", "Ven", "Sam", "Dim" };

    public InsightsDto Insights { get; private set; } = new InsightsDto();

    public IReadOnlyList<HeatmapRowViewModel> HeatmapRows { get; private set; } = new List<HeatmapRowViewModel>();

    public ListeningProfileCardViewModel ListeningProfileCard { get; private set; } = BuildProfileCard(new InsightsDto());

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
        ListeningProfileCard = BuildProfileCard(Insights);
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

    private static ListeningProfileCardViewModel BuildProfileCard(InsightsDto insights)
    {
        (string icon, string label, string description) = insights.ListeningProfile switch
        {
            ListeningProfile.CuriousExplorer => ("🧭", "Explorateur curieux",
                "Tu explores sans cesse de nouveaux artistes,\ntu t'attardes rarement sur les mêmes morceaux."),
            ListeningProfile.FaithfulIntense => ("❤️", "Fidèle intense",
                "Tu écoutes longtemps, tu skippes peu,\net tu reviens souvent sur les mêmes morceaux."),
            ListeningProfile.Night => ("🌙", "Nocturne",
                "Tes sessions d'écoute se concentrent la nuit,\nquand le silence laisse place à la musique."),
            ListeningProfile.FocusMode => ("🎯", "Focus mode",
                "Tu écoutes en profondeur, peu d'artistes différents,\net tu vas jusqu'au bout de tes sessions."),
            ListeningProfile.ChannelSurfer => ("⚡", "Zappeur",
                "Tu explores rapidement, tu skipes beaucoup,\ntoujours à la recherche du prochain morceau parfait."),
            _ => ("🎵", string.Empty, string.Empty)
        };

        return new ListeningProfileCardViewModel(
            Icon: icon,
            Label: label,
            Description: description,
            SkipRateText: $"{insights.SkipRate:F0}%",
            ReplayRateText: $"{insights.ReplayRate:F0}%",
            DiversityText: $"{insights.ArtistsPlayed} artistes / mois"
        );
    }
    private static IReadOnlyList<BadgeViewModel> BuildBadgeViewModels(IReadOnlyList<BadgeDto> badges)
    {
        return badges.Select(b =>
        {
            (string name, string description) = b.Id switch
            {
                Badge.SmoothListener    => ("Smooth Listener",   "Écoute fluide, tu vas toujours au bout."),
                Badge.LowSkip           => ("Low Skip",           "Tu vas au bout des choses."),
                Badge.HyperZapper       => ("Hyper Zappeur",      "Toujours en quête du bon titre."),
                Badge.Zapper            => ("Zappeur",            "Tu changes souvent de morceau."),
                Badge.Obsessed          => ("Obsessed",           "Tu écoutes certains titres en boucle."),
                Badge.ReplayLover       => ("Replay Lover",       "Tu rejoues souvent tes favoris."),
                Badge.FreshSeeker       => ("Fresh Seeker",       "Tu ne reviens presque jamais en arrière."),
                Badge.Explorer          => ("Explorateur",        "Tu découvres beaucoup de nouveaux artistes."),
                Badge.Curious           => ("Curieux",            "Tu explores régulièrement."),
                Badge.RestrictedCircle  => ("Cercle Restreint",   "Tu restes dans ton univers."),
                Badge.UltraFocus        => ("Ultra Focus",        "Tu écoutes très peu d'artistes différents."),
                Badge.DeepListener      => ("Deep Listener",      "Tu te plonges vraiment dans la musique."),
                Badge.LongPlayer        => ("Long Player",        "Tu écoutes longtemps."),
                Badge.ShortSessions     => ("Short Sessions",     "Tu écoutes par petites touches."),
                Badge.NightOwl          => ("Night Owl",          "Tu vis la nuit."),
                Badge.Nocturne          => ("Nocturne",           "Tu écoutes surtout la nuit."),
                Badge.EarlyBird         => ("Matinal",            "Tu commences ta journée en musique."),
                Badge.Afterwork         => ("Afterwork",          "Tu écoutes en fin de journée."),
                Badge.UltraLoyal        => ("Ultra Fidèle",       "Tu as tes titres fétiches."),
                Badge.Loyal             => ("Fidèle",             "Tu restes fidèle à tes favoris."),
                Badge.Eclectic          => ("Éclectique",         "Tu changes souvent de titres."),
                _                       => (string.Empty,         string.Empty)
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
