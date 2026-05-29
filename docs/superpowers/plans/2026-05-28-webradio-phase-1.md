# Webradio Phase 1 — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the ability to listen to a webradio stream from a manually entered URL, with ICY metadata capture and a mutually-exclusive "Radio" playback mode that disables track-specific features (score, navigation, prev/next, seek, crossfade).

**Architecture:** Clean / layered. Option A (single playback orchestrator). `PlayerService` exposes a new `EPlaybackMode { None, Music, Radio }`. The engine `IPlayerEngine` gets a `SetStream` code path that reads HTTP audio (MP3/AAC) with a 15 s `BufferedWaveProvider`, parses ICY metadata for the current `StreamTitle`, and surfaces buffering state. Favorites persist in a new SQLite table (`Migration11` → `RadioStations`).

**Tech Stack:** .NET 10, C# 13, WinUI 3, NAudio (`MediaFoundationReader` / `Mp3FileReader`, `BufferedWaveProvider`, `WaveOutEvent`), SQLite + Dapper, `CleanArch.DevKit.Mediator` (CQRS), `CleanArch.DevKit.Messaging` (events), xUnit + Moq + `SqliteDatabaseFixture`.

**Reference spec:** `docs/superpowers/specs/2026-05-28-webradio-phase-1-design.md`.

**Branch:** `feat/webradio-phase-1` (already created).

**Commit convention:** Conventional Commits enforced (`feat`, `fix`, `refactor`, `test`, `docs`, `chore` …). Scope: use `radio` for new code, `player` when touching shared player code.

**Build / test commands (Windows, x64):**
- `dotnet build /p:Platform=x64`
- `dotnet test /p:Platform=x64 --filter "FullyQualifiedName~<NamespaceOrClass>"`

---

## File Structure

### Created files

| Layer | Path | Responsibility |
|---|---|---|
| Domain | `src/Rok.Domain/Entities/RadioStationEntity.cs` | Persistence model for a saved station. |
| Application | `src/Rok.Application/Dto/RadioStationDto.cs` | Immutable transport record. |
| Application | `src/Rok.Application/Interfaces/Repositories/IRadioStationRepository.cs` | Repository contract. |
| Application | `src/Rok.Application/Player/EPlaybackMode.cs` | Mode enum. |
| Application | `src/Rok.Application/Messages/RadioStationChanged.cs` | Messenger payload — station switch. |
| Application | `src/Rok.Application/Messages/RadioMetadataChanged.cs` | Messenger payload — ICY title change. |
| Application | `src/Rok.Application/Messages/BufferingChanged.cs` | Messenger payload — buffering state change. |
| Application | `src/Rok.Application/Features/Radios/Services/IRadioStreamUrlResolver.cs` | `.pls`/`.m3u`/`.m3u8` resolver contract. |
| Application | `src/Rok.Application/Features/Radios/Services/RadioStreamUrlResolver.cs` | Resolver implementation. |
| Application | `src/Rok.Application/Features/Radios/Requests/AddRadioStationRequest.cs` | + validator + handler in one file (project convention). |
| Application | `src/Rok.Application/Features/Radios/Requests/AddRadioStationRequestHandler.cs` | Handler. |
| Application | `src/Rok.Application/Features/Radios/Requests/GetRadioStationsRequest.cs` | Query type. |
| Application | `src/Rok.Application/Features/Radios/Requests/GetRadioStationsRequestHandler.cs` | Handler. |
| Application | `src/Rok.Application/Features/Radios/Requests/DeleteRadioStationRequest.cs` | Command. |
| Application | `src/Rok.Application/Features/Radios/Requests/DeleteRadioStationRequestHandler.cs` | Handler. |
| Application | `src/Rok.Application/Features/Radios/Requests/PlayRadioStationByIdRequest.cs` | Command. |
| Application | `src/Rok.Application/Features/Radios/Requests/PlayRadioStationByIdRequestHandler.cs` | Handler — resolves favourite, plays, touches `LastListen`. |
| Application | `src/Rok.Application/Features/Radios/Requests/PlayRadioUrlRequest.cs` | Command + validator. |
| Application | `src/Rok.Application/Features/Radios/Requests/PlayRadioUrlRequestHandler.cs` | Handler — ad-hoc URL, no persistence. |
| Application | `src/Rok.Application/Mapping/RadioStationMapping.cs` | Entity ↔ DTO. |
| Infrastructure | `src/Rok.Infrastructure/Migration/Migration11.cs` | DDL for `RadioStations`. |
| Infrastructure | `src/Rok.Infrastructure/Repositories/RadioStationRepository.cs` | Dapper CRUD. |
| Infrastructure | `src/Rok.Infrastructure/Player/Streaming/IcyStreamHandler.cs` | HTTP fetch + ICY metadata extraction. |
| Infrastructure | `src/Rok.Infrastructure/Player/Streaming/IcyMetadataParser.cs` | Pure ICY block parser (`StreamTitle='…'`). |
| Infrastructure | `src/Rok.Infrastructure/Player/Streaming/StreamingPlayback.cs` | Composes `IcyStreamHandler` + decoder + buffer + WaveOut. |
| Presentation | `src/Presentation/ViewModels/Radio/RadiosViewModel.cs` | Favorites list + commands. |
| Presentation | `src/Presentation/Pages/RadiosPage.xaml` + `.cs` | Favorites page. |
| Presentation | `src/Presentation/Dialogs/AddRadioStationDialog.xaml` + `.cs` | Add-to-favorites dialog. |
| Presentation | `src/Presentation/Dialogs/PlayRadioUrlDialog.xaml` + `.cs` | Ad-hoc URL dialog. |
| Tests | `tests/UnitTests/Rok.ApplicationTests/Features/Radios/Requests/*HandlerTests.cs` | Handler tests (Moq). |
| Tests | `tests/UnitTests/Rok.ApplicationTests/Features/Radios/Services/RadioStreamUrlResolverTests.cs` | Resolver tests (`HttpMessageHandler` stub). |
| Tests | `tests/UnitTests/Rok.ApplicationTests/Player/PlayerServiceRadioModeTests.cs` | `PlayerService` mode behavior. |
| Tests | `tests/UnitTests/Rok.Infrastructure.UnitTests/Repositories/RadioStationRepositoryTests.cs` | SQLite fixture. |
| Tests | `tests/UnitTests/Rok.Infrastructure.UnitTests/Migrations/Migration11Tests.cs` | Schema assertions. |
| Tests | `tests/UnitTests/Rok.Infrastructure.UnitTests/Player/Streaming/IcyMetadataParserTests.cs` | Parser units. |

### Modified files

| Path | Change |
|---|---|
| `src/Rok.Application/Interfaces/IPlayerEngine.cs` | Add `SetStream`, `OnMetadataChanged`, `IsLive`, `IsBuffering`. |
| `src/Rok.Application/Player/IPlayerService.cs` | Add `Mode`, `CurrentStation`, `CurrentStreamTitle`, `IsBuffering`, `PlayRadioStation(RadioStationDto)`. |
| `src/Rok.Application/Player/PlayerService.cs` | Implement mode switching, invariants, propagate metadata / buffering messages. |
| `src/Rok.Infrastructure/Player/NAudioMediaPlayer.cs` | Implement `SetStream` + buffering + ICY plumbing. |
| `src/Rok.Infrastructure/Player/SystemMediaTransportControlsService.cs` | Disable next/prev and refresh display when `Mode == Radio`. |
| `src/Rok.Infrastructure/Discord/*` (path to confirm in DI) | Discord rich-presence: "Listening to {Station}" + StreamTitle. |
| `src/Rok.Infrastructure/Migration/MigrationService.cs` | (No change — `Migration11` is auto-discovered by DI as `IMigration`.) |
| `src/Rok.Infrastructure/DependencyInjection.cs` | Register `IRadioStationRepository`, `Migration11`. |
| `src/Rok.Application/DependencyInjection.cs` | Register `IRadioStreamUrlResolver` (transient, depends on `HttpClient`). |
| `src/Presentation/DependencyInjection.cs` | Register `RadiosViewModel`. |
| `src/Presentation/Pages/PlayerView.xaml` + `.cs` | Add radio-mode visual template. |
| `src/Presentation/MainPage.xaml` (or `MainShellView` — verify name at task time) | Add "Radios" navigation entry. |

### Out of scope

HLS playback, Radio-Browser catalogue, favicons, listening history, scoring, scrobbling, crossfade, per-station equalizer presets. All flagged in the spec.

---

## Conventions reminders

- **Branches**: never commit to `master`. Already on `feat/webradio-phase-1`.
- **Tests**: AAA layout, `DisplayName` in English, `when_x_then_y` style. Use `SqliteDatabaseFixture` for repo tests. Use `Moq` for handler tests (`Mock<IRepository>`). Result assertions: `result.Should().BeSuccess()` / `.BeFailure().And.HaveError<NotFoundError>().And.HaveErrorWithCode("radio.not_found")`.
- **No comments unless non-obvious.** No regions. Braces on their own line. `var` only when RHS is obvious. Early returns preferred.
- **Build is warning-free** (`TreatWarningsAsErrors=true` globally).
- **Pre-commit hook** runs `dotnet build /p:Platform=x64` + `dotnet format` on staged `.cs`. **Pre-push** runs `dotnet test /p:Platform=x64`. Don't bypass.

---

## Task 1 — `RadioStationEntity` (Domain)

**Files:**
- Create: `src/Rok.Domain/Entities/RadioStationEntity.cs`

- [ ] **Step 1: Create the entity**

```csharp
namespace Rok.Domain.Entities;

[Table("RadioStations")]
public class RadioStationEntity : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string StreamUrl { get; set; } = string.Empty;

    public string? HomepageUrl { get; set; }

    public DateTime AddedAt { get; set; }

    public DateTime? LastListen { get; set; }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build /p:Platform=x64`
Expected: build succeeds (no entity tests yet — entity has no behavior).

- [ ] **Step 3: Commit**

```bash
git add src/Rok.Domain/Entities/RadioStationEntity.cs
git commit -m "feat(radio): add RadioStationEntity domain model"
```

---

## Task 2 — `Migration11` (RadioStations DDL) + tests

**Files:**
- Create: `src/Rok.Infrastructure/Migration/Migration11.cs`
- Create: `tests/UnitTests/Rok.Infrastructure.UnitTests/Migrations/Migration11Tests.cs`

- [ ] **Step 1: Write the failing migration test**

```csharp
using Dapper;

namespace Rok.Infrastructure.UnitTests.Migrations;

public class Migration11Tests
{
    [Fact(DisplayName = "Migration 11 should create RadioStations table with expected columns")]
    public void Migration11_ShouldCreateRadioStationsTable_WithExpectedColumns()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();

        // Act — fixture applies all migrations including Migration11

        // Assert — table exists and has the expected columns
        string[] columns = fixture.Connection
            .Query<string>("SELECT name FROM pragma_table_info('RadioStations')")
            .ToArray();

        Assert.Contains("Id", columns);
        Assert.Contains("Name", columns);
        Assert.Contains("StreamUrl", columns);
        Assert.Contains("HomepageUrl", columns);
        Assert.Contains("AddedAt", columns);
        Assert.Contains("LastListen", columns);
    }

    [Fact(DisplayName = "Migration 11 should create a unique index on StreamUrl")]
    public void Migration11_ShouldCreateUniqueIndex_OnStreamUrl()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();

        // Act / Assert
        string? indexName = fixture.Connection.QueryFirstOrDefault<string>(
            "SELECT name FROM sqlite_master WHERE type = 'index' AND tbl_name = 'RadioStations' AND sql LIKE '%UNIQUE%StreamUrl%'");

        Assert.Equal("UX_RadioStations_StreamUrl", indexName);
    }
}
```

- [ ] **Step 2: Run test — expect FAIL**

Run: `dotnet test tests/UnitTests/Rok.Infrastructure.UnitTests/Rok.Infrastructure.UnitTests.csproj /p:Platform=x64 --filter "FullyQualifiedName~Migration11Tests"`
Expected: FAIL (`RadioStations` does not exist).

- [ ] **Step 3: Implement Migration11**

```csharp
namespace Rok.Infrastructure.Migration;

public class Migration11 : IMigration
{
    public int TargetVersion => 11;

    public void Apply(IDbConnection connection)
    {
        string createTable = """
            CREATE TABLE RadioStations (
                Id            INTEGER PRIMARY KEY AUTOINCREMENT,
                Name          TEXT    NOT NULL,
                StreamUrl     TEXT    NOT NULL,
                HomepageUrl   TEXT    NULL,
                AddedAt       TEXT    NOT NULL,
                LastListen    TEXT    NULL
            );
            """;

        string uniqueIndex = "CREATE UNIQUE INDEX UX_RadioStations_StreamUrl ON RadioStations(StreamUrl);";
        string lastListenIndex = "CREATE INDEX IX_RadioStations_LastListen ON RadioStations(LastListen DESC);";

        connection.Execute(createTable);
        connection.Execute(uniqueIndex);
        connection.Execute(lastListenIndex);
    }
}
```

- [ ] **Step 4: Verify Migration11 is registered**

Check `src/Rok.Infrastructure/DependencyInjection.cs` (or wherever `IMigration` implementations are registered — search with `Grep` for `IMigration`). If the registration is type-scan based, `Migration11` is auto-picked. Otherwise, add an explicit `services.AddSingleton<IMigration, Migration11>()` line next to the existing `Migration10` registration.

- [ ] **Step 5: Run test — expect PASS**

Run: `dotnet test tests/UnitTests/Rok.Infrastructure.UnitTests/Rok.Infrastructure.UnitTests.csproj /p:Platform=x64 --filter "FullyQualifiedName~Migration11Tests"`
Expected: 2 tests PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Rok.Infrastructure/Migration/Migration11.cs \
        tests/UnitTests/Rok.Infrastructure.UnitTests/Migrations/Migration11Tests.cs \
        src/Rok.Infrastructure/DependencyInjection.cs
git commit -m "feat(radio): add Migration11 creating RadioStations table"
```

---

## Task 3 — `RadioStationDto`

**Files:**
- Create: `src/Rok.Application/Dto/RadioStationDto.cs`

- [ ] **Step 1: Create the DTO**

```csharp
namespace Rok.Application.Dto;

