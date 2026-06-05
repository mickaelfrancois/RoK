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

`appsettings.json` is gitignored — and so is `appsettings.template.json`, because the `appsettings.*.json` rule in `.gitignore` matches it too, so neither file is in the repo. Create `src/Presentation/appsettings.json` yourself with the sections bound in `App.xaml.cs` (`Telemetry`, `NovaApi`, `MusicDataApi`, `TranslateApi`, `Discord`, `RadioBrowser`) and fill in the API keys before running the app.

## Architecture

Clean / layered architecture with a strict dependency direction `Presentation → Application → Domain`, with `Infrastructure` and `Import` plugged in at the edges. Inner layers never reference outer ones.

```
src/
  Rok.Domain/           Entities, enums, repository interfaces. Zero deps.
  Rok.Shared/           Cross-cutting helpers (collections, extensions, validation). Zero domain deps.
  Rok.Application/      Use cases (Features/<Area>/Requests/), CQRS handlers via CleanArch.DevKit.Mediator,
                        application services (PlayerService, PlaylistService, ReviewPromptEligibilityService),
                        DTOs, options, decoupled messages (CleanArch.DevKit.Messaging). Tests have InternalsVisibleTo.
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
  Requests/   *Request.cs + *RequestHandler.cs + *RequestValidator.cs
              (mutations and reads colocated; handlers implement IRequestHandler<TReq, TResp>)
  Services/   feature-scoped helpers
```

Handlers are dispatched through **CleanArch.DevKit.Mediator** (`AddMediator()` in `Rok.Application/DependencyInjection.cs`). The mediator is source-generated (Roslyn) for zero-reflection dispatch — `public partial class Mediator { }` must exist in any project that registers handlers (already in `Rok.Application/Mediator.cs` and `tests/.../Rok.ApplicationTests/Mediator.cs`).

Request validation uses **CleanArch.DevKit.Mediator.Validation** (FluentValidation-style rule builders, `Validator<TRequest>` colocated next to each `Request`). The validation pipeline behavior throws `ValidationException`, automatically converted to `Result.Fail(ValidationError)` by `ResultBehavior` for Result-returning handlers.

Result/error pattern uses **CleanArch.DevKit.Mediator.Results** (`Result` / `Result<T>` structs + `Error` records: `NotFoundError`, `ConflictError`, `ValidationError`, `OperationError` from `Rok.Application.Errors`).

Cross-cutting runtime events flow through **CleanArch.DevKit.Messaging** (`IMessenger`, instance-based, injected via DI). Subscribers receive an `IDisposable` token that **must** be disposed (typically via a `List<IDisposable> _subscriptions` field + dispose loop in `Dispose()`). Examples: `AlbumImportedMessageHandler`, `TrackImportedMessageHandler` in Presentation listen to messages emitted by Application/Import.

Pipeline behaviors registered (in execution order, outermost → innermost): `ValidationBehavior` → `ResultBehavior` → `UnhandledExceptionBehavior` → `LoggingBehavior` → `PerformanceBehavior` (from `CleanArch.DevKit.Mediator.Behaviors`, wired via `AddCommonBehaviors()`).

### Presentation composition

`Presentation/DependencyInjection.cs` is the canonical place to register a new ViewModel or feature service — every ViewModel, data loader, selection/state manager, playback service, message handler, and provider for Albums/Artists/Tracks/Playlists/Genre/Player is wired here. Some "search" variants are registered as **keyed services** (`AddKeyedTransient<…>("SearchAlbums", …)`) to give the SearchPage a separate VM instance with a tailored constructor. Follow that pattern when adding a parallel context for an existing VM.

ViewModels use `CommunityToolkit.Mvvm` (`ObservableObject`, source-gen commands). They MUST NOT reference UI elements — keep XAML/code-behind out of VM logic.

### Persistence

- SQLite via Dapper. Repositories live in `Rok.Infrastructure/Repositories/`; new tables/columns are added through a new `MigrationN` class implementing `IMigration` and registered in `Rok.Infrastructure/DependencyInjection.cs` (discovered by `MigrationService` via `IEnumerable<IMigration>`). The current head is `Migration12`.
- `DateOnlyTypeHandler` is registered globally for Dapper.
- Domain entities use `[Table("…")]` attributes (see `Rok.Domain/Attributes/`).
- A real test database (populated with the developer's library) is available locally at
  `C:\Users\micka\AppData\Local\Packages\dev-Rokapp.Rokmusicplayer_k3w9s3grwk0dt\LocalState\database.sqlite`
  (MSIX LocalState of the installed app). Use it to validate query changes against real data
  (e.g. `sqlite3 <path> "EXPLAIN QUERY PLAN …"`) — open it read-only and never modify it.

### Audio engine

`NAudio` is the playback engine. The seam is `IPlayerEngine` (Application) implemented by `MediaPlayerEngine` / `NAudioMediaPlayer` (Infrastructure). Player orchestration (queue, sleep timer, crossfade, listen tracking) lives in `Rok.Application/Player/` with the user-facing `PlayerViewModel` + helpers (`PlayerStateManager`, `PlayerListenTracker`, `PlayerLyricsService`, `PlayerTimerManager`) registered in Presentation.

## Testing

xUnit + Moq + coverlet, with `Microsoft.Extensions.TimeProvider.Testing` for deterministic time. `IMediator` is stubbed in tests via `FakeMediator` from `CleanArch.DevKit.Mediator.Testing` (instead of `Mock<IMediator>`): `_mediator.Setup<TRequest, TResponse>().Returns(value)` + `Assert.Single(_mediator.Sent<TRequest>())`. Result assertions use the fluent `.Should()` API from `CleanArch.DevKit.Mediator.Results.Testing` (e.g., `result.Should().BeFailure().And.HaveError<NotFoundError>().And.HaveErrorWithCode("track.not_found")`). Test projects mirror the source layout:

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
- Braces on their own line; blank lines around conditions, loops, logical blocks; prefer early return.
- `var` only when the type is obvious from the RHS (`new`, casts, literals).
- `async`/`await` everywhere — no synchronous I/O, no static service locators.
- `IOptions<T>` for configuration, `record` for immutable models, interfaces for all services, factory pattern for audio backends.
- ViewModels never touch UI controls; keep code-behind minimal.
- XML doc comments on public APIs.
- All builds must be **warning-free** (treat-warnings-as-errors is on globally).

## Conventional Commits (enforced)

`<type>(<scope>): <description>` — types: `feat, fix, docs, style, refactor, test, chore, build, ci, perf, revert`. Breaking changes need a `BREAKING CHANGE:` footer. The commit-msg hook will reject anything else.

## Branch policy

**Never commit directly to `master` or `main`.** Before making any code change, check the current branch with `git branch --show-current`. If on `master` or `main`, create a feature branch first:

```bash
git checkout -b <type>/<short-description>
# e.g. git checkout -b feat/onboarding-error-state
```

Ask the user for a branch name if unsure, or propose one based on the work at hand.
