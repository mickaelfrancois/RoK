namespace Rok.Application.Features.Insights.Query;

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