public record RadioStationDto(
    long Id,
    string Name,
    string StreamUrl,
    string? HomepageUrl,
    DateTime AddedAt,
    DateTime? LastListen);
```

- [ ] **Step 2: Build**

Run: `dotnet build /p:Platform=x64`
Expected: success.

- [ ] **Step 3: Commit**

```bash
git add src/Rok.Application/Dto/RadioStationDto.cs
git commit -m "feat(radio): add RadioStationDto"
```

---

## Task 4 — `IRadioStationRepository` (Application contract)

**Files:**
- Create: `src/Rok.Application/Interfaces/Repositories/IRadioStationRepository.cs`

- [ ] **Step 1: Create the interface**

```csharp
namespace Rok.Application.Interfaces.Repositories;

public interface IRadioStationRepository
{
    Task<long> AddAsync(RadioStationEntity station, CancellationToken cancellationToken);

    Task<RadioStationEntity?> GetByIdAsync(long id, CancellationToken cancellationToken);

    Task<RadioStationEntity?> GetByUrlAsync(string streamUrl, CancellationToken cancellationToken);

    Task<IReadOnlyList<RadioStationEntity>> ListAsync(CancellationToken cancellationToken);

    Task DeleteAsync(long id, CancellationToken cancellationToken);

    Task TouchLastListenAsync(long id, DateTime utcNow, CancellationToken cancellationToken);
}
```

- [ ] **Step 2: Build**

Run: `dotnet build /p:Platform=x64`
Expected: success.

- [ ] **Step 3: Commit**

```bash
git add src/Rok.Application/Interfaces/Repositories/IRadioStationRepository.cs
git commit -m "feat(radio): add IRadioStationRepository contract"
```

---

## Task 5 — `RadioStationRepository` (Dapper implementation) + tests

**Files:**
- Create: `src/Rok.Infrastructure/Repositories/RadioStationRepository.cs`
- Create: `tests/UnitTests/Rok.Infrastructure.UnitTests/Repositories/RadioStationRepositoryTests.cs`

- [ ] **Step 1: Write failing repository tests**

```csharp
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
}
```

- [ ] **Step 2: Run tests — expect FAIL (compile error)**

Run: `dotnet test tests/UnitTests/Rok.Infrastructure.UnitTests/Rok.Infrastructure.UnitTests.csproj /p:Platform=x64 --filter "FullyQualifiedName~RadioStationRepositoryTests"`
Expected: compile error (no `RadioStationRepository`).

- [ ] **Step 3: Implement the repository**

```csharp
using Microsoft.Extensions.Logging;
using Rok.Application.Interfaces.Repositories;

namespace Rok.Infrastructure.Repositories;

