using Rok.Application.Interfaces.Repositories;

namespace Rok.Application.Features.Insights.Query;

public class GetInsightsQuery : IQuery<InsightsDto>
{
    public DateTime Month { get; init; }
}


internal class GetInsightsQueryHandler(IListeningEventRepository repository) : IQueryHandler<GetInsightsQuery, InsightsDto>
{
    public async Task<InsightsDto> HandleAsync(GetInsightsQuery request, CancellationToken cancellationToken)
    {
        return await repository.GetInsightsAsync(request.Month);
    }
}

public enum ListeningProfile
{
    Unknown,
    CuriousExplorer,
    FaithfulIntense,
    NightOwl,
    FocusMode,
    ChannelSurfer
}

public enum Badge
{
    SmoothListener,
    LowSkip,
    HyperZapper,
    Zapper,
    Obsessed,
    ReplayLover,
    FreshSeeker,
    Explorer,
    Curious,
    RestrictedCircle,
    UltraFocus,
    DeepListener,
    LongPlayer,
    ShortSessions,
    NightOwl,
    Nocturne,
    EarlyBird,
    Afterwork,
    UltraLoyal,
    Loyal,
    Eclectic
}

public record BadgeDto(Badge Id, string Icon);

public record InsightsDto
{
    public int SessionCount { get; init; }
    public int PreviousSessionCount { get; init; }
    public int DifferenceSessionCount => SessionCount - PreviousSessionCount;

    public int PlayDuration { get; init; }
    public int PreviousPlayDuration { get; init; }
    public int DifferencePlayDuration => PlayDuration - PreviousPlayDuration;

    public int TracksPlayed { get; init; }
    public int PreviousTracksPlayed { get; init; }
    public int DifferenceTracksPlayed => TracksPlayed - PreviousTracksPlayed;

    public int ArtistsPlayed { get; init; }
    public int PreviousArtistsPlayed { get; init; }
    public int DifferenceArtistsPlayed => ArtistsPlayed - PreviousArtistsPlayed;

    public int AlbumsPlayed { get; init; }
    public int PreviousAlbumsPlayed { get; init; }
    public int DifferenceAlbumsPlayed => AlbumsPlayed - PreviousAlbumsPlayed;

    public int GenresPlayed { get; init; }
    public int PreviousGenresPlayed { get; init; }
    public int DifferenceGenresPlayed => GenresPlayed - PreviousGenresPlayed;

    public double SkipRate { get; init; }
    public double ReplayRate { get; init; }
    public int LongSessionCount { get; init; }
    public int GlobalPeakHour { get; init; }
    public ListeningProfile ListeningProfile { get; init; } = ListeningProfile.Unknown;

    public IReadOnlyList<TrackInsightDto> TopTracks { get; init; } = new List<TrackInsightDto>();
    public IReadOnlyList<AlbumInsightDto> TopAlbums { get; init; } = new List<AlbumInsightDto>();
    public IReadOnlyList<GenreInsightDto> TopGenres { get; init; } = new List<GenreInsightDto>();
    public IReadOnlyList<HeatmapCellDto> HeatmapCells { get; init; } = new List<HeatmapCellDto>();
    public IReadOnlyList<BadgeDto> Badges { get; init; } = new List<BadgeDto>();
}

public class TrackInsightDto
{
    public string Title { get; set; } = string.Empty;
    public string ArtistName { get; set; } = string.Empty;
    public int PlayCount { get; set; }
    public int LongSessionCount { get; set; }
    public int PeakHour { get; set; } = -1;

    public string PeakHourRange => PeakHour >= 0
        ? $"{PeakHour:D2}h - {(PeakHour + 3) % 24:D2}h"
        : string.Empty;
}

public class AlbumInsightDto
{
    public string Title { get; set; } = string.Empty;
    public string ArtistName { get; set; } = string.Empty;
    public int PlayCount { get; set; }
    public int LongSessionCount { get; set; }
    public int PeakHour { get; set; } = -1;
    public string DominantTitle { get; set; } = string.Empty;

    public string PeakHourRange => PeakHour >= 0
        ? $"{PeakHour:D2}h - {(PeakHour + 3) % 24:D2}h"
        : string.Empty;
}

public class GenreInsightDto
{
    public int Rank { get; set; }
    public string GenreName { get; set; } = string.Empty;
    public int PlayCount { get; set; }
    public int LongSessionCount { get; set; }
    public int PeakHour { get; set; } = -1;
    public string DominantTitle { get; set; } = string.Empty;
    public double PlayPercentage { get; set; }

    public string RankText => $"{Rank}.";
    public string PlayPercentageText => $"{PlayPercentage:F0}%";

    public string PeakHourRange => PeakHour >= 0
        ? $"{PeakHour:D2}h - {(PeakHour + 3) % 24:D2}h"
        : string.Empty;
}

public record HeatmapCellDto
{
    public int DayOfWeek { get; init; }
    public int Hour { get; init; }
    public int Count { get; init; }
}