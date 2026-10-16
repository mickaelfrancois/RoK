using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using Rok.Infrastructure.Repositories;

namespace Rok.Infrastructure.UnitTests.Repositories;

public class TrackRepositoryFileInfoTests
{
    private sealed record TrackRow(string Title, long Duration, long Score, long? TrackNumber, string MusicFile, string FileDate, long Size, string? EditDate);

    private static TrackRow ReadRow(SqliteDatabaseFixture fixture, long id) =>
        fixture.Connection.QuerySingle<TrackRow>("SELECT title, duration, score, trackNumber, musicFile, fileDate, size, editDate FROM tracks WHERE id = @id", new { id });

    [Fact(DisplayName = "update_file_info_touches_only_date_and_size")]
    public async Task UpdateFileInfoAsync_TouchesOnlyDateAndSize()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        TrackRepository repo = new(fixture.Connection, fixture.Connection, NullLogger<TrackRepository>.Instance, TimeProvider.System);
        TrackRow before = ReadRow(fixture, 1);
        DateTime newDate = new(2030, 1, 2, 3, 4, 5, DateTimeKind.Utc);

        // Act
        bool updated = await repo.UpdateFileInfoAsync(1, newDate, 4242);

        // Assert
        Assert.True(updated);
        TrackRow after = ReadRow(fixture, 1);
        Assert.Equal(4242, after.Size);
        Assert.NotEqual(before.FileDate, after.FileDate);
        Assert.Equal(newDate, DateTime.Parse(after.FileDate, null, System.Globalization.DateTimeStyles.AdjustToUniversal));
        Assert.Equal(before with { FileDate = after.FileDate, Size = 4242 }, after);
        Assert.Equal(1200, ReadRow(fixture, 2).Size);
    }

    [Fact(DisplayName = "update_file_info_rejects_a_non_positive_id")]
    public async Task UpdateFileInfoAsync_NonPositiveId_Throws()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        TrackRepository repo = new(fixture.Connection, fixture.Connection, NullLogger<TrackRepository>.Instance, TimeProvider.System);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repo.UpdateFileInfoAsync(0, DateTime.UtcNow, 1));
    }
}