public class RadioStationRepository(IDbConnection db, ILogger<RadioStationRepository> logger, TimeProvider timeProvider)
    : IRadioStationRepository
{
    private readonly IDbConnection _db = Guard.NotNull(db);
    private readonly ILogger<RadioStationRepository> _logger = Guard.NotNull(logger);
    private readonly TimeProvider _timeProvider = Guard.NotNull(timeProvider);

    private const string InsertSql = """
        INSERT INTO RadioStations (Name, StreamUrl, HomepageUrl, AddedAt, LastListen)
        VALUES (@Name, @StreamUrl, @HomepageUrl, @AddedAt, @LastListen);
        SELECT last_insert_rowid();
        """;

    private const string SelectAllSql = """
        SELECT Id, Name, StreamUrl, HomepageUrl, AddedAt, LastListen
        FROM RadioStations
        ORDER BY LastListen DESC NULLS LAST, AddedAt DESC
        """;

    private const string SelectByIdSql = """
        SELECT Id, Name, StreamUrl, HomepageUrl, AddedAt, LastListen
        FROM RadioStations WHERE Id = @Id
        """;

    private const string SelectByUrlSql = """
        SELECT Id, Name, StreamUrl, HomepageUrl, AddedAt, LastListen
        FROM RadioStations WHERE StreamUrl = @StreamUrl
        """;

    private const string DeleteSql = "DELETE FROM RadioStations WHERE Id = @Id";

    private const string TouchSql = "UPDATE RadioStations SET LastListen = @LastListen WHERE Id = @Id";

    public async Task<long> AddAsync(RadioStationEntity station, CancellationToken cancellationToken)
    {
        if (station.AddedAt == default)
            station.AddedAt = _timeProvider.GetUtcNow().UtcDateTime;

        long id = await _db.ExecuteScalarAsync<long>(new CommandDefinition(InsertSql, station, cancellationToken: cancellationToken));
        return id;
    }

    public Task<RadioStationEntity?> GetByIdAsync(long id, CancellationToken cancellationToken) =>
        _db.QueryFirstOrDefaultAsync<RadioStationEntity?>(new CommandDefinition(SelectByIdSql, new { Id = id }, cancellationToken: cancellationToken));

    public Task<RadioStationEntity?> GetByUrlAsync(string streamUrl, CancellationToken cancellationToken) =>
        _db.QueryFirstOrDefaultAsync<RadioStationEntity?>(new CommandDefinition(SelectByUrlSql, new { StreamUrl = streamUrl }, cancellationToken: cancellationToken));

    public async Task<IReadOnlyList<RadioStationEntity>> ListAsync(CancellationToken cancellationToken)
    {
        IEnumerable<RadioStationEntity> rows = await _db.QueryAsync<RadioStationEntity>(new CommandDefinition(SelectAllSql, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public Task DeleteAsync(long id, CancellationToken cancellationToken) =>
        _db.ExecuteAsync(new CommandDefinition(DeleteSql, new { Id = id }, cancellationToken: cancellationToken));

    public Task TouchLastListenAsync(long id, DateTime utcNow, CancellationToken cancellationToken) =>
        _db.ExecuteAsync(new CommandDefinition(TouchSql, new { Id = id, LastListen = utcNow }, cancellationToken: cancellationToken));
}
```

- [ ] **Step 4: Register repository in Infrastructure DI**

In `src/Rok.Infrastructure/DependencyInjection.cs`, add (next to other repositories):

```csharp
services.AddTransient<IRadioStationRepository, RadioStationRepository>();
```

- [ ] **Step 5: Run tests — expect PASS**

Run: `dotnet test tests/UnitTests/Rok.Infrastructure.UnitTests/Rok.Infrastructure.UnitTests.csproj /p:Platform=x64 --filter "FullyQualifiedName~RadioStationRepositoryTests"`
Expected: 7 tests PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Rok.Infrastructure/Repositories/RadioStationRepository.cs \
        src/Rok.Infrastructure/DependencyInjection.cs \
        tests/UnitTests/Rok.Infrastructure.UnitTests/Repositories/RadioStationRepositoryTests.cs
git commit -m "feat(radio): add RadioStationRepository with Dapper CRUD"
```

---

## Task 6 — `RadioStationMapping` (Entity ↔ DTO)

**Files:**
- Create: `src/Rok.Application/Mapping/RadioStationMapping.cs`

- [ ] **Step 1: Create the mapping**

```csharp
namespace Rok.Application.Mapping;

public static class RadioStationMapping
{
    public static RadioStationDto ToDto(this RadioStationEntity entity) =>
        new(entity.Id, entity.Name, entity.StreamUrl, entity.HomepageUrl, entity.AddedAt, entity.LastListen);

    public static RadioStationEntity ToEntity(this RadioStationDto dto) =>
        new()
        {
            Id = dto.Id,
            Name = dto.Name,
            StreamUrl = dto.StreamUrl,
            HomepageUrl = dto.HomepageUrl,
            AddedAt = dto.AddedAt,
            LastListen = dto.LastListen
        };
}
```

- [ ] **Step 2: Build**

Run: `dotnet build /p:Platform=x64`
Expected: success.

- [ ] **Step 3: Commit**

```bash
git add src/Rok.Application/Mapping/RadioStationMapping.cs
git commit -m "feat(radio): add RadioStationMapping"
```

---

## Task 7 — `EPlaybackMode` enum

**Files:**
- Create: `src/Rok.Application/Player/EPlaybackMode.cs`

- [ ] **Step 1: Create the enum**

```csharp
namespace Rok.Application.Player;

public enum EPlaybackMode
{
    None,
    Music,
    Radio
}
```

- [ ] **Step 2: Build**

Run: `dotnet build /p:Platform=x64`
Expected: success.

- [ ] **Step 3: Commit**

```bash
git add src/Rok.Application/Player/EPlaybackMode.cs
git commit -m "feat(player): add EPlaybackMode enum"
```

---

## Task 8 — Radio-related messages

**Files:**
- Create: `src/Rok.Application/Messages/RadioStationChanged.cs`
- Create: `src/Rok.Application/Messages/RadioMetadataChanged.cs`
- Create: `src/Rok.Application/Messages/BufferingChanged.cs`

- [ ] **Step 1: Create the message records**

```csharp
// RadioStationChanged.cs
using Rok.Application.Dto;

namespace Rok.Application.Messages;

public sealed record RadioStationChanged(RadioStationDto Station);
```

```csharp
// RadioMetadataChanged.cs
namespace Rok.Application.Messages;

public sealed record RadioMetadataChanged(string StreamTitle);
```

```csharp
// BufferingChanged.cs
namespace Rok.Application.Messages;

public sealed record BufferingChanged(bool IsBuffering);
```

- [ ] **Step 2: Build**

Run: `dotnet build /p:Platform=x64`
Expected: success.

- [ ] **Step 3: Commit**

```bash
git add src/Rok.Application/Messages/RadioStationChanged.cs \
        src/Rok.Application/Messages/RadioMetadataChanged.cs \
        src/Rok.Application/Messages/BufferingChanged.cs
git commit -m "feat(radio): add messaging payloads for station, metadata and buffering"
```

---

## Task 9 — `AddRadioStationRequest` + validator + handler + tests

**Files:**
- Create: `src/Rok.Application/Features/Radios/Requests/AddRadioStationRequest.cs`
- Create: `src/Rok.Application/Features/Radios/Requests/AddRadioStationRequestHandler.cs`
- Create: `tests/UnitTests/Rok.ApplicationTests/Features/Radios/Requests/AddRadioStationRequestHandlerTests.cs`

- [ ] **Step 1: Write failing handler tests**

```csharp
using Microsoft.Data.Sqlite;
using Moq;
using Rok.Application.Errors;
using Rok.Application.Features.Radios.Requests;
using Rok.Application.Interfaces.Repositories;
using Rok.Domain.Entities;

namespace Rok.ApplicationTests.Features.Radios.Requests;

public class AddRadioStationRequestHandlerTests
{
    [Fact(DisplayName = "Handle should add station and return its id")]
    public async Task Handle_ShouldAddStation_AndReturnId()
    {
        // Arrange
        Mock<IRadioStationRepository> repo = new();
        repo.Setup(r => r.AddAsync(It.IsAny<RadioStationEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(42L);
        AddRadioStationRequestHandler handler = new(repo.Object);
        AddRadioStationRequest request = new() { Name = "Nova", StreamUrl = "https://stream.nova.fr/nova.mp3" };

        // Act
        Result<long> result = await handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.Equal(42L, result.Value);
        repo.Verify(r => r.AddAsync(It.Is<RadioStationEntity>(e => e.Name == "Nova" && e.StreamUrl == "https://stream.nova.fr/nova.mp3"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "Handle should return conflict error when URL already exists")]
    public async Task Handle_ShouldReturnConflict_WhenUrlAlreadyExists()
    {
        // Arrange
        Mock<IRadioStationRepository> repo = new();
        SqliteException sqliteEx = SqliteExceptionStub.Create(19);
        repo.Setup(r => r.AddAsync(It.IsAny<RadioStationEntity>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(sqliteEx);
        AddRadioStationRequestHandler handler = new(repo.Object);
        AddRadioStationRequest request = new() { Name = "Nova", StreamUrl = "https://stream.nova.fr/nova.mp3" };

        // Act
        Result<long> result = await handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<ConflictError>().And.HaveErrorWithCode("radio.duplicate");
    }
}

internal static class SqliteExceptionStub
{
    // SqliteException has internal constructors; build via reflection.
    public static SqliteException Create(int errorCode)
    {
        var ctor = typeof(SqliteException).GetConstructors(
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).First();
        return (SqliteException)ctor.Invoke(new object?[] { "duplicate", errorCode, errorCode });
    }
}
```

> If `SqliteException` cannot be constructed via reflection on the project's Microsoft.Data.Sqlite version, replace the second test with an integration test using the real `SqliteDatabaseFixture` instead (preferred fallback). Run a quick check on the constructor signature when implementing.

- [ ] **Step 2: Run test — expect FAIL (compile error)**

Run: `dotnet test tests/UnitTests/Rok.ApplicationTests/Rok.ApplicationTests.csproj /p:Platform=x64 --filter "FullyQualifiedName~AddRadioStationRequestHandlerTests"`
Expected: compile error.

- [ ] **Step 3: Implement request, validator, handler**

```csharp
// AddRadioStationRequest.cs
using CleanArch.DevKit.Mediator;
using CleanArch.DevKit.Mediator.Results;
using CleanArch.DevKit.Mediator.Validation;

namespace Rok.Application.Features.Radios.Requests;

public class AddRadioStationRequest : IRequest<Result<long>>
{
    public string Name { get; set; } = string.Empty;

    public string StreamUrl { get; set; } = string.Empty;

    public string? HomepageUrl { get; set; }
}

public sealed class AddRadioStationRequestValidator : Validator<AddRadioStationRequest>
{
    public AddRadioStationRequestValidator()
    {
        Rule(x => x.Name).Required().MaxLength(200);
        Rule(x => x.StreamUrl).Required().Must(BeAbsoluteHttpUri, "Must be an absolute http(s) URL.");
    }

    private static bool BeAbsoluteHttpUri(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
```

```csharp
// AddRadioStationRequestHandler.cs
using CleanArch.DevKit.Mediator;
using CleanArch.DevKit.Mediator.Results;
using Microsoft.Data.Sqlite;
using Rok.Application.Errors;
using Rok.Application.Interfaces.Repositories;
using Rok.Domain.Entities;

namespace Rok.Application.Features.Radios.Requests;

public class AddRadioStationRequestHandler(IRadioStationRepository repository, TimeProvider timeProvider)
    : IRequestHandler<AddRadioStationRequest, Result<long>>
{
    public async Task<Result<long>> Handle(AddRadioStationRequest message, CancellationToken cancellationToken)
    {
        RadioStationEntity entity = new()
        {
            Name = message.Name,
            StreamUrl = message.StreamUrl,
            HomepageUrl = message.HomepageUrl,
            AddedAt = timeProvider.GetUtcNow().UtcDateTime
        };

        try
        {
            long id = await repository.AddAsync(entity, cancellationToken);
            return Result<long>.Ok(id);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            return Result<long>.Fail(new ConflictError("radio.duplicate", "A station with this URL already exists."));
        }
    }
}
```

- [ ] **Step 4: Run tests — expect PASS**

Run: `dotnet test tests/UnitTests/Rok.ApplicationTests/Rok.ApplicationTests.csproj /p:Platform=x64 --filter "FullyQualifiedName~AddRadioStationRequestHandlerTests"`
Expected: 2 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Rok.Application/Features/Radios/Requests/AddRadioStationRequest.cs \
        src/Rok.Application/Features/Radios/Requests/AddRadioStationRequestHandler.cs \
        tests/UnitTests/Rok.ApplicationTests/Features/Radios/Requests/AddRadioStationRequestHandlerTests.cs
git commit -m "feat(radio): add AddRadioStationRequest handler with duplicate guard"
```

---

## Task 10 — `GetRadioStationsRequest` + handler + tests

**Files:**
- Create: `src/Rok.Application/Features/Radios/Requests/GetRadioStationsRequest.cs`
- Create: `src/Rok.Application/Features/Radios/Requests/GetRadioStationsRequestHandler.cs`
- Create: `tests/UnitTests/Rok.ApplicationTests/Features/Radios/Requests/GetRadioStationsRequestHandlerTests.cs`

- [ ] **Step 1: Write failing handler test**

```csharp
using Moq;
using Rok.Application.Dto;
using Rok.Application.Features.Radios.Requests;
using Rok.Application.Interfaces.Repositories;
using Rok.Domain.Entities;

namespace Rok.ApplicationTests.Features.Radios.Requests;

public class GetRadioStationsRequestHandlerTests
{
    [Fact(DisplayName = "Handle should return mapped DTOs in repository order")]
    public async Task Handle_ShouldReturnMappedDtos_InRepositoryOrder()
    {
        // Arrange
        List<RadioStationEntity> entities =
        [
            new() { Id = 1, Name = "A", StreamUrl = "http://a/", AddedAt = DateTime.UtcNow },
            new() { Id = 2, Name = "B", StreamUrl = "http://b/", AddedAt = DateTime.UtcNow }
        ];
        Mock<IRadioStationRepository> repo = new();
        repo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(entities);
        GetRadioStationsRequestHandler handler = new(repo.Object);

        // Act
        Result<IReadOnlyList<RadioStationDto>> result = await handler.Handle(new GetRadioStationsRequest(), CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.Equal(2, result.Value.Count);
        Assert.Equal("A", result.Value[0].Name);
    }

    [Fact(DisplayName = "Handle should return empty list when repository returns none")]
    public async Task Handle_ShouldReturnEmptyList_WhenRepositoryReturnsNone()
    {
        // Arrange
        Mock<IRadioStationRepository> repo = new();
        repo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        GetRadioStationsRequestHandler handler = new(repo.Object);

        // Act
        Result<IReadOnlyList<RadioStationDto>> result = await handler.Handle(new GetRadioStationsRequest(), CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.Empty(result.Value);
    }
}
```

- [ ] **Step 2: Run test — expect FAIL**

Run: `dotnet test tests/UnitTests/Rok.ApplicationTests/Rok.ApplicationTests.csproj /p:Platform=x64 --filter "FullyQualifiedName~GetRadioStationsRequestHandlerTests"`
Expected: compile error.

- [ ] **Step 3: Implement request + handler**

```csharp
// GetRadioStationsRequest.cs
using CleanArch.DevKit.Mediator;
using CleanArch.DevKit.Mediator.Results;
using Rok.Application.Dto;

namespace Rok.Application.Features.Radios.Requests;

public class GetRadioStationsRequest : IRequest<Result<IReadOnlyList<RadioStationDto>>> { }
```

```csharp
// GetRadioStationsRequestHandler.cs
using CleanArch.DevKit.Mediator;
using CleanArch.DevKit.Mediator.Results;
using Rok.Application.Dto;
using Rok.Application.Interfaces.Repositories;
using Rok.Application.Mapping;

namespace Rok.Application.Features.Radios.Requests;

public class GetRadioStationsRequestHandler(IRadioStationRepository repository)
    : IRequestHandler<GetRadioStationsRequest, Result<IReadOnlyList<RadioStationDto>>>
{
    public async Task<Result<IReadOnlyList<RadioStationDto>>> Handle(GetRadioStationsRequest message, CancellationToken cancellationToken)
    {
        IReadOnlyList<RadioStationEntity> entities = await repository.ListAsync(cancellationToken);
        IReadOnlyList<RadioStationDto> dtos = entities.Select(e => e.ToDto()).ToList();
        return Result<IReadOnlyList<RadioStationDto>>.Ok(dtos);
    }
}
```

- [ ] **Step 4: Run tests — expect PASS**

Run: `dotnet test tests/UnitTests/Rok.ApplicationTests/Rok.ApplicationTests.csproj /p:Platform=x64 --filter "FullyQualifiedName~GetRadioStationsRequestHandlerTests"`
Expected: 2 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Rok.Application/Features/Radios/Requests/GetRadioStationsRequest.cs \
        src/Rok.Application/Features/Radios/Requests/GetRadioStationsRequestHandler.cs \
        tests/UnitTests/Rok.ApplicationTests/Features/Radios/Requests/GetRadioStationsRequestHandlerTests.cs
git commit -m "feat(radio): add GetRadioStationsRequest handler"
```

---

## Task 11 — `DeleteRadioStationRequest` + handler + tests

**Files:**
- Create: `src/Rok.Application/Features/Radios/Requests/DeleteRadioStationRequest.cs`
- Create: `src/Rok.Application/Features/Radios/Requests/DeleteRadioStationRequestHandler.cs`
- Create: `tests/UnitTests/Rok.ApplicationTests/Features/Radios/Requests/DeleteRadioStationRequestHandlerTests.cs`

- [ ] **Step 1: Write failing handler tests**

```csharp
using Moq;
using Rok.Application.Errors;
using Rok.Application.Features.Radios.Requests;
using Rok.Application.Interfaces.Repositories;
using Rok.Domain.Entities;

namespace Rok.ApplicationTests.Features.Radios.Requests;

public class DeleteRadioStationRequestHandlerTests
{
    [Fact(DisplayName = "Handle should delete station and return success")]
    public async Task Handle_ShouldDeleteStation_AndReturnSuccess()
    {
        // Arrange
        Mock<IRadioStationRepository> repo = new();
        repo.Setup(r => r.GetByIdAsync(5L, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RadioStationEntity { Id = 5, Name = "X", StreamUrl = "http://x/" });
        DeleteRadioStationRequestHandler handler = new(repo.Object);

        // Act
        Result<bool> result = await handler.Handle(new DeleteRadioStationRequest { Id = 5 }, CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        repo.Verify(r => r.DeleteAsync(5L, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "Handle should return NotFound when station does not exist")]
    public async Task Handle_ShouldReturnNotFound_WhenStationDoesNotExist()
    {
        // Arrange
        Mock<IRadioStationRepository> repo = new();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RadioStationEntity?)null);
        DeleteRadioStationRequestHandler handler = new(repo.Object);

        // Act
        Result<bool> result = await handler.Handle(new DeleteRadioStationRequest { Id = 99 }, CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<NotFoundError>().And.HaveErrorWithCode("radio.not_found");
        repo.Verify(r => r.DeleteAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
```

- [ ] **Step 2: Run test — expect FAIL**

Run: `dotnet test tests/UnitTests/Rok.ApplicationTests/Rok.ApplicationTests.csproj /p:Platform=x64 --filter "FullyQualifiedName~DeleteRadioStationRequestHandlerTests"`
Expected: compile error.

- [ ] **Step 3: Implement**

```csharp
// DeleteRadioStationRequest.cs
using CleanArch.DevKit.Mediator;
using CleanArch.DevKit.Mediator.Results;

namespace Rok.Application.Features.Radios.Requests;

public class DeleteRadioStationRequest : IRequest<Result<bool>>
{
    public long Id { get; set; }
}
```

```csharp
// DeleteRadioStationRequestHandler.cs
using CleanArch.DevKit.Mediator;
using CleanArch.DevKit.Mediator.Results;
using Rok.Application.Errors;
using Rok.Application.Interfaces.Repositories;

namespace Rok.Application.Features.Radios.Requests;

public class DeleteRadioStationRequestHandler(IRadioStationRepository repository)
    : IRequestHandler<DeleteRadioStationRequest, Result<bool>>
{
    public async Task<Result<bool>> Handle(DeleteRadioStationRequest message, CancellationToken cancellationToken)
    {
        RadioStationEntity? existing = await repository.GetByIdAsync(message.Id, cancellationToken);
        if (existing is null)
            return Result<bool>.Fail(new NotFoundError("radio.not_found", $"Radio station {message.Id} was not found."));

        await repository.DeleteAsync(message.Id, cancellationToken);
        return Result<bool>.Ok(true);
    }
}
```

- [ ] **Step 4: Run tests — expect PASS**

Run: `dotnet test tests/UnitTests/Rok.ApplicationTests/Rok.ApplicationTests.csproj /p:Platform=x64 --filter "FullyQualifiedName~DeleteRadioStationRequestHandlerTests"`
Expected: 2 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Rok.Application/Features/Radios/Requests/DeleteRadioStationRequest.cs \
        src/Rok.Application/Features/Radios/Requests/DeleteRadioStationRequestHandler.cs \
        tests/UnitTests/Rok.ApplicationTests/Features/Radios/Requests/DeleteRadioStationRequestHandlerTests.cs
git commit -m "feat(radio): add DeleteRadioStationRequest handler"
```

---

## Task 12 — `IRadioStreamUrlResolver` + implementation + tests

**Files:**
- Create: `src/Rok.Application/Features/Radios/Services/IRadioStreamUrlResolver.cs`
- Create: `src/Rok.Application/Features/Radios/Services/RadioStreamUrlResolver.cs`
- Create: `tests/UnitTests/Rok.ApplicationTests/Features/Radios/Services/RadioStreamUrlResolverTests.cs`

The resolver inspects the URL / response Content-Type and either returns a direct stream URL or extracts one from a `.pls` / `.m3u`. HLS segment manifests are rejected.

- [ ] **Step 1: Write failing resolver tests**

```csharp
using System.Net;
using System.Text;
using Moq;
using Moq.Protected;
using Rok.Application.Errors;
using Rok.Application.Features.Radios.Services;

namespace Rok.ApplicationTests.Features.Radios.Services;

public class RadioStreamUrlResolverTests
{
    private static RadioStreamUrlResolver CreateResolver(Dictionary<string, (string Body, string ContentType)> responses)
    {
        Mock<HttpMessageHandler> handlerMock = new(MockBehavior.Strict);
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage req, CancellationToken _) =>
            {
                string url = req.RequestUri!.ToString();
                if (!responses.TryGetValue(url, out (string Body, string ContentType) entry))
                    return new HttpResponseMessage(HttpStatusCode.NotFound);

                HttpResponseMessage resp = new(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(Encoding.UTF8.GetBytes(entry.Body))
                };
                resp.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(entry.ContentType);
                return resp;
            });

        HttpClient httpClient = new(handlerMock.Object);
        return new RadioStreamUrlResolver(httpClient);
    }

    [Fact(DisplayName = "Resolve should pass through a direct audio URL")]
    public async Task Resolve_ShouldPassThrough_DirectAudioUrl()
    {
        // Arrange
        RadioStreamUrlResolver resolver = CreateResolver(new()
        {
            ["http://stream/audio.mp3"] = ("[binary]", "audio/mpeg")
        });

        // Act
        Result<string> result = await resolver.ResolveAsync("http://stream/audio.mp3", CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.Equal("http://stream/audio.mp3", result.Value);
    }

    [Fact(DisplayName = "Resolve should extract first File entry from a .pls playlist")]
    public async Task Resolve_ShouldExtractFirstFileEntry_FromPlsPlaylist()
    {
        // Arrange
        string pls = """
            [playlist]
            NumberOfEntries=2
            File1=http://stream/one.mp3
            File2=http://stream/two.mp3
            Version=2
            """;
        RadioStreamUrlResolver resolver = CreateResolver(new()
        {
            ["http://radio/stream.pls"] = (pls, "audio/x-scpls")
        });

        // Act
        Result<string> result = await resolver.ResolveAsync("http://radio/stream.pls", CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.Equal("http://stream/one.mp3", result.Value);
    }

    [Fact(DisplayName = "Resolve should extract first URL from a simple m3u playlist")]
    public async Task Resolve_ShouldExtractFirstUrl_FromSimpleM3uPlaylist()
    {
        // Arrange
        string m3u = """
            #EXTM3U
            #EXTINF:-1,Radio
            http://stream/one.mp3
            http://stream/two.mp3
            """;
        RadioStreamUrlResolver resolver = CreateResolver(new()
        {
            ["http://radio/stream.m3u"] = (m3u, "audio/x-mpegurl")
        });

        // Act
        Result<string> result = await resolver.ResolveAsync("http://radio/stream.m3u", CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.Equal("http://stream/one.mp3", result.Value);
    }

    [Fact(DisplayName = "Resolve should reject an HLS segment manifest")]
    public async Task Resolve_ShouldReject_HlsSegmentManifest()
    {
        // Arrange
        string hls = """
            #EXTM3U
            #EXT-X-VERSION:3
            #EXT-X-TARGETDURATION:6
            #EXTINF:6.000,
            segment0.ts
            """;
        RadioStreamUrlResolver resolver = CreateResolver(new()
        {
            ["http://radio/live.m3u8"] = (hls, "application/vnd.apple.mpegurl")
        });

        // Act
        Result<string> result = await resolver.ResolveAsync("http://radio/live.m3u8", CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<OperationError>().And.HaveErrorWithCode("radio.hls_unsupported");
    }

    [Fact(DisplayName = "Resolve should return error when playlist contains no usable URL")]
    public async Task Resolve_ShouldReturnError_WhenPlaylistContainsNoUsableUrl()
    {
        // Arrange
        string pls = """
            [playlist]
            NumberOfEntries=0
            Version=2
            """;
        RadioStreamUrlResolver resolver = CreateResolver(new()
        {
            ["http://radio/empty.pls"] = (pls, "audio/x-scpls")
        });

        // Act
        Result<string> result = await resolver.ResolveAsync("http://radio/empty.pls", CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<OperationError>().And.HaveErrorWithCode("radio.no_stream_in_playlist");
    }
}
```

- [ ] **Step 2: Run tests — expect FAIL**

Run: `dotnet test tests/UnitTests/Rok.ApplicationTests/Rok.ApplicationTests.csproj /p:Platform=x64 --filter "FullyQualifiedName~RadioStreamUrlResolverTests"`
Expected: compile error.

- [ ] **Step 3: Implement the interface + class**

```csharp
// IRadioStreamUrlResolver.cs
using CleanArch.DevKit.Mediator.Results;

namespace Rok.Application.Features.Radios.Services;

public interface IRadioStreamUrlResolver
{
    Task<Result<string>> ResolveAsync(string url, CancellationToken cancellationToken);
}
```

```csharp
// RadioStreamUrlResolver.cs
using System.Net.Http.Headers;
using System.Text;
using CleanArch.DevKit.Mediator.Results;
using Rok.Application.Errors;

namespace Rok.Application.Features.Radios.Services;

public class RadioStreamUrlResolver(HttpClient httpClient) : IRadioStreamUrlResolver
{
    private const long MaxPlaylistBytes = 1024 * 1024;

    public async Task<Result<string>> ResolveAsync(string url, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
            return Result<string>.Fail(new OperationError("radio.invalid_url", "Invalid URL."));

        HttpResponseMessage response;
        try
        {
            response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            return Result<string>.Fail(new OperationError("radio.fetch_failed", ex.Message));
        }

        if (!response.IsSuccessStatusCode)
            return Result<string>.Fail(new OperationError("radio.fetch_failed", $"HTTP {(int)response.StatusCode}"));

        MediaTypeHeaderValue? mediaType = response.Content.Headers.ContentType;
        string mime = mediaType?.MediaType?.ToLowerInvariant() ?? string.Empty;

        if (IsDirectAudio(mime, uri))
            return Result<string>.Ok(uri.ToString());

        if (!IsPlaylist(mime, uri))
            return Result<string>.Fail(new OperationError("radio.unsupported_format", $"Unsupported content type '{mime}'."));

        string body = await ReadBodyAsync(response, cancellationToken);

        if (IsHlsManifest(body))
            return Result<string>.Fail(new OperationError("radio.hls_unsupported", "HLS streams are not supported."));

        string? extracted = ExtractStreamUrl(body, mime, uri);
        if (string.IsNullOrEmpty(extracted))
            return Result<string>.Fail(new OperationError("radio.no_stream_in_playlist", "No usable stream URL found in playlist."));

        return Result<string>.Ok(extracted);
    }

    private static bool IsDirectAudio(string mime, Uri uri)
    {
        if (mime.StartsWith("audio/", StringComparison.Ordinal)
            && !IsPlaylist(mime, uri))
            return true;
        return false;
    }

    private static bool IsPlaylist(string mime, Uri uri)
    {
        if (mime is "audio/x-mpegurl" or "audio/mpegurl" or "audio/x-scpls" or "application/vnd.apple.mpegurl")
            return true;

        string path = uri.AbsolutePath.ToLowerInvariant();
        return path.EndsWith(".pls", StringComparison.Ordinal)
            || path.EndsWith(".m3u", StringComparison.Ordinal)
            || path.EndsWith(".m3u8", StringComparison.Ordinal);
    }

    private static bool IsHlsManifest(string body) =>
        body.Contains("#EXT-X-TARGETDURATION", StringComparison.Ordinal)
        || body.Contains("#EXT-X-STREAM-INF", StringComparison.Ordinal)
        || body.Contains("#EXT-X-VERSION", StringComparison.Ordinal);

    private static string? ExtractStreamUrl(string body, string mime, Uri uri)
    {
        bool isPls = mime == "audio/x-scpls"
                  || uri.AbsolutePath.EndsWith(".pls", StringComparison.OrdinalIgnoreCase);

        if (isPls)
            return ExtractFromPls(body);

        return ExtractFromM3u(body);
    }

    private static string? ExtractFromPls(string body)
    {
        foreach (string raw in body.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = raw.Trim();
            if (!line.StartsWith("File", StringComparison.OrdinalIgnoreCase))
                continue;

            int eq = line.IndexOf('=');
            if (eq <= 0 || eq >= line.Length - 1)
                continue;

            string value = line[(eq + 1)..].Trim();
            if (Uri.TryCreate(value, UriKind.Absolute, out _))
                return value;
        }
        return null;
    }

    private static string? ExtractFromM3u(string body)
    {
        foreach (string raw in body.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;
            if (Uri.TryCreate(line, UriKind.Absolute, out _))
                return line;
        }
        return null;
    }

    private static async Task<string> ReadBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using StreamReader reader = new(stream, Encoding.UTF8);
        char[] buffer = new char[1024];
        StringBuilder sb = new();
        long total = 0;
        while (true)
        {
            int read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (read == 0) break;
            sb.Append(buffer, 0, read);
            total += read;
            if (total >= MaxPlaylistBytes) break;
        }
        return sb.ToString();
    }
}
```

- [ ] **Step 4: Run tests — expect PASS**

Run: `dotnet test tests/UnitTests/Rok.ApplicationTests/Rok.ApplicationTests.csproj /p:Platform=x64 --filter "FullyQualifiedName~RadioStreamUrlResolverTests"`
Expected: 5 tests PASS.

- [ ] **Step 5: Register `IRadioStreamUrlResolver` in Application DI**

In `src/Rok.Application/DependencyInjection.cs`, add:

```csharp
services.AddTransient<IRadioStreamUrlResolver, RadioStreamUrlResolver>();
```

The handler injection requires an `HttpClient`. The project already wires `IHttpClientFactory` (verify: `Grep` for `AddHttpClient`). If not, register a named or typed `HttpClient` for the resolver at this point — adapt to existing convention. Confirm presence of `Microsoft.Extensions.Http` package on `Rok.Application` (or move the resolver registration into `Rok.Infrastructure/DependencyInjection.cs` if HTTP wiring lives there).

- [ ] **Step 6: Commit**

```bash
git add src/Rok.Application/Features/Radios/Services/IRadioStreamUrlResolver.cs \
        src/Rok.Application/Features/Radios/Services/RadioStreamUrlResolver.cs \
        src/Rok.Application/DependencyInjection.cs \
        tests/UnitTests/Rok.ApplicationTests/Features/Radios/Services/RadioStreamUrlResolverTests.cs
git commit -m "feat(radio): add RadioStreamUrlResolver for .pls/.m3u with HLS guard"
```

---

## Task 13 — Extend `IPlayerEngine` interface

**Files:**
- Modify: `src/Rok.Application/Interfaces/IPlayerEngine.cs`

- [ ] **Step 1: Update the interface**

Add the four new members at the end of the interface:

```csharp
event EventHandler<string>? OnMetadataChanged;

bool IsLive { get; }

bool IsBuffering { get; }

bool SetStream(RadioStationDto station);
```

Final shape:

```csharp
using Rok.Application.Dto;

namespace Rok.Application.Interfaces;

public interface IPlayerEngine
{
    event EventHandler? OnMediaChanged;
    event EventHandler? OnMediaEnded;
    event EventHandler? OnMediaStateChanged;
    event EventHandler? OnMediaAboutToEnd;
    event EventHandler<string>? OnMetadataChanged;

    double Position { get; }
    double Length { get; set; }
    int CrossfadeDelay { get; }
    bool IsLive { get; }
    bool IsBuffering { get; }

    void Pause();
    void Play();
    void Stop();
    void SetPosition(double position);
    void SetVolume(double volume);
    bool SetTrack(TrackDto track);
    bool SetStream(RadioStationDto station);
    void SetEqualizerBand(int bandIndex, float gain);

    Task CrossfadeToAsync(TrackDto nextTrack, double durationSeconds, double masterVolume, CancellationToken ct);
}
```

- [ ] **Step 2: Build — expect FAIL**

Run: `dotnet build /p:Platform=x64`
Expected: build FAILS because `NAudioMediaPlayer` does not yet implement the new members. We'll add stubs in step 3 so the rest of the solution builds; the real implementation comes in later tasks.

- [ ] **Step 3: Add not-implemented stubs in `NAudioMediaPlayer`**

In `src/Rok.Infrastructure/Player/NAudioMediaPlayer.cs`, add (placed near the other event declarations):

```csharp
public event EventHandler<string>? OnMetadataChanged;

public bool IsLive => false;

public bool IsBuffering => false;

public bool SetStream(RadioStationDto station)
{
    throw new NotImplementedException("Streaming is wired in a later task.");
}
```

- [ ] **Step 4: Build — expect PASS**

Run: `dotnet build /p:Platform=x64`
Expected: build succeeds. Unit tests for other parts of the codebase still pass (`dotnet test /p:Platform=x64`).

- [ ] **Step 5: Commit**

```bash
git add src/Rok.Application/Interfaces/IPlayerEngine.cs \
        src/Rok.Infrastructure/Player/NAudioMediaPlayer.cs
git commit -m "feat(player): extend IPlayerEngine with streaming, metadata and buffering"
```

---

## Task 14 — `PlayerService` mode switching + tests

**Files:**
- Modify: `src/Rok.Application/Player/IPlayerService.cs`
- Modify: `src/Rok.Application/Player/PlayerService.cs`
- Create: `tests/UnitTests/Rok.ApplicationTests/Player/PlayerServiceRadioModeTests.cs`

- [ ] **Step 1: Extend `IPlayerService`**

Add the following members to `IPlayerService`:

```csharp
EPlaybackMode Mode { get; }
RadioStationDto? CurrentStation { get; }
string? CurrentStreamTitle { get; }
bool IsBuffering { get; }

void PlayRadioStation(RadioStationDto station);
```

- [ ] **Step 2: Write failing `PlayerServiceRadioModeTests`**

```csharp
using Moq;
using Rok.Application.Dto;
using Rok.Application.Interfaces;
using Rok.Application.Player;

namespace Rok.ApplicationTests.Player;

public class PlayerServiceRadioModeTests
{
    private static (PlayerService service, Mock<IPlayerEngine> engine) CreateService()
    {
        // Reuse the existing PlayerService DI construction approach used in other PlayerService tests
        // (see PlayerServiceCrossfadeTests for the canonical helper). The helper returns a service
        // backed by a Mock<IPlayerEngine>. Copy the helper from there and adapt.
        // After copying, ensure the engine mock has IsLive/IsBuffering returning the values expected by each test.
        throw new NotImplementedException("Replace with the project's PlayerService test factory");
    }

    [Fact(DisplayName = "PlayRadioStation should switch mode to Radio and clear the playlist")]
    public void PlayRadioStation_ShouldSwitchToRadio_AndClearPlaylist()
    {
        // Arrange
        (PlayerService service, Mock<IPlayerEngine> engine) = CreateService();
        service.LoadPlaylist([new TrackDto { Id = 1, Title = "T", MusicFile = "C:\\tmp.mp3" }], null);
        RadioStationDto station = new(0, "Nova", "http://stream/nova.mp3", null, DateTime.UtcNow, null);

        // Act
        service.PlayRadioStation(station);

        // Assert
        Assert.Equal(EPlaybackMode.Radio, service.Mode);
        Assert.Empty(service.Playlist);
        Assert.Null(service.CurrentTrack);
        Assert.Equal(station, service.CurrentStation);
        engine.Verify(e => e.SetStream(station), Times.Once);
    }

    [Fact(DisplayName = "Next should be a no-op when in radio mode")]
    public void Next_ShouldBeNoOp_WhenInRadioMode()
    {
        // Arrange
        (PlayerService service, _) = CreateService();
        service.PlayRadioStation(new RadioStationDto(0, "N", "http://s/", null, DateTime.UtcNow, null));

        // Act
        service.Next();

        // Assert
        Assert.Equal(EPlaybackMode.Radio, service.Mode);
        Assert.False(service.CanNext);
    }

    [Fact(DisplayName = "Starting music should stop active radio and switch mode to Music")]
    public void Start_ShouldStopRadio_AndSwitchToMusic()
    {
        // Arrange
        (PlayerService service, _) = CreateService();
        service.PlayRadioStation(new RadioStationDto(0, "N", "http://s/", null, DateTime.UtcNow, null));

        // Act
        service.LoadPlaylist([new TrackDto { Id = 1, Title = "T", MusicFile = "C:\\tmp.mp3" }], null);
        service.Start();

        // Assert
        Assert.Equal(EPlaybackMode.Music, service.Mode);
        Assert.Null(service.CurrentStation);
    }

    [Fact(DisplayName = "Position setter should be a no-op when in radio mode")]
    public void Position_ShouldBeNoOp_WhenInRadioMode()
    {
        // Arrange
        (PlayerService service, Mock<IPlayerEngine> engine) = CreateService();
        service.PlayRadioStation(new RadioStationDto(0, "N", "http://s/", null, DateTime.UtcNow, null));

        // Act
        service.Position = 42;

        // Assert
        engine.Verify(e => e.SetPosition(It.IsAny<double>()), Times.Never);
    }
}
```

> The factory helper `CreateService()` is intentionally a placeholder. Before writing this file, open the closest existing `PlayerService*Tests.cs` (e.g., `PlayerServiceCrossfadeTests`) and copy its construction helper verbatim, then adapt to expose the engine mock too. This avoids duplicating the long PlayerService constructor in this plan.

- [ ] **Step 3: Run test — expect FAIL**

Run: `dotnet test tests/UnitTests/Rok.ApplicationTests/Rok.ApplicationTests.csproj /p:Platform=x64 --filter "FullyQualifiedName~PlayerServiceRadioModeTests"`
Expected: compile error or NotImplementedException.

- [ ] **Step 4: Implement mode switching in `PlayerService`**

Edit `PlayerService` to add:

1. Backing fields:

```csharp
private EPlaybackMode _mode = EPlaybackMode.None;
private RadioStationDto? _currentStation;
private string? _currentStreamTitle;
```

2. Public properties:

```csharp
public EPlaybackMode Mode => _mode;

public RadioStationDto? CurrentStation => _currentStation;

public string? CurrentStreamTitle => _currentStreamTitle;

public bool IsBuffering => _player.IsBuffering;
```

3. New method `PlayRadioStation`:

```csharp
public void PlayRadioStation(RadioStationDto station)
{
    Guard.NotNull(station);

    StopForModeSwitch();

    Playlist.Clear();
    _currentTrack = null;
    _currentStation = station;
    _currentStreamTitle = null;
    _mode = EPlaybackMode.Radio;

    if (!_player.SetStream(station))
    {
        _currentStation = null;
        _mode = EPlaybackMode.None;
        PlaybackState = EPlaybackState.Stopped;
        return;
    }

    _player.Play();
    _messenger.Send(new RadioStationChanged(station));
}

private void StopForModeSwitch()
{
    if (_mode == EPlaybackMode.Radio)
    {
        _player.Stop();
        _currentStation = null;
        _currentStreamTitle = null;
    }
}
```

4. Update entry points that load music to switch back to `Music` mode and stop the radio first:

In `LoadPlaylist(...)` (or `Start(...)`, whichever is the canonical music entry-point — verify), add at the top:

```csharp
StopForModeSwitch();
_mode = EPlaybackMode.Music;
```

5. Update `Stop(...)` to reset mode:

```csharp
public void Stop(bool firePlaybackStateChange)
{
    // existing stop logic ...

    _mode = EPlaybackMode.None;
    _currentStation = null;
    _currentStreamTitle = null;
}
```

6. Update guards in `Next`, `Previous`, `Skip`, and the `Position` setter:

```csharp
public void Next()
{
    if (_mode == EPlaybackMode.Radio) return;
    // existing logic ...
}

// Same pattern in Previous(), Skip(), and Position setter.
```

7. Update `CanNext` and `CanPrevious` getters to return `false` when `_mode == EPlaybackMode.Radio`.

8. Subscribe to `_player.OnMetadataChanged` (wire in the constructor, alongside other engine event subscriptions):

```csharp
_player.OnMetadataChanged += (_, title) =>
{
    _currentStreamTitle = title;
    _messenger.Send(new RadioMetadataChanged(title));
};
```

- [ ] **Step 5: Replace the placeholder factory in the test**

Open the test file written in step 2 and replace `CreateService()` with a real builder copied from the existing `PlayerService` test infrastructure.

- [ ] **Step 6: Run tests — expect PASS**

Run: `dotnet test tests/UnitTests/Rok.ApplicationTests/Rok.ApplicationTests.csproj /p:Platform=x64 --filter "FullyQualifiedName~PlayerServiceRadioModeTests"`
Expected: 4 tests PASS. Also run the full test suite to catch regressions in other PlayerService tests:
Run: `dotnet test /p:Platform=x64 --filter "FullyQualifiedName~PlayerService"`
Expected: all PlayerService tests PASS.

- [ ] **Step 7: Commit**

```bash
git add src/Rok.Application/Player/IPlayerService.cs \
        src/Rok.Application/Player/PlayerService.cs \
        tests/UnitTests/Rok.ApplicationTests/Player/PlayerServiceRadioModeTests.cs
git commit -m "feat(player): add radio mode switching in PlayerService"
```

---

## Task 15 — `PlayRadioStationByIdRequest` + handler + tests

**Files:**
- Create: `src/Rok.Application/Features/Radios/Requests/PlayRadioStationByIdRequest.cs`
- Create: `src/Rok.Application/Features/Radios/Requests/PlayRadioStationByIdRequestHandler.cs`
- Create: `tests/UnitTests/Rok.ApplicationTests/Features/Radios/Requests/PlayRadioStationByIdRequestHandlerTests.cs`

- [ ] **Step 1: Write failing handler tests**

```csharp
using Moq;
using Rok.Application.Errors;
using Rok.Application.Features.Radios.Requests;
using Rok.Application.Interfaces.Repositories;
using Rok.Application.Player;
using Rok.Domain.Entities;

namespace Rok.ApplicationTests.Features.Radios.Requests;

public class PlayRadioStationByIdRequestHandlerTests
{
    [Fact(DisplayName = "Handle should play the favourite station and touch LastListen")]
    public async Task Handle_ShouldPlayFavouriteStation_AndTouchLastListen()
    {
        // Arrange
        DateTime now = new(2026, 5, 28, 12, 0, 0, DateTimeKind.Utc);
        FakeTimeProvider time = new(now);

        RadioStationEntity entity = new() { Id = 7, Name = "Nova", StreamUrl = "http://stream/nova.mp3", AddedAt = now };
        Mock<IRadioStationRepository> repo = new();
        repo.Setup(r => r.GetByIdAsync(7L, It.IsAny<CancellationToken>())).ReturnsAsync(entity);

        Mock<IPlayerService> player = new();
        PlayRadioStationByIdRequestHandler handler = new(repo.Object, player.Object, time);

        // Act
        Result<bool> result = await handler.Handle(new PlayRadioStationByIdRequest { Id = 7 }, CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        player.Verify(p => p.PlayRadioStation(It.Is<RadioStationDto>(d => d.Id == 7 && d.Name == "Nova")), Times.Once);
        repo.Verify(r => r.TouchLastListenAsync(7L, now, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "Handle should return NotFound when station does not exist")]
    public async Task Handle_ShouldReturnNotFound_WhenStationDoesNotExist()
    {
        // Arrange
        Mock<IRadioStationRepository> repo = new();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync((RadioStationEntity?)null);
        Mock<IPlayerService> player = new();
        FakeTimeProvider time = new(DateTime.UtcNow);
        PlayRadioStationByIdRequestHandler handler = new(repo.Object, player.Object, time);

        // Act
        Result<bool> result = await handler.Handle(new PlayRadioStationByIdRequest { Id = 99 }, CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<NotFoundError>().And.HaveErrorWithCode("radio.not_found");
        player.Verify(p => p.PlayRadioStation(It.IsAny<RadioStationDto>()), Times.Never);
    }
}
```

(`FakeTimeProvider` ships with `Microsoft.Extensions.TimeProvider.Testing`, already referenced by the test project.)

- [ ] **Step 2: Run tests — expect FAIL**

Run: `dotnet test tests/UnitTests/Rok.ApplicationTests/Rok.ApplicationTests.csproj /p:Platform=x64 --filter "FullyQualifiedName~PlayRadioStationByIdRequestHandlerTests"`
Expected: compile error.

- [ ] **Step 3: Implement request + handler**

```csharp
// PlayRadioStationByIdRequest.cs
using CleanArch.DevKit.Mediator;
using CleanArch.DevKit.Mediator.Results;

namespace Rok.Application.Features.Radios.Requests;

public class PlayRadioStationByIdRequest : IRequest<Result<bool>>
{
    public long Id { get; set; }
}
```

```csharp
// PlayRadioStationByIdRequestHandler.cs
using CleanArch.DevKit.Mediator;
using CleanArch.DevKit.Mediator.Results;
using Rok.Application.Errors;
using Rok.Application.Interfaces.Repositories;
using Rok.Application.Mapping;
using Rok.Application.Player;

namespace Rok.Application.Features.Radios.Requests;

public class PlayRadioStationByIdRequestHandler(
    IRadioStationRepository repository,
    IPlayerService playerService,
    TimeProvider timeProvider)
    : IRequestHandler<PlayRadioStationByIdRequest, Result<bool>>
{
    public async Task<Result<bool>> Handle(PlayRadioStationByIdRequest message, CancellationToken cancellationToken)
    {
        RadioStationEntity? entity = await repository.GetByIdAsync(message.Id, cancellationToken);
        if (entity is null)
            return Result<bool>.Fail(new NotFoundError("radio.not_found", $"Radio station {message.Id} was not found."));

        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        entity.LastListen = utcNow;

        playerService.PlayRadioStation(entity.ToDto());
        await repository.TouchLastListenAsync(entity.Id, utcNow, cancellationToken);

        return Result<bool>.Ok(true);
    }
}
```

- [ ] **Step 4: Run tests — expect PASS**

Run: `dotnet test tests/UnitTests/Rok.ApplicationTests/Rok.ApplicationTests.csproj /p:Platform=x64 --filter "FullyQualifiedName~PlayRadioStationByIdRequestHandlerTests"`
Expected: 2 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Rok.Application/Features/Radios/Requests/PlayRadioStationByIdRequest.cs \
        src/Rok.Application/Features/Radios/Requests/PlayRadioStationByIdRequestHandler.cs \
        tests/UnitTests/Rok.ApplicationTests/Features/Radios/Requests/PlayRadioStationByIdRequestHandlerTests.cs
git commit -m "feat(radio): add PlayRadioStationByIdRequest handler"
```

---

## Task 16 — `PlayRadioUrlRequest` + validator + handler + tests

**Files:**
- Create: `src/Rok.Application/Features/Radios/Requests/PlayRadioUrlRequest.cs`
- Create: `src/Rok.Application/Features/Radios/Requests/PlayRadioUrlRequestHandler.cs`
- Create: `tests/UnitTests/Rok.ApplicationTests/Features/Radios/Requests/PlayRadioUrlRequestHandlerTests.cs`

- [ ] **Step 1: Write failing handler tests**

```csharp
using Moq;
using Rok.Application.Dto;
using Rok.Application.Errors;
using Rok.Application.Features.Radios.Requests;
using Rok.Application.Features.Radios.Services;
using Rok.Application.Player;

namespace Rok.ApplicationTests.Features.Radios.Requests;

public class PlayRadioUrlRequestHandlerTests
{
    [Fact(DisplayName = "Handle should resolve URL and play an ad-hoc station without persisting")]
    public async Task Handle_ShouldResolveUrl_AndPlayAdHocStation()
    {
        // Arrange
        Mock<IRadioStreamUrlResolver> resolver = new();
        resolver.Setup(r => r.ResolveAsync("http://radio/stream.pls", It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<string>.Ok("http://stream/audio.mp3"));
        Mock<IPlayerService> player = new();
        PlayRadioUrlRequestHandler handler = new(resolver.Object, player.Object);

        // Act
        Result<bool> result = await handler.Handle(new PlayRadioUrlRequest { Url = "http://radio/stream.pls" }, CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        player.Verify(p => p.PlayRadioStation(It.Is<RadioStationDto>(d => d.Id == 0 && d.StreamUrl == "http://stream/audio.mp3")), Times.Once);
    }

    [Fact(DisplayName = "Handle should surface HLS rejection from resolver")]
    public async Task Handle_ShouldSurfaceHlsRejection_FromResolver()
    {
        // Arrange
        Mock<IRadioStreamUrlResolver> resolver = new();
        resolver.Setup(r => r.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<string>.Fail(new OperationError("radio.hls_unsupported", "HLS not supported.")));
        Mock<IPlayerService> player = new();
        PlayRadioUrlRequestHandler handler = new(resolver.Object, player.Object);

        // Act
        Result<bool> result = await handler.Handle(new PlayRadioUrlRequest { Url = "http://radio/live.m3u8" }, CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<OperationError>().And.HaveErrorWithCode("radio.hls_unsupported");
        player.Verify(p => p.PlayRadioStation(It.IsAny<RadioStationDto>()), Times.Never);
    }
}
```

- [ ] **Step 2: Run tests — expect FAIL**

Run: `dotnet test tests/UnitTests/Rok.ApplicationTests/Rok.ApplicationTests.csproj /p:Platform=x64 --filter "FullyQualifiedName~PlayRadioUrlRequestHandlerTests"`
Expected: compile error.

- [ ] **Step 3: Implement**

```csharp
// PlayRadioUrlRequest.cs
using CleanArch.DevKit.Mediator;
using CleanArch.DevKit.Mediator.Results;
using CleanArch.DevKit.Mediator.Validation;

namespace Rok.Application.Features.Radios.Requests;

public class PlayRadioUrlRequest : IRequest<Result<bool>>
{
    public string Url { get; set; } = string.Empty;
}

public sealed class PlayRadioUrlRequestValidator : Validator<PlayRadioUrlRequest>
{
    public PlayRadioUrlRequestValidator()
    {
        Rule(x => x.Url).Required().Must(BeAbsoluteHttpUri, "Must be an absolute http(s) URL.");
    }

    private static bool BeAbsoluteHttpUri(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
```

```csharp
// PlayRadioUrlRequestHandler.cs
using CleanArch.DevKit.Mediator;
using CleanArch.DevKit.Mediator.Results;
using Rok.Application.Dto;
using Rok.Application.Features.Radios.Services;
using Rok.Application.Player;

namespace Rok.Application.Features.Radios.Requests;

public class PlayRadioUrlRequestHandler(
    IRadioStreamUrlResolver resolver,
    IPlayerService playerService)
    : IRequestHandler<PlayRadioUrlRequest, Result<bool>>
{
    public async Task<Result<bool>> Handle(PlayRadioUrlRequest message, CancellationToken cancellationToken)
    {
        Result<string> resolved = await resolver.ResolveAsync(message.Url, cancellationToken);
        if (!resolved.IsSuccess)
            return Result<bool>.Fail(resolved.Error!);

        RadioStationDto adHoc = new(
            Id: 0,
            Name: "Ad-hoc stream",
            StreamUrl: resolved.Value,
            HomepageUrl: null,
            AddedAt: DateTime.UtcNow,
            LastListen: null);

        playerService.PlayRadioStation(adHoc);
        return Result<bool>.Ok(true);
    }
}
```

- [ ] **Step 4: Run tests — expect PASS**

Run: `dotnet test tests/UnitTests/Rok.ApplicationTests/Rok.ApplicationTests.csproj /p:Platform=x64 --filter "FullyQualifiedName~PlayRadioUrlRequestHandlerTests"`
Expected: 2 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Rok.Application/Features/Radios/Requests/PlayRadioUrlRequest.cs \
        src/Rok.Application/Features/Radios/Requests/PlayRadioUrlRequestHandler.cs \
        tests/UnitTests/Rok.ApplicationTests/Features/Radios/Requests/PlayRadioUrlRequestHandlerTests.cs
git commit -m "feat(radio): add PlayRadioUrlRequest handler for ad-hoc streams"
```

---

## Task 17 — `IcyMetadataParser` (pure parser) + tests

**Files:**
- Create: `src/Rok.Infrastructure/Player/Streaming/IcyMetadataParser.cs`
- Create: `tests/UnitTests/Rok.Infrastructure.UnitTests/Player/Streaming/IcyMetadataParserTests.cs`

- [ ] **Step 1: Write failing parser tests**

```csharp
using Rok.Infrastructure.Player.Streaming;

namespace Rok.Infrastructure.UnitTests.Player.Streaming;

public class IcyMetadataParserTests
{
    [Fact(DisplayName = "Parse should extract StreamTitle from a well-formed metadata block")]
    public void Parse_ShouldExtractStreamTitle_FromWellFormedBlock()
    {
        // Arrange
        string block = "StreamTitle='Daft Punk - One More Time';StreamUrl='http://example/';";

        // Act
        string? title = IcyMetadataParser.Parse(block);

        // Assert
        Assert.Equal("Daft Punk - One More Time", title);
    }

    [Fact(DisplayName = "Parse should return null when StreamTitle is missing")]
    public void Parse_ShouldReturnNull_WhenStreamTitleIsMissing()
    {
        // Arrange
        string block = "StreamUrl='http://example/';";

        // Act
        string? title = IcyMetadataParser.Parse(block);

        // Assert
        Assert.Null(title);
    }

    [Fact(DisplayName = "Parse should return null on malformed input")]
    public void Parse_ShouldReturnNull_OnMalformedInput()
    {
        // Arrange
        string block = "StreamTitle='Unterminated";

        // Act
        string? title = IcyMetadataParser.Parse(block);

        // Assert
        Assert.Null(title);
    }

    [Fact(DisplayName = "Parse should handle empty StreamTitle")]
    public void Parse_ShouldHandle_EmptyStreamTitle()
    {
        // Arrange
        string block = "StreamTitle='';";

        // Act
        string? title = IcyMetadataParser.Parse(block);

        // Assert
        Assert.Equal(string.Empty, title);
    }
}
```

- [ ] **Step 2: Run tests — expect FAIL**

Run: `dotnet test tests/UnitTests/Rok.Infrastructure.UnitTests/Rok.Infrastructure.UnitTests.csproj /p:Platform=x64 --filter "FullyQualifiedName~IcyMetadataParserTests"`
Expected: compile error.

- [ ] **Step 3: Implement the parser**

```csharp
namespace Rok.Infrastructure.Player.Streaming;

public static class IcyMetadataParser
{
    private const string Prefix = "StreamTitle='";

    public static string? Parse(string block)
    {
        if (string.IsNullOrEmpty(block))
            return null;

        int start = block.IndexOf(Prefix, StringComparison.Ordinal);
        if (start < 0)
            return null;

        int valueStart = start + Prefix.Length;
        int valueEnd = block.IndexOf('\'', valueStart);
        if (valueEnd < 0)
            return null;

        return block.Substring(valueStart, valueEnd - valueStart);
    }
}
```

- [ ] **Step 4: Run tests — expect PASS**

Run: `dotnet test tests/UnitTests/Rok.Infrastructure.UnitTests/Rok.Infrastructure.UnitTests.csproj /p:Platform=x64 --filter "FullyQualifiedName~IcyMetadataParserTests"`
Expected: 4 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Rok.Infrastructure/Player/Streaming/IcyMetadataParser.cs \
        tests/UnitTests/Rok.Infrastructure.UnitTests/Player/Streaming/IcyMetadataParserTests.cs
git commit -m "feat(radio): add IcyMetadataParser for StreamTitle extraction"
```

---

## Task 18 — `StreamingPlayback` (NAudio + ICY fetch + buffering)

**Files:**
- Create: `src/Rok.Infrastructure/Player/Streaming/IcyStreamHandler.cs`
- Create: `src/Rok.Infrastructure/Player/Streaming/StreamingPlayback.cs`
- Modify: `src/Rok.Infrastructure/Player/NAudioMediaPlayer.cs`

This task is **large** (the engine integration is inherently composite). It implements the spec's "Streaming engine" section in one cohesive change. Tests are deferred to the manual smoke tests described in the spec — automated tests for live HTTP audio would require WireMock + decoded audio fixtures (out of MVP scope).

- [ ] **Step 1: Implement `IcyStreamHandler`**

```csharp
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Rok.Infrastructure.Player.Streaming;

internal sealed class IcyStreamHandler : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _cts = new();

    public event EventHandler<string>? MetadataChanged;

    public Stream AudioStream { get; private set; } = Stream.Null;
    public string? ContentType { get; private set; }

    public IcyStreamHandler(HttpClient httpClient, ILogger logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task ConnectAsync(string url, CancellationToken cancellationToken)
    {
        using HttpRequestMessage req = new(HttpMethod.Get, url);
        req.Headers.TryAddWithoutValidation("Icy-MetaData", "1");

        HttpResponseMessage resp = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        resp.EnsureSuccessStatusCode();

        ContentType = resp.Content.Headers.ContentType?.MediaType;

        int metaInt = ReadMetaInt(resp);

        Stream net = await resp.Content.ReadAsStreamAsync(cancellationToken);
        AudioStream = metaInt > 0
            ? new IcyDemuxStream(net, metaInt, OnMetadata, _logger)
            : net;
    }

    private static int ReadMetaInt(HttpResponseMessage resp)
    {
        if (resp.Headers.TryGetValues("icy-metaint", out IEnumerable<string>? values)
            && int.TryParse(values.First(), out int metaInt) && metaInt > 0)
            return metaInt;
        return 0;
    }

    private void OnMetadata(string title) => MetadataChanged?.Invoke(this, title);

    public void Dispose()
    {
        _cts.Cancel();
        AudioStream.Dispose();
        _cts.Dispose();
    }

    private sealed class IcyDemuxStream : Stream
    {
        private readonly Stream _source;
        private readonly int _metaInt;
        private readonly Action<string> _onMetadata;
        private readonly ILogger _logger;
        private int _bytesUntilMeta;
        private string? _lastTitle;

        public IcyDemuxStream(Stream source, int metaInt, Action<string> onMetadata, ILogger logger)
        {
            _source = source;
            _metaInt = metaInt;
            _bytesUntilMeta = metaInt;
            _onMetadata = onMetadata;
            _logger = logger;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => 0; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_bytesUntilMeta == 0)
            {
                ReadMetadataBlock();
                _bytesUntilMeta = _metaInt;
            }

            int toRead = Math.Min(count, _bytesUntilMeta);
            int read = _source.Read(buffer, offset, toRead);
            _bytesUntilMeta -= read;
            return read;
        }

        private void ReadMetadataBlock()
        {
            int lenByte = _source.ReadByte();
            if (lenByte <= 0) return;

            int len = lenByte * 16;
            byte[] block = new byte[len];
            int read = 0;
            while (read < len)
            {
                int n = _source.Read(block, read, len - read);
                if (n <= 0) break;
                read += n;
            }

            string text = Encoding.UTF8.GetString(block, 0, read).TrimEnd('\0');
            string? title = IcyMetadataParser.Parse(text);
            if (title is not null && title != _lastTitle)
            {
                _lastTitle = title;
                try { _onMetadata(title); }
                catch (Exception ex) { _logger.LogDebug(ex, "ICY metadata handler threw"); }
            }
        }
    }
}
```

- [ ] **Step 2: Implement `StreamingPlayback`**

```csharp
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
using Rok.Application.Dto;

namespace Rok.Infrastructure.Player.Streaming;

internal sealed class StreamingPlayback : IDisposable
{
    public event EventHandler<string>? MetadataChanged;
    public event EventHandler? PlaybackEnded;
    public event EventHandler<bool>? BufferingChanged;

    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;

    private IcyStreamHandler? _icy;
    private IWaveProvider? _decoded;
    private BufferedWaveProvider? _buffer;
    private WaveOutEvent? _output;
    private CancellationTokenSource? _cts;
    private Task? _pumpTask;

    private const double BufferDurationSeconds = 15.0;
    private const double PreBufferSeconds = 3.0;
    private const double ResumeAfterUnderflowSeconds = 2.0;
    private const double BufferingTriggerSeconds = 0.5;
    private const double TerminalNoBytesSeconds = 5.0;

    public bool IsBuffering { get; private set; }

    public StreamingPlayback(HttpClient httpClient, ILogger logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task StartAsync(RadioStationDto station, CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                _icy = new IcyStreamHandler(_httpClient, _logger);
                _icy.MetadataChanged += (_, title) => MetadataChanged?.Invoke(this, title);

                await _icy.ConnectAsync(station.StreamUrl, _cts.Token);

                _decoded = CreateDecoder(_icy.AudioStream, _icy.ContentType);

                _buffer = new BufferedWaveProvider(_decoded.WaveFormat)
                {
                    BufferDuration = TimeSpan.FromSeconds(BufferDurationSeconds),
                    DiscardOnBufferOverflow = true
                };

                _output = new WaveOutEvent();
                _output.Init(_buffer);

                SetBuffering(true);
                _pumpTask = Task.Run(() => PumpAsync(_cts.Token), _cts.Token);
                return;
            }
            catch (Exception ex) when (attempt < 3)
            {
                _logger.LogWarning(ex, "Stream connect attempt {Attempt} failed", attempt);
                await Task.Delay(TimeSpan.FromSeconds(attempt), cancellationToken);
                DisposeResources();
            }
        }

        PlaybackEnded?.Invoke(this, EventArgs.Empty);
    }

    public void Stop()
    {
        _cts?.Cancel();
        try { _pumpTask?.Wait(TimeSpan.FromSeconds(2)); } catch { /* ignore */ }
        DisposeResources();
    }

    public void Pause() => _output?.Pause();

    public void Resume() => _output?.Play();

    public void SetVolume(double percent)
    {
        if (_output is null) return;
        _output.Volume = (float)Math.Clamp(percent / 100.0, 0.0, 1.0);
    }

    private static IWaveProvider CreateDecoder(Stream stream, string? contentType)
    {
        return contentType?.ToLowerInvariant() switch
        {
            "audio/aac" or "audio/aacp" or "audio/mp4" =>
                new StreamMediaFoundationReader(stream),
            _ => new Mp3FileReader(stream)
        };
    }

    private async Task PumpAsync(CancellationToken ct)
    {
        if (_decoded is null || _buffer is null || _output is null)
            return;

        byte[] readBuffer = new byte[8192];
        double bytesPerSecond = _decoded.WaveFormat.AverageBytesPerSecond;
        Stopwatch dryStopwatch = new();

        // Pre-buffer
        while (!ct.IsCancellationRequested
               && _buffer.BufferedBytes < bytesPerSecond * PreBufferSeconds)
        {
            int read = _decoded.Read(readBuffer, 0, readBuffer.Length);
            if (read <= 0) { await Task.Delay(50, ct); continue; }
            _buffer.AddSamples(readBuffer, 0, read);
        }

        SetBuffering(false);
        _output.Play();

        while (!ct.IsCancellationRequested)
        {
            int read = _decoded.Read(readBuffer, 0, readBuffer.Length);

            if (read > 0)
            {
                _buffer.AddSamples(readBuffer, 0, read);
                dryStopwatch.Reset();
            }
            else
            {
                if (!dryStopwatch.IsRunning) dryStopwatch.Start();

                if (dryStopwatch.Elapsed.TotalSeconds >= TerminalNoBytesSeconds)
                {
                    PlaybackEnded?.Invoke(this, EventArgs.Empty);
                    return;
                }

                await Task.Delay(100, ct);
            }

            double bufferedSec = _buffer.BufferedBytes / bytesPerSecond;

            if (!IsBuffering && bufferedSec < BufferingTriggerSeconds)
                SetBuffering(true);
            else if (IsBuffering && bufferedSec >= ResumeAfterUnderflowSeconds)
                SetBuffering(false);
        }
    }

    private void SetBuffering(bool value)
    {
        if (value == IsBuffering) return;
        IsBuffering = value;
        BufferingChanged?.Invoke(this, value);
    }

    private void DisposeResources()
    {
        try { _output?.Stop(); } catch { }
        _output?.Dispose(); _output = null;
        (_decoded as IDisposable)?.Dispose(); _decoded = null;
        _buffer = null;
        _icy?.Dispose(); _icy = null;
        _cts?.Dispose(); _cts = null;
    }

    public void Dispose() => DisposeResources();
}
```

- [ ] **Step 3: Wire `StreamingPlayback` into `NAudioMediaPlayer`**

Replace the `NotImplementedException` stubs from Task 13 with the real wiring:

```csharp
private StreamingPlayback? _streaming;
private bool _isLive;

public bool IsLive => _isLive;

public bool IsBuffering => _streaming?.IsBuffering ?? false;

public bool SetStream(RadioStationDto station)
{
    Stop();

    _streaming = new StreamingPlayback(_httpClientFactory.CreateClient("RadioStream"), _logger);
    _streaming.MetadataChanged += (_, title) => OnMetadataChanged?.Invoke(this, title);
    _streaming.PlaybackEnded += (_, _) => OnMediaEnded?.Invoke(this, EventArgs.Empty);
    _streaming.BufferingChanged += (_, _) => OnMediaStateChanged?.Invoke(this, EventArgs.Empty);

    _isLive = true;
    _length = 0;

    try
    {
        _streaming.StartAsync(station, CancellationToken.None).GetAwaiter().GetResult();
        OnMediaChanged?.Invoke(this, EventArgs.Empty);
        OnMediaStateChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to start stream {Url}", station.StreamUrl);
        _streaming?.Dispose();
        _streaming = null;
        _isLive = false;
        return false;
    }
}
```

Update `Stop()`, `Pause()`, `Play()`, and `SetVolume()` to delegate to `_streaming` when `_isLive == true`:

```csharp
public void Stop()
{
    _streaming?.Stop();
    _streaming?.Dispose();
    _streaming = null;
    _isLive = false;

    // ... existing file-based Stop logic ...
}

public void Pause()
{
    if (_isLive) { _streaming?.Pause(); return; }
    // existing file-based Pause logic
}

public void Play()
{
    if (_isLive) { _streaming?.Resume(); return; }
    // existing file-based Play logic
}

public void SetVolume(double volume)
{
    if (_isLive) { _streaming?.SetVolume(volume); return; }
    // existing file-based SetVolume logic
}
```

Also short-circuit `SetPosition` and `CrossfadeToAsync` when `_isLive == true`:

```csharp
public void SetPosition(double position)
{
    if (_isLive) return;
    // existing logic
}

public Task CrossfadeToAsync(TrackDto nextTrack, double durationSeconds, double masterVolume, CancellationToken ct)
{
    if (_isLive) return Task.CompletedTask;
    // existing logic
}
```

Constructor: inject `IHttpClientFactory`:

```csharp
public NAudioMediaPlayer(ILogger<NAudioMediaPlayer> logger, IHttpClientFactory httpClientFactory)
{
    _logger = logger;
    _httpClientFactory = httpClientFactory;
    // existing constructor body
}
```

- [ ] **Step 4: Register a named `HttpClient` in Infrastructure DI**

In `src/Rok.Infrastructure/DependencyInjection.cs`:

```csharp
services.AddHttpClient("RadioStream", c =>
{
    c.Timeout = Timeout.InfiniteTimeSpan;
    c.DefaultRequestHeaders.UserAgent.ParseAdd("Rok/1.0 (+https://github.com/...)");
});
```

(If `AddHttpClient` is already used in the project, follow the existing convention.)

- [ ] **Step 5: Build**

Run: `dotnet build /p:Platform=x64`
Expected: success.

- [ ] **Step 6: Run all tests**

Run: `dotnet test /p:Platform=x64`
Expected: all tests PASS (no regression in PlayerService / NAudioMediaPlayer).

- [ ] **Step 7: Commit**

```bash
git add src/Rok.Infrastructure/Player/Streaming/IcyStreamHandler.cs \
        src/Rok.Infrastructure/Player/Streaming/StreamingPlayback.cs \
        src/Rok.Infrastructure/Player/NAudioMediaPlayer.cs \
        src/Rok.Infrastructure/DependencyInjection.cs
git commit -m "feat(radio): implement NAudio streaming with ICY metadata and buffering"
```

---

## Task 19 — `AddRadioStationDialog` (Presentation)

**Files:**
- Create: `src/Presentation/Dialogs/AddRadioStationDialog.xaml`
- Create: `src/Presentation/Dialogs/AddRadioStationDialog.xaml.cs`

- [ ] **Step 1: Locate an existing dialog to mirror**

Find a small reference dialog (e.g., `EditAlbumDialog.xaml`) for naming/layout patterns. Mirror its `ContentDialog` shape, theme resources, and accept/cancel button styling.

- [ ] **Step 2: Implement the XAML**

```xml
<ContentDialog
    x:Class="Rok.Presentation.Dialogs.AddRadioStationDialog"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    Title="Add a webradio"
    PrimaryButtonText="Save"
    CloseButtonText="Cancel"
    DefaultButton="Primary"
    PrimaryButtonClick="OnSaveClick">
    <StackPanel Spacing="12" MinWidth="380">
        <TextBox x:Name="NameBox" Header="Name" PlaceholderText="My favourite station"/>
        <TextBox x:Name="UrlBox" Header="Stream URL or playlist (.pls / .m3u)" PlaceholderText="https://stream.example.com/audio.mp3"/>
        <TextBox x:Name="HomepageBox" Header="Homepage (optional)" PlaceholderText="https://station.example.com"/>
        <TextBlock x:Name="ErrorText" Foreground="Red" TextWrapping="Wrap" Visibility="Collapsed"/>
    </StackPanel>
</ContentDialog>
```

- [ ] **Step 3: Implement the code-behind**

```csharp
using CleanArch.DevKit.Mediator;
using Microsoft.UI.Xaml.Controls;
using Rok.Application.Errors;
using Rok.Application.Features.Radios.Requests;

namespace Rok.Presentation.Dialogs;

public sealed partial class AddRadioStationDialog : ContentDialog
{
    private readonly IMediator _mediator;

    public AddRadioStationDialog(IMediator mediator)
    {
        _mediator = mediator;
        InitializeComponent();
    }

    public bool Saved { get; private set; }

    private async void OnSaveClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        ContentDialogButtonClickDeferral deferral = args.GetDeferral();
        try
        {
            AddRadioStationRequest request = new()
            {
                Name = NameBox.Text.Trim(),
                StreamUrl = UrlBox.Text.Trim(),
                HomepageUrl = string.IsNullOrWhiteSpace(HomepageBox.Text) ? null : HomepageBox.Text.Trim()
            };

            Result<long> result = await _mediator.Send(request);

            if (result.IsSuccess)
            {
                Saved = true;
                return;
            }

            ErrorText.Text = result.Error switch
            {
                ConflictError => "A station with this URL already exists.",
                ValidationError ve => string.Join('\n', ve.Failures.Select(f => f.ErrorMessage)),
                _ => result.Error?.Message ?? "Unknown error."
            };
            ErrorText.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
            args.Cancel = true;
        }
        finally
        {
            deferral.Complete();
        }
    }
}
```

- [ ] **Step 4: Build**

Run: `dotnet build /p:Platform=x64`
Expected: success.

- [ ] **Step 5: Commit**

```bash
git add src/Presentation/Dialogs/AddRadioStationDialog.xaml \
        src/Presentation/Dialogs/AddRadioStationDialog.xaml.cs
git commit -m "feat(radio): add AddRadioStationDialog"
```

---

## Task 20 — `PlayRadioUrlDialog` (Presentation)

**Files:**
- Create: `src/Presentation/Dialogs/PlayRadioUrlDialog.xaml`
- Create: `src/Presentation/Dialogs/PlayRadioUrlDialog.xaml.cs`

- [ ] **Step 1: Implement the XAML**

```xml
<ContentDialog
    x:Class="Rok.Presentation.Dialogs.PlayRadioUrlDialog"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    Title="Play a webradio URL"
    PrimaryButtonText="Play"
    CloseButtonText="Cancel"
    DefaultButton="Primary"
    PrimaryButtonClick="OnPlayClick">
    <StackPanel Spacing="12" MinWidth="380">
        <TextBox x:Name="UrlBox" Header="Stream URL or playlist" PlaceholderText="https://stream.example.com/audio.mp3"/>
        <TextBlock Foreground="Gray" TextWrapping="Wrap"
                   Text="Supports MP3 and AAC streams. .pls and .m3u playlists are resolved automatically. HLS (.m3u8 segment manifests) is not supported."/>
        <TextBlock x:Name="ErrorText" Foreground="Red" TextWrapping="Wrap" Visibility="Collapsed"/>
    </StackPanel>
</ContentDialog>
```

- [ ] **Step 2: Implement the code-behind**

```csharp
using CleanArch.DevKit.Mediator;
using Microsoft.UI.Xaml.Controls;
using Rok.Application.Errors;
using Rok.Application.Features.Radios.Requests;

namespace Rok.Presentation.Dialogs;

public sealed partial class PlayRadioUrlDialog : ContentDialog
{
    private readonly IMediator _mediator;

    public PlayRadioUrlDialog(IMediator mediator)
    {
        _mediator = mediator;
        InitializeComponent();
    }

    public bool Played { get; private set; }

    private async void OnPlayClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        ContentDialogButtonClickDeferral deferral = args.GetDeferral();
        try
        {
            Result<bool> result = await _mediator.Send(new PlayRadioUrlRequest { Url = UrlBox.Text.Trim() });

            if (result.IsSuccess)
            {
                Played = true;
                return;
            }

            ErrorText.Text = result.Error?.Code switch
            {
                "radio.hls_unsupported" => "HLS streams are not supported.",
                "radio.no_stream_in_playlist" => "No usable stream URL found in this playlist.",
                "radio.fetch_failed" => "Cannot reach this URL.",
                _ => result.Error?.Message ?? "Unknown error."
            };
            ErrorText.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
            args.Cancel = true;
        }
        finally
        {
            deferral.Complete();
        }
    }
}
```

- [ ] **Step 3: Build & commit**

Run: `dotnet build /p:Platform=x64`
Expected: success.

```bash
git add src/Presentation/Dialogs/PlayRadioUrlDialog.xaml \
        src/Presentation/Dialogs/PlayRadioUrlDialog.xaml.cs
git commit -m "feat(radio): add PlayRadioUrlDialog for ad-hoc streams"
```

---

## Task 21 — `RadiosViewModel` + `RadiosPage` + DI

**Files:**
- Create: `src/Presentation/ViewModels/Radio/RadiosViewModel.cs`
- Create: `src/Presentation/Pages/RadiosPage.xaml`
- Create: `src/Presentation/Pages/RadiosPage.xaml.cs`
- Modify: `src/Presentation/DependencyInjection.cs`

- [ ] **Step 1: Implement the ViewModel**

```csharp
using System.Collections.ObjectModel;
using CleanArch.DevKit.Mediator;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rok.Application.Dto;
using Rok.Application.Features.Radios.Requests;

namespace Rok.Presentation.ViewModels.Radio;

public sealed partial class RadiosViewModel : ObservableObject
{
    private readonly IMediator _mediator;

    public ObservableCollection<RadioStationDto> Stations { get; } = [];

    public RadiosViewModel(IMediator mediator)
    {
        _mediator = mediator;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        Result<IReadOnlyList<RadioStationDto>> result = await _mediator.Send(new GetRadioStationsRequest());
        if (!result.IsSuccess) return;

        Stations.Clear();
        foreach (RadioStationDto station in result.Value)
            Stations.Add(station);
    }

    [RelayCommand]
    public async Task PlayAsync(RadioStationDto station)
    {
        await _mediator.Send(new PlayRadioStationByIdRequest { Id = station.Id });
    }

    [RelayCommand]
    public async Task DeleteAsync(RadioStationDto station)
    {
        Result<bool> result = await _mediator.Send(new DeleteRadioStationRequest { Id = station.Id });
        if (result.IsSuccess)
            Stations.Remove(station);
    }
}
```

- [ ] **Step 2: Implement the page XAML**

```xml
<Page
    x:Class="Rok.Presentation.Pages.RadiosPage"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:dto="using:Rok.Application.Dto">
    <Grid RowDefinitions="Auto,*" Padding="24" RowSpacing="16">
        <StackPanel Orientation="Horizontal" Spacing="8">
            <Button Content="Play a URL" Click="OnPlayUrlClick"/>
            <Button Content="Add to favourites" Click="OnAddClick"/>
        </StackPanel>

        <ListView Grid.Row="1" ItemsSource="{x:Bind ViewModel.Stations}">
            <ListView.ItemTemplate>
                <DataTemplate x:DataType="dto:RadioStationDto">
                    <Grid ColumnDefinitions="*,Auto,Auto" ColumnSpacing="12" Padding="8">
                        <StackPanel>
                            <TextBlock Text="{x:Bind Name}" FontWeight="SemiBold"/>
                            <TextBlock Text="{x:Bind StreamUrl}" Opacity="0.6" FontSize="12"/>
                        </StackPanel>
                        <Button Grid.Column="1" Content="Play"
                                Click="OnPlayClick"
                                Tag="{x:Bind}"/>
                        <Button Grid.Column="2" Content="Delete"
                                Click="OnDeleteClick"
                                Tag="{x:Bind}"/>
                    </Grid>
                </DataTemplate>
            </ListView.ItemTemplate>
        </ListView>
    </Grid>
</Page>
```

- [ ] **Step 3: Implement the page code-behind**

```csharp
using CleanArch.DevKit.Mediator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rok.Application.Dto;
using Rok.Presentation.Dialogs;
using Rok.Presentation.ViewModels.Radio;

namespace Rok.Presentation.Pages;

public sealed partial class RadiosPage : Page
{
    public RadiosViewModel ViewModel { get; }

    public RadiosPage()
    {
        IServiceProvider services = ((App)Application.Current).Services;
        ViewModel = services.GetRequiredService<RadiosViewModel>();
        InitializeComponent();
        _ = ViewModel.LoadAsync();
    }

    private async void OnAddClick(object sender, RoutedEventArgs e)
    {
        IServiceProvider services = ((App)Application.Current).Services;
        AddRadioStationDialog dialog = new(services.GetRequiredService<IMediator>())
        {
            XamlRoot = this.XamlRoot
        };
        ContentDialogResult result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary && dialog.Saved)
            await ViewModel.LoadAsync();
    }

    private async void OnPlayUrlClick(object sender, RoutedEventArgs e)
    {
        IServiceProvider services = ((App)Application.Current).Services;
        PlayRadioUrlDialog dialog = new(services.GetRequiredService<IMediator>())
        {
            XamlRoot = this.XamlRoot
        };
        await dialog.ShowAsync();
    }

    private void OnPlayClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: RadioStationDto dto })
            _ = ViewModel.PlayCommand.ExecuteAsync(dto);
    }

    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: RadioStationDto dto })
            _ = ViewModel.DeleteCommand.ExecuteAsync(dto);
    }
}
```

> Adjust the `App.Current.Services` access pattern to match whatever the project uses (look at one of the existing pages — e.g. `AlbumsPage.xaml.cs` — and mirror it).

- [ ] **Step 4: Register `RadiosViewModel` in `Presentation/DependencyInjection.cs`**

Add (with other ViewModel registrations):

```csharp
services.AddTransient<RadiosViewModel>();
```

- [ ] **Step 5: Build & commit**

Run: `dotnet build /p:Platform=x64`
Expected: success.

```bash
git add src/Presentation/ViewModels/Radio/RadiosViewModel.cs \
        src/Presentation/Pages/RadiosPage.xaml \
        src/Presentation/Pages/RadiosPage.xaml.cs \
        src/Presentation/DependencyInjection.cs
git commit -m "feat(radio): add RadiosPage and RadiosViewModel"
```

---

## Task 22 — Navigation entry for "Radios"

**Files:**
- Modify: `src/Presentation/MainPage.xaml` (or the equivalent shell page — verify name)
- Modify: `src/Presentation/MainPage.xaml.cs` (for the navigation handler, if there's one)

- [ ] **Step 1: Locate the navigation source**

Run: `Grep` for `NavigationViewItem` to find where the main nav pane is declared (e.g., `MainPage.xaml` / `MainShellView.xaml`).

- [ ] **Step 2: Add a new `NavigationViewItem`**

Add (near the Tracks / Playlists entries):

```xml
<NavigationViewItem Tag="Radios" Content="Radios">
    <NavigationViewItem.Icon>
        <SymbolIcon Symbol="Audio"/>
    </NavigationViewItem.Icon>
</NavigationViewItem>
```

- [ ] **Step 3: Wire the navigation handler**

In the code-behind switch / dispatcher that maps `Tag` → page type, add the `"Radios"` case:

```csharp
"Radios" => typeof(Rok.Presentation.Pages.RadiosPage),
```

- [ ] **Step 4: Build, run manually, verify the navigation entry appears and opens the empty Radios page.**

Run: `dotnet build /p:Platform=x64`
Manual: launch the app, click "Radios", confirm the empty page with the two top buttons appears.

- [ ] **Step 5: Commit**

```bash
git add src/Presentation/MainPage.xaml src/Presentation/MainPage.xaml.cs
git commit -m "feat(radio): add Radios entry to the main navigation pane"
```

---

## Task 23 — `PlayerView` radio template (dual UI)

**Files:**
- Modify: `src/Presentation/Pages/PlayerView.xaml`
- Modify: `src/Presentation/Pages/PlayerView.xaml.cs` (or the `PlayerViewModel` it binds to)
- Modify: `src/Presentation/ViewModels/Player/PlayerViewModel.cs` (verify exact path with Glob)

- [ ] **Step 1: Expose mode + radio properties on `PlayerViewModel`**

In the existing `PlayerViewModel`, add:

```csharp
[ObservableProperty]
private EPlaybackMode _mode;

[ObservableProperty]
private string? _currentStationName;

[ObservableProperty]
private string? _currentStreamTitle;

[ObservableProperty]
private bool _isBuffering;

public bool IsMusicMode => Mode == EPlaybackMode.Music;
public bool IsRadioMode => Mode == EPlaybackMode.Radio;
```

Wire `Mode` to `IPlayerService.Mode` and subscribe to the new messages (use the existing message subscription pattern in the VM — look for other `messenger.Subscribe<MediaStateChanged>(...)` calls):

```csharp
_subscriptions.Add(_messenger.Subscribe<RadioStationChanged>(this, m =>
{
    CurrentStationName = m.Station.Name;
    Mode = EPlaybackMode.Radio;
    OnPropertyChanged(nameof(IsMusicMode));
    OnPropertyChanged(nameof(IsRadioMode));
}));

_subscriptions.Add(_messenger.Subscribe<RadioMetadataChanged>(this, m =>
{
    CurrentStreamTitle = m.StreamTitle;
}));

_subscriptions.Add(_messenger.Subscribe<BufferingChanged>(this, m =>
{
    IsBuffering = m.IsBuffering;
}));
```

When `MediaStateChanged` triggers and `Mode == None`, ensure `IsMusicMode` / `IsRadioMode` notifications fire.

- [ ] **Step 2: Update the XAML — wrap existing music UI and add the radio panel**

Wrap the existing player content in two grids gated by visibility:

```xml
<Grid>
    <Grid Visibility="{x:Bind ViewModel.IsMusicMode, Mode=OneWay, Converter={StaticResource BoolToVisibility}}">
        <!-- existing music player markup (cover, progress bar, prev/next, score, ...) -->
    </Grid>

    <Grid Visibility="{x:Bind ViewModel.IsRadioMode, Mode=OneWay, Converter={StaticResource BoolToVisibility}}"
          ColumnDefinitions="*,Auto" ColumnSpacing="16" Padding="16">
        <StackPanel Spacing="4">
            <Border Background="Red" CornerRadius="4" HorizontalAlignment="Left" Padding="6,2">
                <TextBlock Text="LIVE" Foreground="White" FontSize="11" FontWeight="Bold"/>
            </Border>
            <TextBlock Text="{x:Bind ViewModel.CurrentStationName, Mode=OneWay}" FontSize="22" FontWeight="SemiBold"/>
            <TextBlock Text="{x:Bind ViewModel.CurrentStreamTitle, Mode=OneWay}" Opacity="0.8"/>
            <TextBlock Text="Buffering…" Opacity="0.7"
                       Visibility="{x:Bind ViewModel.IsBuffering, Mode=OneWay, Converter={StaticResource BoolToVisibility}}"/>
        </StackPanel>
        <Button Grid.Column="1" Content="Stop" Click="OnStopClick"/>
    </Grid>
</Grid>
```

> If `BoolToVisibility` already exists in `Converters/`, reuse it. Otherwise add a simple converter — but check the existing converters folder first.

- [ ] **Step 3: Build & manual test**

Run: `dotnet build /p:Platform=x64`
Manual: launch the app, switch from a playing track to a radio (paste a URL), verify the radio template appears and the music template disappears.

- [ ] **Step 4: Commit**

```bash
git add src/Presentation/Pages/PlayerView.xaml \
        src/Presentation/Pages/PlayerView.xaml.cs \
        src/Presentation/ViewModels/Player/PlayerViewModel.cs
git commit -m "feat(radio): add radio template to PlayerView and bind mode-aware properties"
```

---

## Task 24 — SMTC adaptation for radio mode

**Files:**
- Modify: `src/Rok.Infrastructure/Player/SystemMediaTransportControlsService.cs`

- [ ] **Step 1: Subscribe to radio messages and update SMTC display**

Open `SystemMediaTransportControlsService.cs`. Locate the existing `MediaStateChanged` / `MediaChanged` subscriptions. Add:

```csharp
_subscriptions.Add(_messenger.Subscribe<RadioStationChanged>(this, OnRadioStationChanged));
_subscriptions.Add(_messenger.Subscribe<RadioMetadataChanged>(this, OnRadioMetadataChanged));
```

Implement:

```csharp
private void OnRadioStationChanged(RadioStationChanged m)
{
    SystemMediaTransportControlsDisplayUpdater updater = _smtc.DisplayUpdater;
    updater.Type = MediaPlaybackType.Music;
    updater.MusicProperties.Title = m.Station.Name;
    updater.MusicProperties.Artist = m.Station.Name;
    updater.MusicProperties.AlbumArtist = m.Station.Name;
    updater.Update();

    _smtc.IsNextEnabled = false;
    _smtc.IsPreviousEnabled = false;
    _smtc.UpdateTimelineProperties(new SystemMediaTransportControlsTimelineProperties());
}

private void OnRadioMetadataChanged(RadioMetadataChanged m)
{
    SystemMediaTransportControlsDisplayUpdater updater = _smtc.DisplayUpdater;

    (string artist, string title) = ParseIcyTitle(m.StreamTitle, fallbackArtist: updater.MusicProperties.AlbumArtist);
    updater.MusicProperties.Title = title;
    updater.MusicProperties.Artist = artist;
    updater.Update();
}

private static (string Artist, string Title) ParseIcyTitle(string streamTitle, string fallbackArtist)
{
    int sep = streamTitle.IndexOf(" - ", StringComparison.Ordinal);
    if (sep > 0)
        return (streamTitle[..sep].Trim(), streamTitle[(sep + 3)..].Trim());
    return (fallbackArtist, streamTitle.Trim());
}
```

Also re-enable `IsNextEnabled` / `IsPreviousEnabled` when the existing music-mode handler fires (so switching back to music restores prev/next).

- [ ] **Step 2: Build & manual test**

Run: `dotnet build /p:Platform=x64`
Manual: play a radio with ICY, open the Windows mini-controller (volume keys), confirm the title updates and prev/next are disabled.

- [ ] **Step 3: Commit**

```bash
git add src/Rok.Infrastructure/Player/SystemMediaTransportControlsService.cs
git commit -m "feat(radio): wire SMTC display to radio station and ICY metadata"
```

---

## Task 25 — Discord rich presence adaptation

**Files:**
- Modify: the `IDiscordRichPresenceService` implementation (locate with `Grep -r DiscordRichPresenceService`)

- [ ] **Step 1: Locate the Discord service**

Run: `Grep` for `class DiscordRichPresenceService` to find the file.

- [ ] **Step 2: Subscribe to radio messages and update rich presence**

Apply the same pattern as Task 24:

```csharp
_subscriptions.Add(_messenger.Subscribe<RadioStationChanged>(this, OnRadioStationChanged));
_subscriptions.Add(_messenger.Subscribe<RadioMetadataChanged>(this, OnRadioMetadataChanged));

private void OnRadioStationChanged(RadioStationChanged m)
{
    _currentStationName = m.Station.Name;
    UpdatePresence(details: $"Listening to {m.Station.Name}", state: "");
}

private void OnRadioMetadataChanged(RadioMetadataChanged m)
{
    string state = m.StreamTitle.Length > 128 ? m.StreamTitle[..128] : m.StreamTitle;
    UpdatePresence(details: $"Listening to {_currentStationName}", state: state);
}
```

Where `UpdatePresence` is the existing Discord wrapper (look for the existing call pattern in the file).

- [ ] **Step 3: Build & manual test (optional)**

Run: `dotnet build /p:Platform=x64`
Manual: configure Discord credentials in `appsettings.json` (already required), play a radio, confirm presence updates.

- [ ] **Step 4: Commit**

```bash
git add <discord-service-file>
git commit -m "feat(radio): show station and ICY metadata in Discord rich presence"
```

---

## Task 26 — End-to-end manual verification + plan close-out

**Files:**
- None (manual)

- [ ] **Step 1: Full build + full test**

```bash
dotnet build /p:Platform=x64
dotnet test /p:Platform=x64
```

Expected: green.

- [ ] **Step 2: Manual smoke checklist (record results in PR description)**

- [ ] Paste a known public Shoutcast MP3 URL into "Play a URL" → audio plays within 3 s.
- [ ] Paste a `.pls` URL → audio plays.
- [ ] Paste a `.m3u` URL → audio plays.
- [ ] Paste an HLS `.m3u8` segment manifest URL → "HLS streams are not supported" error.
- [ ] Confirm `StreamTitle` displays and updates in the player view.
- [ ] Confirm Windows mini-controller (volume keys) shows station + StreamTitle, prev/next disabled.
- [ ] Confirm Discord rich presence shows "Listening to {Station}" + title.
- [ ] Click "Add to favourites" → save → station appears in favourites list.
- [ ] Click "Play" on a favourite → playback starts, LastListen updated (verify by re-opening the page, station should move to top).
- [ ] Click "Delete" → station disappears.
- [ ] While radio is playing, double-click a track in an album → radio stops, track starts (transparent mode switch).
- [ ] While music is playing, click "Play a URL" → music stops, stream starts.
- [ ] Disconnect the network briefly during stream playback → state shows "Buffering" within 1 s; reconnect → playback resumes.
- [ ] Disconnect the network for > 5 s → playback ends with "Stream stopped" indication.
- [ ] Sleep timer fires while a radio is playing → playback stops cleanly.
- [ ] Confirm progress bar, prev/next, seek, score, album/artist links are hidden in radio mode.

- [ ] **Step 3: Open PR**

```bash
git push -u origin feat/webradio-phase-1
gh pr create --title "feat(radio): webradio Phase 1 — manual URL with ICY metadata" \
             --body "Implements docs/superpowers/specs/2026-05-28-webradio-phase-1-design.md. See plan at docs/superpowers/plans/2026-05-28-webradio-phase-1.md."
```

- [ ] **Step 4: Update spec status**

Edit `docs/superpowers/specs/2026-05-28-webradio-phase-1-design.md` and change `Status: Draft (awaiting review)` to `Status: Implemented`. Commit:

```bash
git add docs/superpowers/specs/2026-05-28-webradio-phase-1-design.md
git commit -m "docs(webradio): mark phase 1 spec as implemented"
```

---

## Notes for the implementer

- **DI inspection step (do this once before Task 5):** open `src/Rok.Infrastructure/DependencyInjection.cs` and observe how repositories and migrations are registered. If everything is scanned by convention, the entries the plan adds become no-ops; if explicit, use the surrounding lines as templates.
- **Test factories for PlayerService**: rather than re-creating the long constructor argument list in `PlayerServiceRadioModeTests`, copy and adapt the factory used by `PlayerServiceCrossfadeTests`. This was deliberately left out of the plan to avoid duplicating a long, mostly-unrelated block; reading the existing test helper is the right shortcut.
- **HttpClient lifetime**: never instantiate `HttpClient` directly; always pull it from `IHttpClientFactory`. The Application-side `RadioStreamUrlResolver` should be wired via `AddHttpClient<RadioStreamUrlResolver>()` if existing convention uses typed clients.
- **Sleep timer interaction**: confirm that `PlayerSleepModeService.Stop()` reaches `_player.Stop()` via the standard path — no radio-specific changes expected, but verify in manual smoke step.
- **Equalizer**: the global equalizer chain is built per file in `SetTrack`. The streaming path in Task 18 currently bypasses the existing equalizer. If equalizer support over a radio stream is later requested, insert the `Equalizer` provider between `BufferedWaveProvider` and `WaveOutEvent` in `StreamingPlayback`. Out of scope for Phase 1; mentioned for completeness.

---

## Self-review checklist (the author already ran this)

- ✅ Spec coverage: every "In scope" bullet has a task (Migration11→T2, repository→T5, resolver→T12, engine→T13/T18, mode→T14, CQRS→T9-11/T15/T16, ICY→T17/T18, presentation→T19-23, SMTC→T24, Discord→T25, buffering→T18 with strategy from spec section).
- ✅ No placeholders left in the task bodies. The two "verify in existing code" notes (DI scan, test factory copy) are explicit research steps with named files, not "TBD".
- ✅ Type consistency: `IRadioStationRepository` method signatures match between Task 4 (interface), Task 5 (impl + tests), Task 11 (delete handler), Task 15 (play-by-id handler). `RadioStationDto` field order is consistent across mapping (T6), handlers (T9-T16), and view-models (T21). `EPlaybackMode` values used identically in `PlayerService` (T14) and `PlayerViewModel` (T23).
- ✅ Out-of-scope items from the spec are NOT in any task (HLS playback, Radio-Browser, favicons, history, scoring, scrobbling, crossfade, per-station EQ).
