# Webradio Search Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a search experience for webradios via Radio-Browser API directly from the `RadiosPage`, with click-to-play preview, star-to-favorite, persisted enrichment (StationUuid, FaviconUrl, CountryCode, Codec, Bitrate).

**Architecture:** Layered (`Domain → Application → Infrastructure / Presentation`). New typed `HttpClient` (`RadioBrowserClient`) behind an Application seam (`IRadioBrowserClient`). Search exposed via a CQRS request (`SearchRadioStationsRequest`). New `ContentDialog` (`SearchRadioStationsDialog`) opened from the `RadiosPage` `CommandBar`. Migration12 adds 5 nullable columns to the existing `RadioStations` table.

**Tech Stack:** .NET 10, C# 13 (preview), WinUI 3 / Windows App SDK 1.8, CleanArch.DevKit.Mediator, CommunityToolkit.Mvvm, Dapper + SQLite, `IHttpClientFactory`, xUnit + Moq, `System.Text.Json` (default web options).

**Spec reference:** `docs/superpowers/specs/2026-05-29-webradio-search-design.md`

**Branch:** continues on `feat/webradio-phase-1`. Every commit follows Conventional Commits (`feat|fix|test|refactor|chore(scope): …`).

**Build/test commands (PowerShell or Bash):**

```bash
dotnet build /p:Platform=x64
dotnet test /p:Platform=x64
dotnet format /p:Platform=x64
```

Build must remain green after each commit (`TreatWarningsAsErrors=true` is on globally).

---

## File map

**Created**

- `src/Rok.Application/Options/RadioBrowserOptions.cs`
- `src/Rok.Application/Dto/RadioSearchResultDto.cs`
- `src/Rok.Application/Features/Radios/Services/IRadioBrowserClient.cs`
- `src/Rok.Application/Features/Radios/Requests/SearchRadioStationsRequest.cs`
- `src/Rok.Application/Features/Radios/Requests/SearchRadioStationsRequestHandler.cs`
- `src/Rok.Infrastructure/Migration/Migration12.cs`
- `src/Rok.Infrastructure/RadioBrowser/RadioBrowserClient.cs`
- `src/Rok.Infrastructure/RadioBrowser/RadioBrowserStationResponse.cs`
- `src/Rok.Infrastructure/RadioBrowser/Mapping/RadioBrowserStationMapping.cs`
- `src/Presentation/ViewModels/Radio/SearchRadioStationsViewModel.cs`
- `src/Presentation/Dialogs/SearchRadioStationsDialog.xaml`
- `src/Presentation/Dialogs/SearchRadioStationsDialog.xaml.cs`
- `tests/UnitTests/Rok.ApplicationTests/Features/Radios/Requests/SearchRadioStationsRequestHandlerTests.cs`
- `tests/UnitTests/Rok.Infrastructure.UnitTests/RadioBrowser/RadioBrowserClientTests.cs`

**Modified**

- `src/Rok.Domain/Entities/RadioStationEntity.cs` (+5 properties)
- `src/Rok.Application/Dto/RadioStationDto.cs` (+5 record params)
- `src/Rok.Application/Mapping/RadioStationMapping.cs` (map new fields)
- `src/Rok.Application/Features/Radios/Requests/AddRadioStationRequest.cs` (+5 properties + validator rules)
- `src/Rok.Application/Features/Radios/Requests/AddRadioStationRequestHandler.cs` (copy new fields)
- `src/Rok.Application/Features/Radios/Requests/PlayRadioUrlRequestHandler.cs` (ad-hoc DTO must include new params)
- `src/Rok.Infrastructure/Repositories/RadioStationRepository.cs` (SELECT/INSERT)
- `src/Rok.Infrastructure/DependencyInjection.cs` (register Migration12 + typed `RadioBrowserClient`)
- `src/Presentation/Pages/RadiosPage.xaml` (+1 AppBarButton)
- `src/Presentation/Pages/RadiosPage.xaml.cs` (+`OnSearchClick`)
- `src/Presentation/DependencyInjection.cs` (+`SearchRadioStationsViewModel`)
- `src/Presentation/App.xaml.cs` (+`services.Configure<RadioBrowserOptions>(...)`)
- `src/Presentation/Strings/fr/Resources.resw`
- `src/Presentation/Strings/en/Resources.resw`
- `tests/UnitTests/Rok.Infrastructure.UnitTests/SqliteDatabaseFixture.cs` (+`new Migration12()`)
- `tests/UnitTests/Rok.Infrastructure.UnitTests/Repositories/RadioStationRepositoryTests.cs` (+2 cases)
- `tests/UnitTests/Rok.ApplicationTests/Features/Radios/Requests/AddRadioStationRequestHandlerTests.cs` (+3 cases)
- `tests/UnitTests/Rok.ApplicationTests/Features/Radios/Requests/PlayRadioUrlRequestHandlerTests.cs` (DTO constructor changes)
- `tests/UnitTests/Rok.ApplicationTests/Features/Radios/Requests/GetRadioStationsRequestHandlerTests.cs` (DTO constructor changes)

---

## Task 1 — Extend `RadioStationEntity` with new optional columns

**Files:**
- Modify: `src/Rok.Domain/Entities/RadioStationEntity.cs`

- [ ] **Step 1: Add the 5 new nullable properties**

Replace the body of `RadioStationEntity` with:

```csharp
namespace Rok.Domain.Entities;

[Table("RadioStations")]
public class RadioStationEntity : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string StreamUrl { get; set; } = string.Empty;

    public string? HomepageUrl { get; set; }

    public string? StationUuid { get; set; }

    public string? FaviconUrl { get; set; }

    public string? CountryCode { get; set; }

    public string? Codec { get; set; }

    public int? Bitrate { get; set; }

    public DateTime AddedAt { get; set; }

    public DateTime? LastListen { get; set; }
}
```

- [ ] **Step 2: Build (expect FAIL — the new properties are unused, but the project compiles since they are simple properties)**

```bash
dotnet build /p:Platform=x64
```

