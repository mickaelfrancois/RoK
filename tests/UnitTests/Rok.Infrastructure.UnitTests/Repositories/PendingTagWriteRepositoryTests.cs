using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using Rok.Application.Interfaces.Repositories;
using Rok.Domain.Enums;
using Rok.Infrastructure.Repositories;

namespace Rok.Infrastructure.UnitTests.Repositories;

public class PendingTagWriteRepositoryTests
{
    private static PendingTagWriteRepository CreateRepository(SqliteDatabaseFixture fixture) =>
        new(fixture.Connection, NullLogger<PendingTagWriteRepository>.Instance);

    [Fact(DisplayName = "pending_rows_round_trip_by_album")]
    public async Task AddAsync_ThenGetByAlbum_ReturnsTheRowsOfThatAlbumOnly()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        PendingTagWriteRepository repo = CreateRepository(fixture);

        // Act
        await repo.AddAsync(1, ETagWriteFields.Album | ETagWriteFields.Genre);
        await repo.AddAsync(3, ETagWriteFields.Disc);
        IReadOnlyList<PendingTagWrite> album1 = await repo.GetByAlbumAsync(1);
        IReadOnlyList<PendingTagWrite> album2 = await repo.GetByAlbumAsync(2);

        // Assert
        PendingTagWrite row = Assert.Single(album1);
        Assert.Equal(new PendingTagWrite(1, ETagWriteFields.Album | ETagWriteFields.Genre), row);
        Assert.Equal(new PendingTagWrite(3, ETagWriteFields.Disc), Assert.Single(album2));
        Assert.Equal(1, await repo.CountByAlbumAsync(1));
        Assert.Equal(0, await repo.CountByAlbumAsync(3));
    }

    [Fact(DisplayName = "pending_insert_ors_the_fields_of_an_existing_row")]
    public async Task AddAsync_ExistingRow_OrsTheFields()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        PendingTagWriteRepository repo = CreateRepository(fixture);
        await repo.AddAsync(1, ETagWriteFields.Album);

        // Act
        await repo.AddAsync(1, ETagWriteFields.Disc);
        await repo.AddAsync(1, ETagWriteFields.Album);

        // Assert
        PendingTagWrite row = Assert.Single(await repo.GetByAlbumAsync(1));
        Assert.Equal(ETagWriteFields.Album | ETagWriteFields.Disc, row.Fields);
        Assert.Equal(1, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM pendingTagWrites"));
    }

    [Fact(DisplayName = "pending_insert_without_fields_stores_nothing")]
    public async Task AddAsync_NoFields_StoresNothing()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        PendingTagWriteRepository repo = CreateRepository(fixture);

        // Act
        await repo.AddAsync(1, ETagWriteFields.None);

        // Assert
        Assert.Equal(0, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM pendingTagWrites"));
    }

    [Fact(DisplayName = "pending_insert_joins_the_open_transaction_and_rolls_back_with_it")]
    public async Task AddAsync_InsideTransaction_RollsBackWithIt()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        PendingTagWriteRepository repo = CreateRepository(fixture);
        using var transaction = fixture.Connection.BeginTransaction();

        // Act
        await repo.AddAsync(1, ETagWriteFields.Artist, transaction);
        transaction.Rollback();

        // Assert
        Assert.Equal(0, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM pendingTagWrites"));
    }

    [Fact(DisplayName = "complete_updates_file_info_and_deletes_the_row")]
    public async Task CompleteAsync_UpdatesFileInfoAndDeletesTheRow()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        PendingTagWriteRepository repo = CreateRepository(fixture);
        await repo.AddAsync(1, ETagWriteFields.Album);
        await repo.AddAsync(2, ETagWriteFields.Album);
        DateTime newDate = new(2031, 2, 3, 4, 5, 6, DateTimeKind.Utc);

        // Act
        await repo.CompleteAsync(1, newDate, 777);

        // Assert
        PendingTagWrite remaining = Assert.Single(await repo.GetByAlbumAsync(1));
        Assert.Equal(2, remaining.TrackId);
        Assert.Equal(777, fixture.Connection.ExecuteScalar<long>("SELECT size FROM tracks WHERE id = 1"));
        Assert.Equal(1200, fixture.Connection.ExecuteScalar<long>("SELECT size FROM tracks WHERE id = 2"));
        Assert.Equal(newDate, DateTime.Parse(fixture.Connection.ExecuteScalar<string>("SELECT fileDate FROM tracks WHERE id = 1")!, null, System.Globalization.DateTimeStyles.AdjustToUniversal));
    }

    [Fact(DisplayName = "orphan_rows_are_ignored_and_purged")]
    public async Task OrphanRows_AreIgnoredByQueriesAndPurged()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        PendingTagWriteRepository repo = CreateRepository(fixture);
        await repo.AddAsync(1, ETagWriteFields.Album);
        fixture.Connection.Execute("PRAGMA foreign_keys = OFF");
        fixture.Connection.Execute("INSERT INTO pendingTagWrites(trackId, fields) VALUES (9999, 8)");
        fixture.Connection.Execute("PRAGMA foreign_keys = ON");

        // Act
        int countBefore = await repo.CountByAlbumAsync(1);
        int purged = await repo.DeleteOrphansAsync();

        // Assert
        Assert.Equal(1, countBefore);
        Assert.Equal(1, purged);
        Assert.Equal(1, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM pendingTagWrites"));
        Assert.Equal(1, fixture.Connection.ExecuteScalar<long>("SELECT trackId FROM pendingTagWrites"));
    }

    [Fact(DisplayName = "pending_repository_rejects_a_non_positive_id")]
    public async Task NonPositiveId_Throws()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        PendingTagWriteRepository repo = CreateRepository(fixture);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repo.AddAsync(0, ETagWriteFields.Album));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repo.GetByAlbumAsync(0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repo.CompleteAsync(0, DateTime.UtcNow, 1));
    }
}