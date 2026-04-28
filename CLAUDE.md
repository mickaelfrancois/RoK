# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Rok is a Windows desktop music player built with **.NET 10**, **C# 13** (`<LangVersion>preview</LangVersion>`), and **WinUI 3** (Windows App SDK 1.8). It manages and plays a local music collection with library browsing, metadata editing, playlists, Discord rich-presence, and telemetry.

`Directory.Build.props` enables nullable, implicit usings, and **`TreatWarningsAsErrors=true`** for every project — code must compile clean. Target platforms: `x86;x64;ARM64` (the `Rok` Presentation project requires a platform; default to `x64`).

## Build, test, format

The solution is `Rok.slnx` (XML solution format). Most local commands need `/p:Platform=x64` because the Presentation project does not build under `AnyCPU`.

```bash
# Restore tools (husky, conventional-commits) once after clone
dotnet tool restore

# Build everything
dotnet build /p:Platform=x64

# Run all tests (build must already be up to date for --no-build)
dotnet test /p:Platform=x64

# Run a single test project
dotnet test tests/UnitTests/Rok.ApplicationTests/Rok.ApplicationTests.csproj /p:Platform=x64

# Run a single test by fully-qualified name or filter
dotnet test /p:Platform=x64 --filter "FullyQualifiedName~PlayerServiceTests"
dotnet test /p:Platform=x64 --filter "DisplayName~when_track_ends"

# Format (also runs automatically on staged .cs files via husky pre-commit)
dotnet format /p:Platform=x64
```

Husky-driven git hooks live in `.husky/` (`task-runner.json`):
- **pre-commit**: `dotnet build --no-restore -v quiet /p:Platform=x64` + `dotnet format` on staged `*.cs`
- **pre-push**: `dotnet test --no-build /p:Platform=x64`
- **commit-msg**: enforces Conventional Commits (`feat|fix|docs|style|refactor|test|chore|build|ci|perf|revert(scope)?: …`)

`appsettings.json` is gitignored; copy `src/Presentation/appsettings.template.json` to `src/Presentation/appsettings.json` and fill in the API keys (Telemetry, MusicDataApi, Discord) before running the app.

## Architecture

Clean / layered architecture with a strict dependency direction `Presentation → Application → Domain`, with `Infrastructure` and `Import` plugged in at the edges. Inner layers never reference outer ones.

```
src/
  Rok.Domain/           Entities, enums, repository interfaces. Zero deps.
  Rok.Shared/           Cross-cutting helpers (collections, extensions, validation). Zero domain deps.
  Rok.Application/      Use cases (Features/<Area>/Command|Query), CQRS handlers via MiF.Mediator,
                        application services (PlayerService, PlaylistService, ReviewPromptEligibilityService),
                        DTOs, options, decoupled messages (MiF.Messenger). Tests have InternalsVisibleTo.
  Rok.Infrastructure/   Dapper + SQLite repositories, AppDbContext, schema Migrations (Migration2..N +
                        MigrationService), TagLibSharp tag IO, NAudio playback engine
                        (Player/NAudioMediaPlayer + MediaPlayerEngine), LastFm/MusicData/Translate HTTP
                        clients, Discord rich presence, telemetry, Serilog wiring, file system services.
  Rok.Import/           Library scan + import pipeline (ImportService, AlbumImport, ArtistImport,
                        GenreImport, TrackImport, CleanLibraryService) — runs on a thread pool to keep
                        the UI responsive.
  Presentation/ (Rok)   WinUI 3 head: App.xaml(.cs), Pages/, ViewModels/, Dialogs/, Commons/,
                        Converters/, Mapping/, Services/. WinExe targeting net10.0-windows10.0.26100.
                        Composes the app via DependencyInjection.AddLogic() and registers ALL
                        ViewModels + per-feature services/handlers there.
```

### CQRS feature layout

Application features follow the same shape:

```
Rok.Application/Features/<Area>/
  Command/    *CommandHandler.cs         (mutations)
  Query/      *QueryHandler.cs           (reads)
  Services/   feature-scoped helpers
```