Expected: build SUCCESS (the entity additions don't break anything yet).

- [ ] **Step 3: Commit**

```bash
git add src/Rok.Domain/Entities/RadioStationEntity.cs
git commit -m "feat(radio): add StationUuid/FaviconUrl/CountryCode/Codec/Bitrate to RadioStationEntity"
```

---

## Task 2 — Create `Migration12` and register it

**Files:**
- Create: `src/Rok.Infrastructure/Migration/Migration12.cs`
- Modify: `src/Rok.Infrastructure/DependencyInjection.cs`

- [ ] **Step 1: Create `Migration12.cs`**

```csharp
namespace Rok.Infrastructure.Migration;

public class Migration12 : IMigration
{
    public int TargetVersion => 12;

    public void Apply(IDbConnection connection)
    {
        connection.Execute("ALTER TABLE RadioStations ADD COLUMN StationUuid TEXT NULL;");
        connection.Execute("ALTER TABLE RadioStations ADD COLUMN FaviconUrl  TEXT NULL;");
        connection.Execute("ALTER TABLE RadioStations ADD COLUMN CountryCode TEXT NULL;");
        connection.Execute("ALTER TABLE RadioStations ADD COLUMN Codec       TEXT NULL;");
        connection.Execute("ALTER TABLE RadioStations ADD COLUMN Bitrate     INTEGER NULL;");
    }
}
```

- [ ] **Step 2: Register Migration12 in DI**

In `src/Rok.Infrastructure/DependencyInjection.cs`, locate the migration registrations and append after `Migration11`:

```csharp
services.AddSingleton<IMigration, Migration12>();
```

(Existing block ends with `services.AddSingleton<IMigration, Migration11>();` — add Migration12 right after.)

- [ ] **Step 3: Build to verify**

```bash
dotnet build /p:Platform=x64
```

Expected: SUCCESS.

- [ ] **Step 4: Commit**

```bash
git add src/Rok.Infrastructure/Migration/Migration12.cs src/Rok.Infrastructure/DependencyInjection.cs
git commit -m "feat(radio): add Migration12 with 5 nullable columns on RadioStations"
```

---

## Task 3 — Update `SqliteDatabaseFixture` to apply Migration12

**Files:**
- Modify: `tests/UnitTests/Rok.Infrastructure.UnitTests/SqliteDatabaseFixture.cs`

- [ ] **Step 1: Add `new Migration12()` to the migrations array**

Locate the line:

```csharp
var migrations = new IMigration[] { new Migration2(), new Migration3(), new Migration4(), new Migration5(), new Migration6(), new Migration7(), new Migration8(), new Migration9(), new Migration10(), new Migration11() };
```

Replace with:

```csharp
var migrations = new IMigration[] { new Migration2(), new Migration3(), new Migration4(), new Migration5(), new Migration6(), new Migration7(), new Migration8(), new Migration9(), new Migration10(), new Migration11(), new Migration12() };
```

- [ ] **Step 2: Run the Infrastructure tests to confirm the fixture still builds and existing tests pass**

```bash
dotnet test tests/UnitTests/Rok.Infrastructure.UnitTests/Rok.Infrastructure.UnitTests.csproj /p:Platform=x64
```

Expected: all tests PASS (Migration12 is a no-op until the repository writes to those columns).

- [ ] **Step 3: Commit**

```bash
git add tests/UnitTests/Rok.Infrastructure.UnitTests/SqliteDatabaseFixture.cs
git commit -m "test(radio): apply Migration12 in SqliteDatabaseFixture"
```

---

## Task 4 — Extend `RadioStationRepository` to persist new columns (TDD)

**Files:**
- Test: `tests/UnitTests/Rok.Infrastructure.UnitTests/Repositories/RadioStationRepositoryTests.cs`
- Modify: `src/Rok.Infrastructure/Repositories/RadioStationRepository.cs`

### Step 1: Write the first failing test

- [ ] **Step 1.1: Read the current test file**

Open `tests/UnitTests/Rok.Infrastructure.UnitTests/Repositories/RadioStationRepositoryTests.cs` to see the existing patterns and helpers (fixture injection, AAA layout, snake-style English DisplayName).

- [ ] **Step 1.2: Add `add_should_persist_all_extended_columns`**

Append this test method to the existing test class (use the same DisplayName convention as the surrounding tests):

```csharp
[Fact(DisplayName = "add_should_persist_all_extended_columns")]
public async Task Add_ShouldPersistAllExtendedColumns()
{
    // Arrange
    RadioStationRepository repository = new(_fixture.Connection);
    RadioStationEntity entity = new()
    {
        Name = "Jazz FM",
        StreamUrl = "https://stream.example/jazz",
        HomepageUrl = "https://jazz.example",
        StationUuid = "uuid-jazz-001",
        FaviconUrl = "https://jazz.example/logo.png",
        CountryCode = "fr",
        Codec = "MP3",
        Bitrate = 128,
        AddedAt = DateTime.UtcNow
    };

    // Act
    long id = await repository.AddAsync(entity, CancellationToken.None);
    RadioStationEntity? loaded = (await repository.GetAllAsync(CancellationToken.None))
        .FirstOrDefault(s => s.Id == id);

    // Assert
    Assert.NotNull(loaded);
    Assert.Equal("uuid-jazz-001", loaded!.StationUuid);
    Assert.Equal("https://jazz.example/logo.png", loaded.FaviconUrl);
    Assert.Equal("fr", loaded.CountryCode);
    Assert.Equal("MP3", loaded.Codec);
    Assert.Equal(128, loaded.Bitrate);
}
```

If the existing test file uses a different fixture-access pattern (constructor injection, class fixture, `IClassFixture<SqliteDatabaseFixture>`), match it exactly.

If `GetAllAsync` isn't the existing read method, substitute the equivalent (see the existing tests).

- [ ] **Step 2: Run the test to see it fail (red)**

```bash
dotnet test tests/UnitTests/Rok.Infrastructure.UnitTests/Rok.Infrastructure.UnitTests.csproj /p:Platform=x64 --filter "DisplayName~add_should_persist_all_extended_columns"
```

Expected: FAIL. Either the test compiles but the persisted columns return `null` because the repository doesn't read/write them, or compilation fails because `GetAllAsync` doesn't exist in its current form. If it's the latter, fix the call to match an existing read method on the repository.

- [ ] **Step 3: Modify the repository INSERT and SELECT to handle the new columns**

Open `src/Rok.Infrastructure/Repositories/RadioStationRepository.cs`. Find the INSERT SQL (likely a single statement returning `last_insert_rowid()` or using Dapper's parameter binding) and update it:

```csharp
const string InsertSql = """
    INSERT INTO RadioStations
        (Name, StreamUrl, HomepageUrl, StationUuid, FaviconUrl, CountryCode, Codec, Bitrate, AddedAt, LastListen)
    VALUES
        (@Name, @StreamUrl, @HomepageUrl, @StationUuid, @FaviconUrl, @CountryCode, @Codec, @Bitrate, @AddedAt, @LastListen);
    SELECT last_insert_rowid();
    """;
```

Find the SELECT(s) returning `RadioStationEntity` (in `GetAll`/`GetById` or equivalents) and extend the column list:

```csharp
const string SelectSql = """
    SELECT Id, Name, StreamUrl, HomepageUrl, StationUuid, FaviconUrl, CountryCode, Codec, Bitrate, AddedAt, LastListen
    FROM RadioStations
    """;
```

If the file currently uses `SELECT *`, you can leave it; Dapper maps by column name. But explicit lists are preferred to remain stable against future schema changes.

- [ ] **Step 4: Re-run the test (green)**

```bash
dotnet test tests/UnitTests/Rok.Infrastructure.UnitTests/Rok.Infrastructure.UnitTests.csproj /p:Platform=x64 --filter "DisplayName~add_should_persist_all_extended_columns"
```

Expected: PASS.

- [ ] **Step 5: Add the second test `add_should_persist_null_extended_columns`**

```csharp
[Fact(DisplayName = "add_should_persist_null_extended_columns")]
public async Task Add_ShouldPersistNullExtendedColumns()
{
    // Arrange
    RadioStationRepository repository = new(_fixture.Connection);
    RadioStationEntity entity = new()
    {
        Name = "Manual entry",
        StreamUrl = "https://stream.example/manual",
        AddedAt = DateTime.UtcNow
    };

    // Act
    long id = await repository.AddAsync(entity, CancellationToken.None);
    RadioStationEntity? loaded = (await repository.GetAllAsync(CancellationToken.None))
        .FirstOrDefault(s => s.Id == id);

    // Assert
    Assert.NotNull(loaded);
    Assert.Null(loaded!.StationUuid);
    Assert.Null(loaded.FaviconUrl);
    Assert.Null(loaded.CountryCode);
    Assert.Null(loaded.Codec);
    Assert.Null(loaded.Bitrate);
}
```

- [ ] **Step 6: Run both tests**

```bash
dotnet test tests/UnitTests/Rok.Infrastructure.UnitTests/Rok.Infrastructure.UnitTests.csproj /p:Platform=x64 --filter "DisplayName~add_should_persist"
```

Expected: BOTH PASS.

- [ ] **Step 7: Run the full Infrastructure test suite**

```bash
dotnet test tests/UnitTests/Rok.Infrastructure.UnitTests/Rok.Infrastructure.UnitTests.csproj /p:Platform=x64
```

Expected: ALL PASS.

- [ ] **Step 8: Commit**

```bash
git add src/Rok.Infrastructure/Repositories/RadioStationRepository.cs tests/UnitTests/Rok.Infrastructure.UnitTests/Repositories/RadioStationRepositoryTests.cs
git commit -m "feat(radio): persist StationUuid/FaviconUrl/CountryCode/Codec/Bitrate in repository"
```

---

## Task 5 — Extend `RadioStationDto` and fix dependent code

`RadioStationDto` is a positional record. Adding parameters is a breaking change for every caller that constructs it. We fix them in this task atomically.

**Files:**
- Modify: `src/Rok.Application/Dto/RadioStationDto.cs`
- Modify: `src/Rok.Application/Mapping/RadioStationMapping.cs`
- Modify: `src/Rok.Application/Features/Radios/Requests/PlayRadioUrlRequestHandler.cs`
- Modify (tests will break compilation): `tests/UnitTests/Rok.ApplicationTests/Features/Radios/Requests/PlayRadioUrlRequestHandlerTests.cs`
- Modify (tests will break compilation): `tests/UnitTests/Rok.ApplicationTests/Features/Radios/Requests/GetRadioStationsRequestHandlerTests.cs`

- [ ] **Step 1: Extend the record**

Replace `src/Rok.Application/Dto/RadioStationDto.cs` with:

```csharp
namespace Rok.Application.Dto;

public record RadioStationDto(
    long Id,
    string Name,
    string StreamUrl,
    string? HomepageUrl,
    string? StationUuid,
    string? FaviconUrl,
    string? CountryCode,
    string? Codec,
    int? Bitrate,
    DateTime AddedAt,
    DateTime? LastListen);
```

- [ ] **Step 2: Update the mapping**

Open `src/Rok.Application/Mapping/RadioStationMapping.cs`. It currently maps `RadioStationEntity` → `RadioStationDto` with the 6-arg constructor. Update it to pass the new fields:

```csharp
public static RadioStationDto ToDto(this RadioStationEntity entity) =>
    new(
        entity.Id,
        entity.Name,
        entity.StreamUrl,
        entity.HomepageUrl,
        entity.StationUuid,
        entity.FaviconUrl,
        entity.CountryCode,
        entity.Codec,
        entity.Bitrate,
        entity.AddedAt,
        entity.LastListen);
```

(Adjust to the exact signature/style currently used in the file — extension method or static method.)

- [ ] **Step 3: Fix `PlayRadioUrlRequestHandler`**

In `src/Rok.Application/Features/Radios/Requests/PlayRadioUrlRequestHandler.cs`, locate the ad-hoc DTO:

```csharp
RadioStationDto adHoc = new(
    Id: 0,
    Name: "Ad-hoc stream",
    StreamUrl: resolved.Value,
    HomepageUrl: null,
    AddedAt: DateTime.UtcNow,
    LastListen: null);
```

Replace with:

```csharp
RadioStationDto adHoc = new(
    Id: 0,
    Name: "Ad-hoc stream",
    StreamUrl: resolved.Value,
    HomepageUrl: null,
    StationUuid: null,
    FaviconUrl: null,
    CountryCode: null,
    Codec: null,
    Bitrate: null,
    AddedAt: DateTime.UtcNow,
    LastListen: null);
```

- [ ] **Step 4: Fix `PlayRadioUrlRequestHandlerTests`**

Open the test file. Find every place that constructs a `RadioStationDto` (likely inside a mock setup or expected-value assertion) and add the 5 new named arguments (all `null` unless the test explicitly cares). Use named-argument syntax to keep call sites readable.

- [ ] **Step 5: Fix `GetRadioStationsRequestHandlerTests`**

Same operation: open the file, locate every `new RadioStationDto(...)` and add the 5 new named arguments (all `null` by default in these test setups — the new fields don't change the behavior these tests are validating).

- [ ] **Step 6: Build**

```bash
dotnet build /p:Platform=x64
```

Expected: SUCCESS. If a compilation error remains, it's another callsite of `RadioStationDto` constructor — fix it in the same shape.

- [ ] **Step 7: Run the full Application test suite**

```bash
dotnet test tests/UnitTests/Rok.ApplicationTests/Rok.ApplicationTests.csproj /p:Platform=x64
```

Expected: ALL PASS (nothing should change behaviorally; only constructor shapes were updated).

- [ ] **Step 8: Commit**

```bash
git add src/Rok.Application/Dto/RadioStationDto.cs src/Rok.Application/Mapping/RadioStationMapping.cs src/Rok.Application/Features/Radios/Requests/PlayRadioUrlRequestHandler.cs tests/UnitTests/Rok.ApplicationTests/Features/Radios/Requests/PlayRadioUrlRequestHandlerTests.cs tests/UnitTests/Rok.ApplicationTests/Features/Radios/Requests/GetRadioStationsRequestHandlerTests.cs
git commit -m "refactor(radio): extend RadioStationDto with 5 new optional fields"
```

---

## Task 6 — Extend `AddRadioStationRequest` + validator + handler (TDD)

**Files:**
- Modify: `src/Rok.Application/Features/Radios/Requests/AddRadioStationRequest.cs`
- Modify: `src/Rok.Application/Features/Radios/Requests/AddRadioStationRequestHandler.cs`
- Test: `tests/UnitTests/Rok.ApplicationTests/Features/Radios/Requests/AddRadioStationRequestHandlerTests.cs`

### Step 1: Add the first new test (red)

- [ ] **Step 1.1: Read the existing test file**

Open `tests/UnitTests/Rok.ApplicationTests/Features/Radios/Requests/AddRadioStationRequestHandlerTests.cs` to see how it mocks `IRadioStationRepository` and `TimeProvider`. Identify the fluent `Result.Should()...` patterns used.

- [ ] **Step 1.2: Add `add_should_persist_extended_metadata`**

Append (match the existing class's setup, mock fields, and AAA layout):

```csharp
[Fact(DisplayName = "add_should_persist_extended_metadata")]
public async Task Add_ShouldPersistExtendedMetadata()
{
    // Arrange
    RadioStationEntity? captured = null;
    _repository
        .Setup(r => r.AddAsync(It.IsAny<RadioStationEntity>(), It.IsAny<CancellationToken>()))
        .Callback<RadioStationEntity, CancellationToken>((e, _) => captured = e)
        .ReturnsAsync(42L);

    AddRadioStationRequest request = new()
    {
        Name = "Jazz FM",
        StreamUrl = "https://stream.example/jazz",
        HomepageUrl = "https://jazz.example",
        StationUuid = "uuid-jazz-001",
        FaviconUrl = "https://jazz.example/logo.png",
        CountryCode = "fr",
        Codec = "MP3",
        Bitrate = 128
    };

    AddRadioStationRequestHandler handler = new(_repository.Object, _timeProvider);

    // Act
    Result<long> result = await handler.Handle(request, CancellationToken.None);

    // Assert
    result.Should().BeSuccess();
    Assert.NotNull(captured);
    Assert.Equal("uuid-jazz-001", captured!.StationUuid);
    Assert.Equal("https://jazz.example/logo.png", captured.FaviconUrl);
    Assert.Equal("fr", captured.CountryCode);
    Assert.Equal("MP3", captured.Codec);
    Assert.Equal(128, captured.Bitrate);
}
```

(If the existing tests do not have `_repository`/`_timeProvider` as fields, mirror the local `Mock<>` instantiation style used there.)

- [ ] **Step 2: Run the test to see it fail (red)**

```bash
dotnet test tests/UnitTests/Rok.ApplicationTests/Rok.ApplicationTests.csproj /p:Platform=x64 --filter "DisplayName~add_should_persist_extended_metadata"
```

Expected: FAIL — `AddRadioStationRequest` doesn't have these 5 properties yet (compile error) OR they aren't copied to the entity.

- [ ] **Step 3: Extend `AddRadioStationRequest` + validator (single file edit)**

Replace `src/Rok.Application/Features/Radios/Requests/AddRadioStationRequest.cs` with:

```csharp
namespace Rok.Application.Features.Radios.Requests;

public class AddRadioStationRequest : IRequest<Result<long>>
{
    public string Name { get; set; } = string.Empty;

    public string StreamUrl { get; set; } = string.Empty;

    public string? HomepageUrl { get; set; }

    public string? StationUuid { get; set; }

    public string? FaviconUrl { get; set; }

    public string? CountryCode { get; set; }

    public string? Codec { get; set; }

    public int? Bitrate { get; set; }
}

public sealed class AddRadioStationRequestValidator : Validator<AddRadioStationRequest>
{
    public AddRadioStationRequestValidator()
    {
        Rule(x => x.Name).Required().MaxLength(200);
        Rule(x => x.StreamUrl).Required().Must(BeAbsoluteHttpUri).Message("Must be an absolute http(s) URL.");
        Rule(x => x.FaviconUrl).Must(BeAbsoluteHttpUriOrNull).Message("Must be an absolute http(s) URL or empty.");
        Rule(x => x.StationUuid).MaxLength(64);
        Rule(x => x.CountryCode).MaxLength(2);
        Rule(x => x.Codec).MaxLength(20);
        Rule(x => x.Bitrate).Must(b => b is null or >= 0).Message("Bitrate must be positive.");
    }

    private static bool BeAbsoluteHttpUri(string? value) =>
        value is not null &&
        Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static bool BeAbsoluteHttpUriOrNull(string? value) =>
        string.IsNullOrEmpty(value) || BeAbsoluteHttpUri(value);
}
```

- [ ] **Step 4: Extend `AddRadioStationRequestHandler`**

Open `src/Rok.Application/Features/Radios/Requests/AddRadioStationRequestHandler.cs`. Locate the entity construction and add the 5 new fields:

```csharp
RadioStationEntity entity = new()
{
    Name = message.Name,
    StreamUrl = message.StreamUrl,
    HomepageUrl = message.HomepageUrl,
    StationUuid = message.StationUuid,
    FaviconUrl = message.FaviconUrl,
    CountryCode = message.CountryCode,
    Codec = message.Codec,
    Bitrate = message.Bitrate,
    AddedAt = timeProvider.GetUtcNow().UtcDateTime
};
```

Leave the surrounding try/catch (with the `SqliteException 19 → ConflictError` mapping) unchanged.

- [ ] **Step 5: Re-run the test (green)**

```bash
dotnet test tests/UnitTests/Rok.ApplicationTests/Rok.ApplicationTests.csproj /p:Platform=x64 --filter "DisplayName~add_should_persist_extended_metadata"
```

Expected: PASS.

- [ ] **Step 6: Add `add_should_accept_nullable_extended_fields`**

```csharp
[Fact(DisplayName = "add_should_accept_nullable_extended_fields")]
public async Task Add_ShouldAcceptNullableExtendedFields()
{
    // Arrange
    _repository
        .Setup(r => r.AddAsync(It.IsAny<RadioStationEntity>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(7L);

    AddRadioStationRequest request = new()
    {
        Name = "Manual entry",
        StreamUrl = "https://stream.example/manual"
    };

    AddRadioStationRequestHandler handler = new(_repository.Object, _timeProvider);

    // Act
    Result<long> result = await handler.Handle(request, CancellationToken.None);

    // Assert
    result.Should().BeSuccess();
}
```

- [ ] **Step 7: Add `add_should_be_rejected_when_favicon_url_is_relative`**

The validator pipeline runs before the handler. Validation is enforced by sending through `IMediator` in the test (using `FakeMediator`) or by instantiating the validator directly. Follow whichever style the existing tests use for validation failures.

If validators are exercised directly, the test looks like:

```csharp
[Fact(DisplayName = "add_should_be_rejected_when_favicon_url_is_relative")]
public void Add_ShouldBeRejected_WhenFaviconUrlIsRelative()
{
    // Arrange
    AddRadioStationRequestValidator validator = new();
    AddRadioStationRequest request = new()
    {
        Name = "Test",
        StreamUrl = "https://stream.example/x",
        FaviconUrl = "favicon.ico"
    };

    // Act
    var result = validator.Validate(request);

    // Assert
    Assert.False(result.IsValid);
    Assert.Contains(result.Errors, e => e.PropertyName == nameof(AddRadioStationRequest.FaviconUrl));
}
```

If validators are exercised through the mediator pipeline (look at the existing AddRadioStationRequest tests for the pattern — a `FakeMediator` setup or a `ValidationException → ValidationError` Result), match that style instead.

- [ ] **Step 8: Run all three new tests**

```bash
dotnet test tests/UnitTests/Rok.ApplicationTests/Rok.ApplicationTests.csproj /p:Platform=x64 --filter "FullyQualifiedName~AddRadioStationRequestHandlerTests"
```

Expected: ALL PASS (plus the existing tests in the same class still PASS).

- [ ] **Step 9: Commit**

```bash
git add src/Rok.Application/Features/Radios/Requests/AddRadioStationRequest.cs src/Rok.Application/Features/Radios/Requests/AddRadioStationRequestHandler.cs tests/UnitTests/Rok.ApplicationTests/Features/Radios/Requests/AddRadioStationRequestHandlerTests.cs
git commit -m "feat(radio): accept and persist extended metadata in AddRadioStationRequest"
```

---

## Task 7 — Create `RadioBrowserOptions`, `RadioSearchResultDto`, `IRadioBrowserClient`

Pure additions, no test required at this stage (they are wiring + contracts).

**Files:**
- Create: `src/Rok.Application/Options/RadioBrowserOptions.cs`
- Create: `src/Rok.Application/Dto/RadioSearchResultDto.cs`
- Create: `src/Rok.Application/Features/Radios/Services/IRadioBrowserClient.cs`

- [ ] **Step 1: Create `RadioBrowserOptions`**

```csharp
namespace Rok.Application.Options;

public sealed class RadioBrowserOptions
{
    public string BaseAddress { get; set; } = "https://de1.api.radio-browser.info/";

    public int TimeoutSeconds { get; set; } = 8;

    public string UserAgent { get; set; } = "Rok/1.0";
}
```

- [ ] **Step 2: Create `RadioSearchResultDto`**

```csharp
namespace Rok.Application.Dto;

public record RadioSearchResultDto(
    string Name,
    string StreamUrl,
    string? HomepageUrl,
    string? StationUuid,
    string? FaviconUrl,
    string? CountryCode,
    string? Codec,
    int? Bitrate);
```

- [ ] **Step 3: Create `IRadioBrowserClient`**

```csharp
using Rok.Application.Dto;

namespace Rok.Application.Features.Radios.Services;

public interface IRadioBrowserClient
{
    Task<IReadOnlyList<RadioSearchResultDto>> SearchByNameAsync(
        string query,
        int limit,
        CancellationToken cancellationToken);
}
```

- [ ] **Step 4: Build**

```bash
dotnet build /p:Platform=x64
```

Expected: SUCCESS.

- [ ] **Step 5: Commit**

```bash
git add src/Rok.Application/Options/RadioBrowserOptions.cs src/Rok.Application/Dto/RadioSearchResultDto.cs src/Rok.Application/Features/Radios/Services/IRadioBrowserClient.cs
git commit -m "feat(radio): add RadioBrowserOptions, RadioSearchResultDto and IRadioBrowserClient seam"
```

---

## Task 8 — Implement `RadioBrowserClient` + mapping (TDD)

**Files:**
- Create: `src/Rok.Infrastructure/RadioBrowser/RadioBrowserStationResponse.cs`
- Create: `src/Rok.Infrastructure/RadioBrowser/Mapping/RadioBrowserStationMapping.cs`
- Create: `src/Rok.Infrastructure/RadioBrowser/RadioBrowserClient.cs`
- Test: `tests/UnitTests/Rok.Infrastructure.UnitTests/RadioBrowser/RadioBrowserClientTests.cs`

### Step 1: Write the first failing test

- [ ] **Step 1.1: Create the test file with the shared helper**

Create `tests/UnitTests/Rok.Infrastructure.UnitTests/RadioBrowser/RadioBrowserClientTests.cs`:

```csharp
using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Rok.Application.Dto;
using Rok.Infrastructure.RadioBrowser;

namespace Rok.Infrastructure.UnitTests.RadioBrowser;

public class RadioBrowserClientTests
{
    private static (RadioBrowserClient Client, List<HttpRequestMessage> Captured) CreateClient(string responseJson, HttpStatusCode status = HttpStatusCode.OK)
    {
        List<HttpRequestMessage> captured = [];
        Mock<HttpMessageHandler> handlerMock = new(MockBehavior.Strict);
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage req, CancellationToken _) =>
            {
                captured.Add(req);
                HttpResponseMessage resp = new(status)
                {
                    Content = new ByteArrayContent(Encoding.UTF8.GetBytes(responseJson))
                };
                resp.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                return resp;
            });

        HttpClient http = new(handlerMock.Object) { BaseAddress = new Uri("https://de1.api.radio-browser.info/") };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Rok/1.0");

        RadioBrowserClient client = new(http, NullLogger<RadioBrowserClient>.Instance);
        return (client, captured);
    }

    [Fact(DisplayName = "search_by_name_should_call_byname_endpoint_with_encoded_query")]
    public async Task SearchByName_ShouldCallByNameEndpoint_WithEncodedQuery()
    {
        // Arrange
        var (client, captured) = CreateClient("[]");

        // Act
        _ = await client.SearchByNameAsync("jazz fm", 50, CancellationToken.None);

        // Assert
        Assert.Single(captured);
        string url = captured[0].RequestUri!.ToString();
        Assert.Contains("/json/stations/byname/jazz%20fm", url);
        Assert.Contains("limit=50", url);
        Assert.Contains("hidebroken=true", url);
        Assert.Contains("order=votes", url);
        Assert.Contains("reverse=true", url);
    }
}
```

- [ ] **Step 2: Run the test to see it fail (red)**

```bash
dotnet test tests/UnitTests/Rok.Infrastructure.UnitTests/Rok.Infrastructure.UnitTests.csproj /p:Platform=x64 --filter "DisplayName~search_by_name_should_call_byname_endpoint"
```

Expected: FAIL — `RadioBrowserClient` doesn't exist.

- [ ] **Step 3: Create the JSON contract**

Create `src/Rok.Infrastructure/RadioBrowser/RadioBrowserStationResponse.cs`:

```csharp
using System.Text.Json.Serialization;

namespace Rok.Infrastructure.RadioBrowser;

internal sealed class RadioBrowserStationResponse
{
    [JsonPropertyName("stationuuid")] public string? StationUuid { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("url")] public string? Url { get; set; }
    [JsonPropertyName("url_resolved")] public string? UrlResolved { get; set; }
    [JsonPropertyName("homepage")] public string? Homepage { get; set; }
    [JsonPropertyName("favicon")] public string? Favicon { get; set; }
    [JsonPropertyName("countrycode")] public string? CountryCode { get; set; }
    [JsonPropertyName("codec")] public string? Codec { get; set; }
    [JsonPropertyName("bitrate")] public int? Bitrate { get; set; }
    [JsonPropertyName("lastcheckok")] public int? LastCheckOk { get; set; }
}
```

- [ ] **Step 4: Create the mapping**

Create `src/Rok.Infrastructure/RadioBrowser/Mapping/RadioBrowserStationMapping.cs`:

```csharp
using Rok.Application.Dto;

namespace Rok.Infrastructure.RadioBrowser.Mapping;

internal static class RadioBrowserStationMapping
{
    public static RadioSearchResultDto? ToDto(this RadioBrowserStationResponse r)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) return null;

        string? stream = !string.IsNullOrWhiteSpace(r.UrlResolved) ? r.UrlResolved : r.Url;
        if (string.IsNullOrWhiteSpace(stream)) return null;

        return new RadioSearchResultDto(
            Name: r.Name.Trim(),
            StreamUrl: stream.Trim(),
            HomepageUrl: NullIfEmpty(r.Homepage),
            StationUuid: NullIfEmpty(r.StationUuid),
            FaviconUrl: NullIfEmpty(r.Favicon),
            CountryCode: NullIfEmpty(r.CountryCode)?.ToLowerInvariant(),
            Codec: NullIfEmpty(r.Codec),
            Bitrate: r.Bitrate is > 0 ? r.Bitrate : null);
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
```

- [ ] **Step 5: Create `RadioBrowserClient`**

Create `src/Rok.Infrastructure/RadioBrowser/RadioBrowserClient.cs`:

```csharp
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Rok.Application.Dto;
using Rok.Application.Features.Radios.Services;
using Rok.Infrastructure.RadioBrowser.Mapping;

namespace Rok.Infrastructure.RadioBrowser;

internal sealed class RadioBrowserClient(HttpClient http, ILogger<RadioBrowserClient> logger)
    : IRadioBrowserClient
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<RadioSearchResultDto>> SearchByNameAsync(
        string query, int limit, CancellationToken ct)
    {
        string encoded = Uri.EscapeDataString(query);
        string path = $"json/stations/byname/{encoded}?limit={limit}&hidebroken=true&order=votes&reverse=true";

        logger.LogDebug("Radio-Browser search: query='{Query}' limit={Limit}", query, limit);

        using HttpResponseMessage response = await http.GetAsync(path, ct);
        response.EnsureSuccessStatusCode();

        RadioBrowserStationResponse[]? raw =
            await response.Content.ReadFromJsonAsync<RadioBrowserStationResponse[]>(JsonOpts, ct);

        if (raw is null) return [];

        return raw.Select(r => r.ToDto())
                  .Where(d => d is not null)
                  .Select(d => d!)
                  .ToArray();
    }
}
```

- [ ] **Step 6: Re-run the first test (green)**

```bash
dotnet test tests/UnitTests/Rok.Infrastructure.UnitTests/Rok.Infrastructure.UnitTests.csproj /p:Platform=x64 --filter "DisplayName~search_by_name_should_call_byname_endpoint"
```

Expected: PASS.

- [ ] **Step 7: Add the remaining test cases**

Append to `RadioBrowserClientTests`:

```csharp
[Fact(DisplayName = "search_by_name_should_attach_user_agent_header")]
public async Task SearchByName_ShouldAttachUserAgentHeader()
{
    var (client, captured) = CreateClient("[]");

    _ = await client.SearchByNameAsync("jazz", 10, CancellationToken.None);

    Assert.Contains(captured[0].Headers.UserAgent.ToString(), "Rok/1.0");
}

[Fact(DisplayName = "search_by_name_should_map_url_resolved_when_present")]
public async Task SearchByName_ShouldMapUrlResolved_WhenPresent()
{
    string json = """
        [{
            "name": "Jazz FM",
            "url": "https://stream.example/orig",
            "url_resolved": "https://stream.example/resolved.mp3",
            "stationuuid": "uuid-1",
            "favicon": "https://jazz.example/logo.png",
            "homepage": "https://jazz.example",
            "countrycode": "FR",
            "codec": "MP3",
            "bitrate": 128
        }]
        """;
    var (client, _) = CreateClient(json);

    IReadOnlyList<RadioSearchResultDto> results = await client.SearchByNameAsync("jazz", 50, CancellationToken.None);

    Assert.Single(results);
    Assert.Equal("https://stream.example/resolved.mp3", results[0].StreamUrl);
    Assert.Equal("Jazz FM", results[0].Name);
    Assert.Equal("fr", results[0].CountryCode);
    Assert.Equal("MP3", results[0].Codec);
    Assert.Equal(128, results[0].Bitrate);
}

[Fact(DisplayName = "search_by_name_should_fallback_to_url_when_resolved_missing")]
public async Task SearchByName_ShouldFallbackToUrl_WhenResolvedMissing()
{
    string json = """
        [{ "name": "TSF", "url": "https://stream.example/tsf", "url_resolved": "" }]
        """;
    var (client, _) = CreateClient(json);

    var results = await client.SearchByNameAsync("tsf", 50, CancellationToken.None);

    Assert.Equal("https://stream.example/tsf", results[0].StreamUrl);
}

[Fact(DisplayName = "search_by_name_should_skip_stations_without_name_or_url")]
public async Task SearchByName_ShouldSkipStations_WithoutNameOrUrl()
{
    string json = """
        [
            { "name": "Valid", "url": "https://stream.example/valid", "url_resolved": "https://stream.example/valid" },
            { "name": "",      "url": "https://stream.example/noname", "url_resolved": "https://stream.example/noname" },
            { "name": "No URL", "url": "", "url_resolved": "" }
        ]
        """;
    var (client, _) = CreateClient(json);

    var results = await client.SearchByNameAsync("x", 50, CancellationToken.None);

    Assert.Single(results);
    Assert.Equal("Valid", results[0].Name);
}

[Fact(DisplayName = "search_by_name_should_treat_bitrate_zero_as_unknown")]
public async Task SearchByName_ShouldTreatBitrateZero_AsUnknown()
{
    string json = """
        [{ "name": "A", "url": "https://stream.example/a", "url_resolved": "https://stream.example/a", "bitrate": 0 }]
        """;
    var (client, _) = CreateClient(json);

    var results = await client.SearchByNameAsync("a", 50, CancellationToken.None);

    Assert.Null(results[0].Bitrate);
}

[Fact(DisplayName = "search_by_name_should_return_empty_list_on_empty_response")]
public async Task SearchByName_ShouldReturnEmptyList_OnEmptyResponse()
{
    var (client, _) = CreateClient("[]");

    var results = await client.SearchByNameAsync("zzz", 50, CancellationToken.None);

    Assert.Empty(results);
}

[Fact(DisplayName = "search_by_name_should_throw_http_request_exception_on_500")]
public async Task SearchByName_ShouldThrowHttpRequestException_On500()
{
    var (client, _) = CreateClient("internal error", HttpStatusCode.InternalServerError);

    await Assert.ThrowsAsync<HttpRequestException>(() =>
        client.SearchByNameAsync("x", 50, CancellationToken.None));
}

[Fact(DisplayName = "search_by_name_should_apply_limit_parameter")]
public async Task SearchByName_ShouldApplyLimitParameter()
{
    var (client, captured) = CreateClient("[]");

    _ = await client.SearchByNameAsync("rock", 10, CancellationToken.None);

    Assert.Contains("limit=10", captured[0].RequestUri!.ToString());
}
```

- [ ] **Step 8: Run all `RadioBrowserClientTests`**

```bash
dotnet test tests/UnitTests/Rok.Infrastructure.UnitTests/Rok.Infrastructure.UnitTests.csproj /p:Platform=x64 --filter "FullyQualifiedName~RadioBrowserClientTests"
```

Expected: ALL PASS.

- [ ] **Step 9: Commit**

```bash
git add src/Rok.Infrastructure/RadioBrowser tests/UnitTests/Rok.Infrastructure.UnitTests/RadioBrowser
git commit -m "feat(radio): add RadioBrowserClient implementation with JSON mapping"
```

---

## Task 9 — Wire `RadioBrowserClient` into DI

**Files:**
- Modify: `src/Rok.Infrastructure/DependencyInjection.cs`
- Modify: `src/Presentation/App.xaml.cs`

- [ ] **Step 1: Register the typed client in Infrastructure DI**

Open `src/Rok.Infrastructure/DependencyInjection.cs`. Locate the existing `AddHttpClient(...)` calls (~line 93-99). Add after them:

```csharp
services.AddHttpClient<IRadioBrowserClient, RadioBrowserClient>((sp, client) =>
{
    RadioBrowserOptions opts = sp.GetRequiredService<IOptions<RadioBrowserOptions>>().Value;
    client.BaseAddress = new Uri(opts.BaseAddress);
    client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);
    client.DefaultRequestHeaders.UserAgent.ParseAdd(opts.UserAgent);
});
```

Add the following `using` statements at the top of the file if they aren't already present:

```csharp
using Microsoft.Extensions.Options;
using Rok.Application.Options;
using Rok.Infrastructure.RadioBrowser;
```

- [ ] **Step 2: Bind options in `App.xaml.cs`**

Open `src/Presentation/App.xaml.cs`. Find the block where other options are bound (search for `services.Configure<MusicDataApiOptions>` or `services.Configure<TelemetryOptions>`). Add:

```csharp
services.Configure<RadioBrowserOptions>(configuration.GetSection("RadioBrowser"));
```

Add `using Rok.Application.Options;` at the top if not present.

The binding tolerates a missing section: with no `"RadioBrowser"` block in `appsettings.json`, the default values from `RadioBrowserOptions` are used.

- [ ] **Step 3: Build**

```bash
dotnet build /p:Platform=x64
```

Expected: SUCCESS.

- [ ] **Step 4: Commit**

```bash
git add src/Rok.Infrastructure/DependencyInjection.cs src/Presentation/App.xaml.cs
git commit -m "chore(radio): register RadioBrowserClient and RadioBrowserOptions in DI"
```

---

## Task 10 — Implement `SearchRadioStationsRequest` + handler (TDD)

**Files:**
- Create: `src/Rok.Application/Features/Radios/Requests/SearchRadioStationsRequest.cs`
- Create: `src/Rok.Application/Features/Radios/Requests/SearchRadioStationsRequestHandler.cs`
- Test: `tests/UnitTests/Rok.ApplicationTests/Features/Radios/Requests/SearchRadioStationsRequestHandlerTests.cs`

### Step 1: Write the first failing test

- [ ] **Step 1.1: Create the test file**

```csharp
using Moq;
using Rok.Application.Dto;
using Rok.Application.Errors;
using Rok.Application.Features.Radios.Requests;
using Rok.Application.Features.Radios.Services;

namespace Rok.ApplicationTests.Features.Radios.Requests;

public class SearchRadioStationsRequestHandlerTests
{
    private readonly Mock<IRadioBrowserClient> _client = new();

    [Fact(DisplayName = "search_should_return_results_when_client_responds")]
    public async Task Search_ShouldReturnResults_WhenClientResponds()
    {
        // Arrange
        IReadOnlyList<RadioSearchResultDto> expected = new[]
        {
            new RadioSearchResultDto("Jazz FM", "https://s/jazz", null, null, null, null, null, null),
            new RadioSearchResultDto("TSF Jazz", "https://s/tsf", null, null, null, null, null, null),
        };
        _client.Setup(c => c.SearchByNameAsync("jazz", 50, It.IsAny<CancellationToken>()))
               .ReturnsAsync(expected);

        SearchRadioStationsRequestHandler handler = new(_client.Object);

        // Act
        var result = await handler.Handle(
            new SearchRadioStationsRequest { Query = "jazz", Limit = 50 },
            CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.Equal(2, result.Value.Count);
    }
}
```

- [ ] **Step 2: Run the test to see it fail (red)**

```bash
dotnet test tests/UnitTests/Rok.ApplicationTests/Rok.ApplicationTests.csproj /p:Platform=x64 --filter "DisplayName~search_should_return_results_when_client_responds"
```

Expected: FAIL — `SearchRadioStationsRequest`/`SearchRadioStationsRequestHandler` don't exist yet.

- [ ] **Step 3: Create the request + validator**

Create `src/Rok.Application/Features/Radios/Requests/SearchRadioStationsRequest.cs`:

```csharp
using CleanArch.DevKit.Mediator;
using CleanArch.DevKit.Mediator.Results;
using CleanArch.DevKit.Mediator.Validation;
using Rok.Application.Dto;

namespace Rok.Application.Features.Radios.Requests;

public sealed class SearchRadioStationsRequest : IRequest<Result<IReadOnlyList<RadioSearchResultDto>>>
{
    public string Query { get; set; } = string.Empty;

    public int Limit { get; set; } = 50;
}

public sealed class SearchRadioStationsRequestValidator : Validator<SearchRadioStationsRequest>
{
    public SearchRadioStationsRequestValidator()
    {
        Rule(x => x.Query).Required().MinLength(2).MaxLength(100);
        Rule(x => x.Limit).Must(l => l is > 0 and <= 200).Message("Limit must be between 1 and 200.");
    }
}
```

- [ ] **Step 4: Create the handler**

Create `src/Rok.Application/Features/Radios/Requests/SearchRadioStationsRequestHandler.cs`:

```csharp
using CleanArch.DevKit.Mediator;
using CleanArch.DevKit.Mediator.Results;
using Rok.Application.Dto;
using Rok.Application.Errors;
using Rok.Application.Features.Radios.Services;

namespace Rok.Application.Features.Radios.Requests;

public sealed class SearchRadioStationsRequestHandler(IRadioBrowserClient client)
    : IRequestHandler<SearchRadioStationsRequest, Result<IReadOnlyList<RadioSearchResultDto>>>
{
    public async Task<Result<IReadOnlyList<RadioSearchResultDto>>> Handle(
        SearchRadioStationsRequest message,
        CancellationToken cancellationToken)
    {
        try
        {
            IReadOnlyList<RadioSearchResultDto> results =
                await client.SearchByNameAsync(message.Query, message.Limit, cancellationToken);

            return Result<IReadOnlyList<RadioSearchResultDto>>.Ok(results);
        }
        catch (HttpRequestException ex)
        {
            return Result<IReadOnlyList<RadioSearchResultDto>>.Fail(
                new OperationError("radio.search_failed", ex.Message));
        }
        catch (TaskCanceledException)
        {
            return Result<IReadOnlyList<RadioSearchResultDto>>.Fail(
                new OperationError("radio.search_timeout", "Radio search timed out."));
        }
    }
}
```

- [ ] **Step 5: Re-run the test (green)**

```bash
dotnet test tests/UnitTests/Rok.ApplicationTests/Rok.ApplicationTests.csproj /p:Platform=x64 --filter "DisplayName~search_should_return_results_when_client_responds"
```

Expected: PASS.

- [ ] **Step 6: Add the remaining test cases**

```csharp
[Fact(DisplayName = "search_should_return_empty_when_no_match")]
public async Task Search_ShouldReturnEmpty_WhenNoMatch()
{
    _client.Setup(c => c.SearchByNameAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(Array.Empty<RadioSearchResultDto>());

    SearchRadioStationsRequestHandler handler = new(_client.Object);

    var result = await handler.Handle(new SearchRadioStationsRequest { Query = "zz", Limit = 50 }, CancellationToken.None);

    result.Should().BeSuccess();
    Assert.Empty(result.Value);
}

[Fact(DisplayName = "search_should_fail_with_search_failed_on_http_exception")]
public async Task Search_ShouldFailWithSearchFailed_OnHttpException()
{
    _client.Setup(c => c.SearchByNameAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
           .ThrowsAsync(new HttpRequestException("no network"));

    SearchRadioStationsRequestHandler handler = new(_client.Object);

    var result = await handler.Handle(new SearchRadioStationsRequest { Query = "x", Limit = 50 }, CancellationToken.None);

    result.Should().BeFailure().And.HaveErrorWithCode("radio.search_failed");
}

[Fact(DisplayName = "search_should_fail_with_search_timeout_on_task_canceled")]
public async Task Search_ShouldFailWithSearchTimeout_OnTaskCanceled()
{
    _client.Setup(c => c.SearchByNameAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
           .ThrowsAsync(new TaskCanceledException());

    SearchRadioStationsRequestHandler handler = new(_client.Object);

    var result = await handler.Handle(new SearchRadioStationsRequest { Query = "x", Limit = 50 }, CancellationToken.None);

    result.Should().BeFailure().And.HaveErrorWithCode("radio.search_timeout");
}

[Fact(DisplayName = "search_should_forward_cancellation_token")]
public async Task Search_ShouldForwardCancellationToken()
{
    CancellationTokenSource cts = new();
    _client.Setup(c => c.SearchByNameAsync(It.IsAny<string>(), It.IsAny<int>(), cts.Token))
           .ReturnsAsync(Array.Empty<RadioSearchResultDto>());

    SearchRadioStationsRequestHandler handler = new(_client.Object);

    _ = await handler.Handle(new SearchRadioStationsRequest { Query = "x", Limit = 50 }, cts.Token);

    _client.Verify(c => c.SearchByNameAsync("x", 50, cts.Token), Times.Once);
}

[Fact(DisplayName = "search_should_be_rejected_when_query_too_short")]
public void Search_ShouldBeRejected_WhenQueryTooShort()
{
    SearchRadioStationsRequestValidator validator = new();
    var result = validator.Validate(new SearchRadioStationsRequest { Query = "a", Limit = 50 });

    Assert.False(result.IsValid);
    Assert.Contains(result.Errors, e => e.PropertyName == nameof(SearchRadioStationsRequest.Query));
}

[Fact(DisplayName = "search_should_be_rejected_when_limit_out_of_range")]
public void Search_ShouldBeRejected_WhenLimitOutOfRange()
{
    SearchRadioStationsRequestValidator validator = new();
    var resultZero = validator.Validate(new SearchRadioStationsRequest { Query = "jazz", Limit = 0 });
    var resultHigh = validator.Validate(new SearchRadioStationsRequest { Query = "jazz", Limit = 300 });

    Assert.False(resultZero.IsValid);
    Assert.False(resultHigh.IsValid);
}
```

(If the `Validator<T>` from CleanArch.DevKit.Mediator.Validation exposes a different `.Validate(...)` method shape — e.g. async, or returning `ValidationResult` with different members — adapt the calls to match. Check the existing `SearchRadioStations`-adjacent validators or `AddRadioStationRequestValidator` for the canonical pattern in the codebase.)

- [ ] **Step 7: Run all `SearchRadioStationsRequestHandlerTests`**

```bash
dotnet test tests/UnitTests/Rok.ApplicationTests/Rok.ApplicationTests.csproj /p:Platform=x64 --filter "FullyQualifiedName~SearchRadioStationsRequestHandlerTests"
```

Expected: ALL PASS.

- [ ] **Step 8: Commit**

```bash
git add src/Rok.Application/Features/Radios/Requests/SearchRadioStationsRequest.cs src/Rok.Application/Features/Radios/Requests/SearchRadioStationsRequestHandler.cs tests/UnitTests/Rok.ApplicationTests/Features/Radios/Requests/SearchRadioStationsRequestHandlerTests.cs
git commit -m "feat(radio): add SearchRadioStationsRequest with handler and tests"
```

---

## Task 11 — Implement `SearchRadioStationsViewModel`

No automated tests (cohérent avec le projet — pas de tests UI/VM).

**Files:**
- Create: `src/Presentation/ViewModels/Radio/SearchRadioStationsViewModel.cs`
- Modify: `src/Presentation/DependencyInjection.cs`

- [ ] **Step 1: Create the ViewModel**

```csharp
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CleanArch.DevKit.Mediator;
using CleanArch.DevKit.Mediator.Results;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Windows.ApplicationModel.Resources;
using Rok.Application.Dto;
using Rok.Application.Errors;
using Rok.Application.Features.Radios.Requests;

namespace Rok.ViewModels.Radio;

public enum SearchFeedbackKind
{
    None,
    Success,
    Info,
    Error
}

public sealed partial class SearchRadioStationsViewModel : ObservableObject
{
    private readonly IMediator _mediator;
    private readonly ResourceLoader _resourceLoader;

    [ObservableProperty]
    private string _query = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoResults))]
    private bool _isSearching;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    [NotifyPropertyChangedFor(nameof(HasNoResults))]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoResults))]
    private bool _hasSearched;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFeedback))]
    private string? _feedbackMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFeedback))]
    private SearchFeedbackKind _feedbackKind = SearchFeedbackKind.None;

    public ObservableCollection<RadioSearchResultDto> Results { get; } = [];

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool HasFeedback => !string.IsNullOrEmpty(FeedbackMessage);

    public bool HasNoResults => HasSearched && !IsSearching && Results.Count == 0 && !HasError;

    public SearchRadioStationsViewModel(IMediator mediator, ResourceLoader resourceLoader)
    {
        _mediator = mediator;
        _resourceLoader = resourceLoader;
        Results.CollectionChanged += OnResultsChanged;
    }

    private void OnResultsChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        OnPropertyChanged(nameof(HasNoResults));

    [RelayCommand(CanExecute = nameof(CanSearch))]
    private async Task SearchAsync(CancellationToken ct)
    {
        IsSearching = true;
        ErrorMessage = null;
        FeedbackMessage = null;
        FeedbackKind = SearchFeedbackKind.None;
        Results.Clear();

        try
        {
            Result<IReadOnlyList<RadioSearchResultDto>> result =
                await _mediator.Send(
                    new SearchRadioStationsRequest { Query = Query.Trim(), Limit = 50 },
                    ct);

            HasSearched = true;

            if (result.IsFailure)
            {
                ErrorMessage = ResolveErrorMessage(result.Errors.First());
                return;
            }

            foreach (RadioSearchResultDto r in result.Value)
                Results.Add(r);
        }
        finally
        {
            IsSearching = false;
        }
    }

    private bool CanSearch() => !IsSearching && Query?.Trim().Length >= 2;

    [RelayCommand]
    private Task PlayAsync(RadioSearchResultDto r) =>
        _mediator.Send(new PlayRadioUrlRequest { Url = r.StreamUrl });

    [RelayCommand]
    private async Task AddToFavoritesAsync(RadioSearchResultDto r)
    {
        Result<long> result = await _mediator.Send(new AddRadioStationRequest
        {
            Name = r.Name,
            StreamUrl = r.StreamUrl,
            HomepageUrl = r.HomepageUrl,
            StationUuid = r.StationUuid,
            FaviconUrl = r.FaviconUrl,
            CountryCode = r.CountryCode,
            Codec = r.Codec,
            Bitrate = r.Bitrate,
        });

        if (result.IsSuccess)
            SetFeedback(_resourceLoader.GetString("radioFavoriteAdded"), SearchFeedbackKind.Success);
        else if (result.Errors.FirstOrDefault() is ConflictError)
            SetFeedback(_resourceLoader.GetString("radioFavoriteDuplicate"), SearchFeedbackKind.Info);
        else
            SetFeedback(ResolveErrorMessage(result.Errors.First()), SearchFeedbackKind.Error);
    }

    private void SetFeedback(string message, SearchFeedbackKind kind)
    {
        FeedbackMessage = message;
        FeedbackKind = kind;
    }

    public void ClearFeedback()
    {
        FeedbackMessage = null;
        FeedbackKind = SearchFeedbackKind.None;
    }

    private string ResolveErrorMessage(Error error)
    {
        string localized = _resourceLoader.GetString($"error.{error.Code}");
        return string.IsNullOrEmpty(localized) ? error.Message : localized;
    }
}
```

- [ ] **Step 2: Register the ViewModel as `Transient`**

In `src/Presentation/DependencyInjection.cs`, locate the ViewModel registrations (search for `RadiosViewModel`) and add:

```csharp
services.AddTransient<SearchRadioStationsViewModel>();
```

Add `using Rok.ViewModels.Radio;` at the top if it's not already present.

- [ ] **Step 3: Build**

```bash
dotnet build /p:Platform=x64
```

Expected: SUCCESS.

- [ ] **Step 4: Commit**

```bash
git add src/Presentation/ViewModels/Radio/SearchRadioStationsViewModel.cs src/Presentation/DependencyInjection.cs
git commit -m "feat(radio): add SearchRadioStationsViewModel"
```

---

## Task 12 — Implement `SearchRadioStationsDialog` (XAML + code-behind)

**Files:**
- Create: `src/Presentation/Dialogs/SearchRadioStationsDialog.xaml`
- Create: `src/Presentation/Dialogs/SearchRadioStationsDialog.xaml.cs`

- [ ] **Step 1: Create the XAML**

```xml
<ContentDialog x:Class="Rok.Dialogs.SearchRadioStationsDialog"
               xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
               xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
               xmlns:dto="using:Rok.Application.Dto"
               xmlns:converters="using:Rok.Converters"
               xmlns:vm="using:Rok.ViewModels.Radio"
               x:Uid="searchRadioDialog"
               x:Name="Root"
               CloseButtonText="Close"
               DefaultButton="None">

    <ContentDialog.Resources>
        <converters:CountryCodeToImageSourceConverter x:Key="CountryCodeToImageSource"/>
    </ContentDialog.Resources>

    <Grid MinWidth="540" MinHeight="480" RowSpacing="12">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
        </Grid.RowDefinitions>

        <!-- Search row -->
        <StackPanel Orientation="Horizontal" Spacing="8">
            <TextBox x:Name="QueryBox"
                     x:Uid="searchRadioQuery"
                     Width="380"
                     Text="{x:Bind ViewModel.Query, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"
                     KeyDown="OnQueryKeyDown"/>
            <Button x:Uid="searchRadioSubmit"
                    Command="{x:Bind ViewModel.SearchCommand}">
                <SymbolIcon Symbol="Find"/>
            </Button>
        </StackPanel>

        <!-- Error InfoBar -->
        <InfoBar Grid.Row="1"
                 Severity="Error"
                 IsClosable="True"
                 IsOpen="{x:Bind ViewModel.HasError, Mode=OneWay}"
                 Message="{x:Bind ViewModel.ErrorMessage, Mode=OneWay}"/>

        <!-- Transient feedback InfoBar -->
        <InfoBar Grid.Row="2"
                 x:Name="FeedbackBar"
                 IsClosable="True"
                 IsOpen="{x:Bind ViewModel.HasFeedback, Mode=OneWay}"
                 Message="{x:Bind ViewModel.FeedbackMessage, Mode=OneWay}"
                 CloseButtonClick="OnFeedbackCloseClick"/>

        <!-- Results area -->
        <Grid Grid.Row="3">
            <ProgressRing IsActive="{x:Bind ViewModel.IsSearching, Mode=OneWay}"
                          HorizontalAlignment="Center" VerticalAlignment="Center"/>

            <ListView ItemsSource="{x:Bind ViewModel.Results}"
                      SelectionMode="None"
                      IsItemClickEnabled="True"
                      ItemClick="OnResultItemClick">
                <ListView.ItemTemplate>
                    <DataTemplate x:DataType="dto:RadioSearchResultDto">
                        <Grid Padding="8" ColumnSpacing="12">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="44"/>
                                <ColumnDefinition Width="*"/>
                                <ColumnDefinition Width="Auto"/>
                                <ColumnDefinition Width="Auto"/>
                            </Grid.ColumnDefinitions>

                            <Border Width="40" Height="40" CornerRadius="6"
                                    Background="{ThemeResource BrandPrimaryBrush}">
                                <Image Source="{x:Bind FaviconUrl}" Stretch="UniformToFill"/>
                            </Border>

                            <StackPanel Grid.Column="1" VerticalAlignment="Center">
                                <TextBlock Text="{x:Bind Name}" FontWeight="SemiBold"/>
                                <TextBlock Foreground="{ThemeResource TextFillColorSecondaryBrush}"
                                           FontSize="12"
                                           Text="{x:Bind Codec, FallbackValue=''}"/>
                            </StackPanel>

                            <Image Grid.Column="2" Width="20" Height="14"
                                   Source="{x:Bind CountryCode, Converter={StaticResource CountryCodeToImageSource}}"/>

                            <StackPanel Grid.Column="3" Orientation="Horizontal" Spacing="4">
                                <Button x:Uid="searchRadioPlay"
                                        Command="{Binding ElementName=Root, Path=ViewModel.PlayCommand}"
                                        CommandParameter="{x:Bind}">
                                    <SymbolIcon Symbol="Play"/>
                                </Button>
                                <Button x:Uid="searchRadioAdd"
                                        Command="{Binding ElementName=Root, Path=ViewModel.AddToFavoritesCommand}"
                                        CommandParameter="{x:Bind}">
                                    <FontIcon Glyph="&#xE734;"/>
                                </Button>
                            </StackPanel>
                        </Grid>
                    </DataTemplate>
                </ListView.ItemTemplate>
            </ListView>

            <common:EmptyStateControl xmlns:common="using:Rok.Commons"
                                      x:Uid="searchRadioNoResults"
                                      Glyph="&#xE721;"
                                      Visibility="{x:Bind ViewModel.HasNoResults, Mode=OneWay, Converter={StaticResource BoolToVisibilityConverter}}"/>
        </Grid>
    </Grid>
</ContentDialog>
```

- [ ] **Step 2: Create the code-behind**

```csharp
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Rok.Application.Dto;
using Rok.ViewModels.Radio;
using Windows.System;

namespace Rok.Dialogs;

public sealed partial class SearchRadioStationsDialog : ContentDialog
{
    public SearchRadioStationsViewModel ViewModel { get; }

    private readonly DispatcherQueueTimer _feedbackTimer;

    public SearchRadioStationsDialog(SearchRadioStationsViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();

        _feedbackTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _feedbackTimer.Interval = TimeSpan.FromSeconds(2.5);
        _feedbackTimer.IsRepeating = false;
        _feedbackTimer.Tick += (_, _) => ViewModel.ClearFeedback();

        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ViewModel.FeedbackMessage) && ViewModel.HasFeedback)
            {
                if (ViewModel.FeedbackKind != SearchFeedbackKind.Error)
                {
                    _feedbackTimer.Stop();
                    _feedbackTimer.Start();
                }
                FeedbackBar.Severity = ViewModel.FeedbackKind switch
                {
                    SearchFeedbackKind.Success => InfoBarSeverity.Success,
                    SearchFeedbackKind.Info => InfoBarSeverity.Informational,
                    SearchFeedbackKind.Error => InfoBarSeverity.Error,
                    _ => InfoBarSeverity.Informational
                };
            }
        };
    }

    private void OnQueryKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && ViewModel.SearchCommand.CanExecute(null))
        {
            _ = ViewModel.SearchCommand.ExecuteAsync(null);
            e.Handled = true;
        }
    }

    private void OnResultItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is RadioSearchResultDto r)
            _ = ViewModel.PlayCommand.ExecuteAsync(r);
    }

    private void OnFeedbackCloseClick(InfoBar sender, object args) =>
        ViewModel.ClearFeedback();
}
```

- [ ] **Step 3: Build**

```bash
dotnet build /p:Platform=x64
```

Expected: SUCCESS. If XAML compile errors come from `common:EmptyStateControl` namespace declaration, match the same approach used in `RadiosPage.xaml` (declare `xmlns:common="using:Rok.Commons"` at the root element, not on the child).

- [ ] **Step 4: Commit**

```bash
git add src/Presentation/Dialogs/SearchRadioStationsDialog.xaml src/Presentation/Dialogs/SearchRadioStationsDialog.xaml.cs
git commit -m "feat(radio): add SearchRadioStationsDialog with results list and feedback"
```

---

## Task 13 — Add the "Search" button on `RadiosPage`

**Files:**
- Modify: `src/Presentation/Pages/RadiosPage.xaml`
- Modify: `src/Presentation/Pages/RadiosPage.xaml.cs`

- [ ] **Step 1: Add the AppBarButton in `RadiosPage.xaml`**

Locate the `CommandBar.Content` block (around line 76-92). Insert this `AppBarButton` as the **first** child of the `StackPanel`, before `radiosPlayUrl`:

```xml
<AppBarButton x:Uid="radiosSearch"
              Style="{StaticResource AppBarButtonCompactStyle}"
              Click="OnSearchClick">
    <AppBarButton.Icon>
        <SymbolIcon Symbol="Find"/>
    </AppBarButton.Icon>
</AppBarButton>
```

- [ ] **Step 2: Add the `OnSearchClick` handler in `RadiosPage.xaml.cs`**

Add this method to the class:

```csharp
private async void OnSearchClick(object sender, RoutedEventArgs e)
{
    SearchRadioStationsDialog dialog = new(
        App.ServiceProvider.GetRequiredService<SearchRadioStationsViewModel>())
    {
        XamlRoot = XamlRoot
    };

    await dialog.ShowAsync();
    await ViewModel.LoadAsync();
}
```

Add `using Rok.ViewModels.Radio;` at the top if not already present.

- [ ] **Step 3: Build**

```bash
dotnet build /p:Platform=x64
```

Expected: SUCCESS.

- [ ] **Step 4: Commit**

```bash
git add src/Presentation/Pages/RadiosPage.xaml src/Presentation/Pages/RadiosPage.xaml.cs
git commit -m "feat(radio): add Search button to RadiosPage command bar"
```

---

## Task 14 — Add localization keys (FR + EN)

**Files:**
- Modify: `src/Presentation/Strings/fr-FR/Resources.resw` (or whatever the FR locale path is in this project)
- Modify: `src/Presentation/Strings/en-US/Resources.resw` (likewise for EN)

> **Note:** the exact folder name depends on the existing layout. Locate the files using `git ls-files | grep -i Resources.resw` if uncertain. Add all keys to BOTH locales.

- [ ] **Step 1: Add the FR strings**

In the FR `Resources.resw`, add (between existing `<data>` nodes, alphabetized if the file is ordered):

| Name | Value |
|---|---|
| `radiosSearch.[Label]` | `Rechercher` |
| `radiosSearch.[ToolTipService.ToolTip]` | `Rechercher des webradios` |
| `searchRadioDialog.Title` | `Rechercher une webradio` |
| `searchRadioDialog.CloseButtonText` | `Fermer` |
| `searchRadioQuery.PlaceholderText` | `Nom de la station...` |
| `searchRadioSubmit.[ToolTipService.ToolTip]` | `Rechercher` |
| `searchRadioPlay.[ToolTipService.ToolTip]` | `Lire` |
| `searchRadioAdd.[ToolTipService.ToolTip]` | `Ajouter aux favoris` |
| `searchRadioNoResults.Title` | `Aucun résultat` |
| `searchRadioNoResults.Subtitle` | `Essaie un autre nom.` |
| `error.radio.search_failed` | `Impossible de joindre Radio-Browser. Vérifie ta connexion.` |
| `error.radio.search_timeout` | `La recherche a expiré, réessaie.` |
| `radioFavoriteAdded` | `Ajoutée aux favoris.` |
| `radioFavoriteDuplicate` | `Cette station est déjà dans tes favoris.` |

XML form for each entry:

```xml
<data name="radiosSearch.[Label]" xml:space="preserve">
    <value>Rechercher</value>
</data>
```

- [ ] **Step 2: Add the EN strings**

Same keys, English values:

| Name | Value |
|---|---|
| `radiosSearch.[Label]` | `Search` |
| `radiosSearch.[ToolTipService.ToolTip]` | `Search webradios` |
| `searchRadioDialog.Title` | `Search webradio` |
| `searchRadioDialog.CloseButtonText` | `Close` |
| `searchRadioQuery.PlaceholderText` | `Station name...` |
| `searchRadioSubmit.[ToolTipService.ToolTip]` | `Search` |
| `searchRadioPlay.[ToolTipService.ToolTip]` | `Play` |
| `searchRadioAdd.[ToolTipService.ToolTip]` | `Add to favorites` |
| `searchRadioNoResults.Title` | `No results` |
| `searchRadioNoResults.Subtitle` | `Try a different name.` |
| `error.radio.search_failed` | `Cannot reach Radio-Browser. Check your connection.` |
| `error.radio.search_timeout` | `Search timed out, try again.` |
| `radioFavoriteAdded` | `Added to favorites.` |
| `radioFavoriteDuplicate` | `This station is already in your favorites.` |

- [ ] **Step 3: Build**

```bash
dotnet build /p:Platform=x64
```

Expected: SUCCESS.

- [ ] **Step 4: Commit**

```bash
git add src/Presentation/Strings
git commit -m "i18n(radio): localize SearchRadioStationsDialog (FR/EN)"
```

---

## Task 15 — Full verification and manual smoke test

**Files:** none modified — verification only.

- [ ] **Step 1: Format the whole solution**

```bash
dotnet format /p:Platform=x64
```

- [ ] **Step 2: Build the whole solution**

```bash
dotnet build /p:Platform=x64
```

Expected: zero warnings, zero errors.

- [ ] **Step 3: Run the full test suite**

```bash
dotnet test /p:Platform=x64
```

Expected: all tests PASS, including ~17-20 new ones added in this plan.

- [ ] **Step 4: Manual smoke test**

Launch the WinUI app, navigate to the Radios page, and verify:

1. **Search button** is visible in the `CommandBar`, first position (left of the existing buttons).
2. Click it → the `SearchRadioStationsDialog` opens.
3. Type `france inter`, press `Enter` → results appear within a few seconds.
4. Click on a result card → the radio starts playing in the background, the dialog stays open.
5. Click the ★ button on a different result → InfoBar (Success) "Ajoutée aux favoris." appears for ~2.5 s.
6. Click the ★ button again on the same result → InfoBar (Info) "Cette station est déjà dans tes favoris." appears.
7. Type `azertyuiop12345xxxx` → empty results, `EmptyStateControl` shows "Aucun résultat".
8. Disconnect the network (or block `de1.api.radio-browser.info` via `hosts`) → search → InfoBar (Error) "Impossible de joindre Radio-Browser..." appears.
9. Restore network, close the dialog → returning to the page reloads the favorites grid; the station added in step 5 appears with its favicon (if any) and country flag.

If any of these scenarios fails, debug at the relevant layer and add a corrective commit. Do NOT consider the task complete until step 4 passes end-to-end.

- [ ] **Step 5: Commit if any housekeeping changes were made by `dotnet format`**

```bash
git status
# if there are changes:
git add -A
git commit -m "chore(radio): apply dotnet format"
```

---

## Done

After Task 15 passes, the webradio search feature is complete:
- 5 new persisted fields on `RadioStationEntity` (Migration12).
- Typed `RadioBrowserClient` querying `de1.api.radio-browser.info/json/stations/byname/...`.
- `SearchRadioStationsRequest` use case.
- `SearchRadioStationsDialog` opened from the Radios page with click-to-play preview and ★-to-favorite.
- FR/EN localization.
- ~17-20 new unit tests, ~5 amended existing tests.
- Zero warnings, full test suite green.
