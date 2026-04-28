using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using Rok.Domain.Entities;
using Rok.Infrastructure.Repositories;

namespace Rok.Infrastructure.UnitTests.Repositories;

public class TrackRepositoryGetByFilePathTests
{
    private static SqliteDatabaseFixture CreateFixture()
    {
        SqliteDatabaseFixture fixture = new();
        // Insert a known file path on top of the seeded tracks
        fixture.Connection.Execute(@"
            INSERT INTO Tracks(
                id, title, duration, size, bitrate, musicFile, fileDate, isLive, score, listenCount, skipCount, creatDate, albumId, artistId, trackNumber
            ) VALUES (
                100, 'PathTrack', 180, 1000, 128, @path, @now, 0, 0, 0, 0, @now, 1, 1, 1
            )", new { path = @"D:\Music\Daft Punk\Discovery\01 - One More Time.mp3", now = DateTime.UtcNow });
        return fixture;
    }

    private static TrackRepository CreateRepository(SqliteDatabaseFixture fixture)
        => new(fixture.Connection, fixture.Connection, NullLogger<TrackRepository>.Instance);

    [Fact(DisplayName = "returns_track_when_file_path_matches_exactly")]
    public async Task Returns_track_when_file_path_matches_exactly()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        TrackRepository repo = CreateRepository(fixture);

        // Act
        TrackEntity? track = await repo.GetByFilePathAsync(@"D:\Music\Daft Punk\Discovery\01 - One More Time.mp3", CancellationToken.None);

        // Assert
        Assert.NotNull(track);
        Assert.Equal(100, track.Id);
    }

    [Fact(DisplayName = "returns_track_when_file_path_matches_case_insensitive")]
    public async Task Returns_track_when_file_path_matches_case_insensitive()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        TrackRepository repo = CreateRepository(fixture);

        // Act
        TrackEntity? track = await repo.GetByFilePathAsync(@"d:\music\daft punk\discovery\01 - one more time.mp3", CancellationToken.None);

        // Assert
        Assert.NotNull(track);
        Assert.Equal(100, track.Id);
    }

    [Fact(DisplayName = "returns_null_when_no_match")]
    public async Task Returns_null_when_no_match()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        TrackRepository repo = CreateRepository(fixture);

        // Act
        TrackEntity? track = await repo.GetByFilePathAsync(@"D:\Nope\nope.mp3", CancellationToken.None);

        // Assert
        Assert.Null(track);
    }
}