Handlers are dispatched through **MiF.Mediator** (`AddSimpleMediator()` in `Rok.Application/DependencyInjection.cs`). Cross-cutting domain events flow through **MiF.Messenger** (e.g. `AlbumImportedMessageHandler`, `TrackImportedMessageHandler` in Presentation listen to messages emitted by Application/Import).

### Presentation composition

`Presentation/DependencyInjection.cs` is the canonical place to register a new ViewModel or feature service — every ViewModel, data loader, selection/state manager, playback service, message handler, and provider for Albums/Artists/Tracks/Playlists/Genre/Player is wired here. Some "search" variants are registered as **keyed services** (`AddKeyedTransient<…>("SearchAlbums", …)`) to give the SearchPage a separate VM instance with a tailored constructor. Follow that pattern when adding a parallel context for an existing VM.

ViewModels use `CommunityToolkit.Mvvm` (`ObservableObject`, source-gen commands). They MUST NOT reference UI elements — keep XAML/code-behind out of VM logic.

### Persistence

- SQLite via Dapper. Repositories live in `Rok.Infrastructure/Repositories/`; new tables/columns are added through a new `MigrationN` class and registered in `MigrationService`. The current head is `Migration10`.
- `DateOnlyTypeHandler` is registered globally for Dapper.
- Domain entities use `[Table("…")]` attributes (see `Rok.Domain/Attributes/`).

### Audio engine

`NAudio` is the playback engine. The seam is `IPlayerEngine` (Application) implemented by `MediaPlayerEngine` / `NAudioMediaPlayer` (Infrastructure). Player orchestration (queue, sleep timer, crossfade, listen tracking) lives in `Rok.Application/Player/` with the user-facing `PlayerViewModel` + helpers (`PlayerStateManager`, `PlayerListenTracker`, `PlayerLyricsService`, `PlayerTimerManager`) registered in Presentation.

## Testing

xUnit + Moq + coverlet, with `Microsoft.Extensions.TimeProvider.Testing` for deterministic time. Test projects mirror the source layout:

```
tests/UnitTests/
  Rok.ApplicationTests/
  Rok.ImportTests/
  Rok.Infrastructure.UnitTests/   uses SqliteDatabaseFixture (real in-memory SQLite + all migrations + seed data)
  Rok.PresentationTests/          targets x86/x64/ARM64 like the Presentation project
tests/PerfTests/Rok.PerfTests/
```

Conventions enforced in the codebase:
- **AAA** (Arrange / Act / Assert) layout.
- Test class name = `<TypeUnderTest>Tests`, organized by feature folder.
- `DisplayName` on `[Fact]` / `[Theory]` is **English**, snake-style sentences (e.g. `when_track_ends_playlist_advances`).
- Infrastructure tests use the real SQLite fixture rather than mocking the DB. Application code must remain mockable (`InternalsVisibleTo("Rok.ApplicationTests")` is set on `Rok.Application`).

## Conventions

From `.github/copilot-instructions.md` and `.editorconfig`:

- **All code and identifiers in English.** Comments only when strictly necessary; prefer self-documenting code. No regions.
- **Never use collection expressions** (`dotnet_style_prefer_collection_expression = never`) — use explicit `new List<T>()` / `new[] { … }`.
- Braces on their own line; blank lines around conditions, loops, logical blocks; prefer early return.
- `var` only when the type is obvious from the RHS (`new`, casts, literals).
- `async`/`await` everywhere — no synchronous I/O, no static service locators.
- `IOptions<T>` for configuration, `record` for immutable models, interfaces for all services, factory pattern for audio backends.
- ViewModels never touch UI controls; keep code-behind minimal.
- XML doc comments on public APIs.
- All builds must be **warning-free** (treat-warnings-as-errors is on globally).

## Conventional Commits (enforced)

`<type>(<scope>): <description>` — types: `feat, fix, docs, style, refactor, test, chore, build, ci, perf, revert`. Breaking changes need a `BREAKING CHANGE:` footer. The commit-msg hook will reject anything else.
