using Rok.Application.Dto;
using Rok.Application.Mapping;
using Rok.Domain.Entities;
using Rok.MetadataTool;

namespace Rok.ApplicationTests.Tools;

public class MixScanTracksTests
{
    private static TrackDto Dto(long id, bool live = false, bool albumLive = false) =>
        new() { Id = id, Title = $"T{id}", MusicFile = $"{id}.mp3", IsLive = live, IsAlbumLive = albumLive };

    [Fact(DisplayName = "scan_dto_matches_the_app_mapping_for_analysis_fields")]
    public void ToDto_MatchesTheAppMapping()
    {
        // Arrange
        var entity = new TrackEntity
        {
            Id = 42,
            Title = "Song",
            MusicFile = @"C:\music\song.mp3",
            FileDate = new DateTime(2026, 2, 3, 4, 5, 6, DateTimeKind.Utc),
            Size = 123456,
            Bpm = 128,
            IsLive = true,
            IsAlbumLive = true,
        };

        // Act
        var scan = MixScanTracks.ToDto(entity);
        var app = TrackDtoMapping.Map(entity);

        // Assert
        Assert.Equal(app.Id, scan.Id);
        Assert.Equal(app.Title, scan.Title);
        Assert.Equal(app.MusicFile, scan.MusicFile);
        Assert.Equal(app.FileDate, scan.FileDate);
        Assert.Equal(app.Size, scan.Size);
        Assert.Equal(app.Bpm, scan.Bpm);
        Assert.Equal(app.IsLive, scan.IsLive);
        Assert.Equal(app.IsAlbumLive, scan.IsAlbumLive);
    }

    [Fact(DisplayName = "live_tracks_are_excluded_from_the_scan")]
    public void Select_ExcludesLiveTracks()
    {
        // Arrange
        TrackDto[] all = [Dto(1), Dto(2, live: true), Dto(3, albumLive: true), Dto(4)];

        // Act
        var selected = MixScanTracks.Select(all, null);

        // Assert
        Assert.Equal([1L, 4L], selected.Select(t => t.Id));
    }

    [Fact(DisplayName = "limit_counts_analysable_tracks_in_id_order")]
    public void Select_Limit_CountsAnalysableTracksInIdOrder()
    {
        // Arrange
        TrackDto[] all = [Dto(5), Dto(1, live: true), Dto(3), Dto(2, albumLive: true), Dto(4), Dto(6)];

        // Act
        var selected = MixScanTracks.Select(all, 2);

        // Assert
        Assert.Equal([3L, 4L], selected.Select(t => t.Id));
    }
}