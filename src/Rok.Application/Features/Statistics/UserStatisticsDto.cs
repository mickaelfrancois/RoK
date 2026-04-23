namespace Rok.Application.Features.Statistics;

public class NamedCount
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class TopItem : NamedCount
{
    public long Id { get; set; }

    public int ListenCount
    {
        get => Count;
        set => Count = value;
    }
}

public class UserStatisticsDto
{
    public long TotalTracks { get; set; }
    public long TotalSizeBytes { get; set; }
    public long TotalDurationSeconds { get; set; }

    public long TotalAlbums { get; set; }
    public long TotalArtists { get; set; }
    public long TotalGenres { get; set; }

    public long TracksListenedCount { get; set; }
    public long TracksNeverListenedCount { get; set; }

    public List<NamedCount> TracksByFileType { get; set; } = new();
    public List<NamedCount> AlbumsByType { get; set; } = new();
    public List<NamedCount> ArtistsByGenre { get; set; } = new();

    public List<TopItem> TopAlbums { get; set; } = new();
    public List<TopItem> TopArtists { get; set; } = new();
    public List<TopItem> TopTracks { get; set; } = new();
    public List<TopItem> TopGenres { get; set; } = new();
}
