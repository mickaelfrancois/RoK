using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Rok.Domain.Entities;
using Rok.Infrastructure.Repositories;

namespace Rok.Infrastructure.UnitTests.Repositories;

public class RadioStationRepositoryTests
{
    private static RadioStationRepository CreateRepository(SqliteDatabaseFixture fixture) =>
        new(fixture.Connection, NullLogger<RadioStationRepository>.Instance, TimeProvider.System);

    private static RadioStationEntity NewStation(string name = "Nova", string url = "https://stream.nova.fr/nova.mp3") =>
        new() { Name = name, StreamUrl = url, AddedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) };

    [Fact(DisplayName = "Add should persist a new station and return its id")]
    public async Task Add_ShouldPersistNewStation_AndReturnId()
    {
        using SqliteDatabaseFixture fixture = new();
        RadioStationRepository repo = CreateRepository(fixture);

        long id = await repo.AddAsync(NewStation(), CancellationToken.None);

        Assert.True(id > 0);
        int count = await fixture.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM RadioStations");
        Assert.Equal(1, count);
    }

    [Fact(DisplayName = "Add should throw SqliteException on duplicate StreamUrl")]
    public async Task Add_ShouldThrowSqliteException_OnDuplicateStreamUrl()
    {
        using SqliteDatabaseFixture fixture = new();
        RadioStationRepository repo = CreateRepository(fixture);
        await repo.AddAsync(NewStation(), CancellationToken.None);

        SqliteException ex = await Assert.ThrowsAsync<SqliteException>(
            () => repo.AddAsync(NewStation(name: "Other"), CancellationToken.None));

        Assert.Equal(19, ex.SqliteErrorCode);
    }

    [Fact(DisplayName = "GetById should return the saved station")]
    public async Task GetById_ShouldReturnSavedStation()
    {
        using SqliteDatabaseFixture fixture = new();
        RadioStationRepository repo = CreateRepository(fixture);
        long id = await repo.AddAsync(NewStation(), CancellationToken.None);

        RadioStationEntity? loaded = await repo.GetByIdAsync(id, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal("Nova", loaded!.Name);
    }

    [Fact(DisplayName = "GetById should return null when station does not exist")]
    public async Task GetById_ShouldReturnNull_WhenStationDoesNotExist()
    {
        using SqliteDatabaseFixture fixture = new();
        RadioStationRepository repo = CreateRepository(fixture);

        RadioStationEntity? loaded = await repo.GetByIdAsync(999, CancellationToken.None);

        Assert.Null(loaded);
    }

    [Fact(DisplayName = "List should order stations by LastListen desc then AddedAt desc")]
    public async Task List_ShouldOrderByLastListenDesc_ThenAddedAtDesc()
    {
        using SqliteDatabaseFixture fixture = new();
        RadioStationRepository repo = CreateRepository(fixture);
        long idA = await repo.AddAsync(NewStation("A", "http://a/"), CancellationToken.None);
        long idB = await repo.AddAsync(NewStation("B", "http://b/"), CancellationToken.None);
        await repo.TouchLastListenAsync(idA, new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc), CancellationToken.None);

        IReadOnlyList<RadioStationEntity> stations = await repo.ListAsync(CancellationToken.None);

        Assert.Equal(2, stations.Count);
        Assert.Equal("A", stations[0].Name);
        Assert.Equal("B", stations[1].Name);
    }

    [Fact(DisplayName = "Delete should remove the station")]
    public async Task Delete_ShouldRemoveStation()
    {
        using SqliteDatabaseFixture fixture = new();
        RadioStationRepository repo = CreateRepository(fixture);
        long id = await repo.AddAsync(NewStation(), CancellationToken.None);

        await repo.DeleteAsync(id, CancellationToken.None);

        int count = await fixture.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM RadioStations");
        Assert.Equal(0, count);
    }

    [Fact(DisplayName = "TouchLastListen should set LastListen to the provided value")]
    public async Task TouchLastListen_ShouldSetLastListenToProvidedValue()
    {
        using SqliteDatabaseFixture fixture = new();
        RadioStationRepository repo = CreateRepository(fixture);
        long id = await repo.AddAsync(NewStation(), CancellationToken.None);
        DateTime now = new(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);

        await repo.TouchLastListenAsync(id, now, CancellationToken.None);

        RadioStationEntity? loaded = await repo.GetByIdAsync(id, CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.Equal(now, loaded!.LastListen);
    }

    [Fact(DisplayName = "add_should_persist_all_extended_columns")]
    public async Task Add_ShouldPersistAllExtendedColumns()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        RadioStationRepository repo = CreateRepository(fixture);
        RadioStationEntity entity = new()
        {
            Name = "Jazz FM",
            StreamUrl = "https://stream.example/jazz-task4-001",
            HomepageUrl = "https://jazz.example",
            StationUuid = "uuid-jazz-001",
            FaviconUrl = "https://jazz.example/logo.png",
            CountryCode = "fr",
            Codec = "MP3",
            Bitrate = 128,
            AddedAt = DateTime.UtcNow
        };

        // Act
        long id = await repo.AddAsync(entity, CancellationToken.None);
        RadioStationEntity? loaded = (await repo.ListAsync(CancellationToken.None))
            .FirstOrDefault(s => s.Id == id);

        // Assert
        Assert.NotNull(loaded);
        Assert.Equal("uuid-jazz-001", loaded!.StationUuid);
        Assert.Equal("https://jazz.example/logo.png", loaded.FaviconUrl);
        Assert.Equal("fr", loaded.CountryCode);
        Assert.Equal("MP3", loaded.Codec);
        Assert.Equal(128, loaded.Bitrate);
    }

    [Fact(DisplayName = "add_should_persist_null_extended_columns")]
    public async Task Add_ShouldPersistNullExtendedColumns()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        RadioStationRepository repo = CreateRepository(fixture);
        RadioStationEntity entity = new()
        {
            Name = "Manual entry",
            StreamUrl = "https://stream.example/manual-task4-002",
            AddedAt = DateTime.UtcNow
        };

        // Act
        long id = await repo.AddAsync(entity, CancellationToken.None);
        RadioStationEntity? loaded = (await repo.ListAsync(CancellationToken.None))
            .FirstOrDefault(s => s.Id == id);

        // Assert
        Assert.NotNull(loaded);
        Assert.Null(loaded!.StationUuid);
        Assert.Null(loaded.FaviconUrl);
        Assert.Null(loaded.CountryCode);
        Assert.Null(loaded.Codec);
        Assert.Null(loaded.Bitrate);
    }
}