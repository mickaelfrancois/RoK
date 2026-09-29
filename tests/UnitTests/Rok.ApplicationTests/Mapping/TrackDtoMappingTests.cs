using Rok.Application.Mapping;
using Rok.Domain.Entities;

namespace Rok.ApplicationTests.Mapping;

public class TrackDtoMappingTests
{
    [Fact(DisplayName = "map_copies_track_and_album_replay_gain")]
    public void Map_CopiesTrackAndAlbumReplayGain()
    {
        // Arrange
        TrackEntity entity = new()
        {
            Id = 1,
            Title = "t1",
            ReplayGainTrackGain = -7.25,
            ReplayGainTrackPeak = 0.98,
            ReplayGainAlbumGain = -6.5,
            ReplayGainAlbumPeak = 1.12
        };

        // Act
        TrackDto dto = TrackDtoMapping.Map(entity);

        // Assert
        Assert.Equal(-7.25, dto.ReplayGainTrackGain);
        Assert.Equal(0.98, dto.ReplayGainTrackPeak);
        Assert.Equal(-6.5, dto.ReplayGainAlbumGain);
        Assert.Equal(1.12, dto.ReplayGainAlbumPeak);
    }

    [Fact(DisplayName = "map_keeps_replay_gain_null_when_the_entity_has_none")]
    public void Map_KeepsReplayGainNull_WhenEntityHasNone()
    {
        // Act
        TrackDto dto = TrackDtoMapping.Map(new TrackEntity { Id = 2, Title = "t2" });

        // Assert
        Assert.Null(dto.ReplayGainTrackGain);
        Assert.Null(dto.ReplayGainTrackPeak);
        Assert.Null(dto.ReplayGainAlbumGain);
        Assert.Null(dto.ReplayGainAlbumPeak);
    }
}