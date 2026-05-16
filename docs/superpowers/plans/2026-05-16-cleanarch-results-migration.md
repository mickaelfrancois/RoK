# Implementation plan — Step 2 : `MiF.Result` → `CleanArch.DevKit.Mediator.Results`

> Spec : `2026-05-16-cleanarch-results-migration-design.md`. Branche stacked sur `refactor/migrate-to-cleanarch-mediator` : `refactor/migrate-to-cleanarch-results`.

## Préambule

Mêmes règles que l'étape 1 :
- Subagents pour les tâches mécaniques (par feature)
- **Toujours `git add -A` après édition** (rappel post-bug récurrent étape 1) ; vérifier `git diff --cached --stat` montre +/- réels avant commit
- Conventional Commits, `refactor(app):` ou `refactor(presentation):` selon scope
- Build x64 + tests verts avant chaque commit

## T1. Foundation — package, GlobalUsings, OperationError, DI

**Fichiers** :
- `src/Rok.Application/Rok.Application.csproj` : retirer `MiF.Result`, ajouter `CleanArch.DevKit.Mediator.Results` 1.0.0
- `src/Rok.Application/GlobalUsings.cs` : `MiF.Result` → `CleanArch.DevKit.Mediator.Results`, ajouter `Rok.Application.Errors`
- `src/Presentation/GlobalUsings.cs` : idem
- `tests/UnitTests/Rok.ApplicationTests/GlobalUsings.cs` : idem
- `tests/UnitTests/Rok.ImportTests/GlobalUsings.cs` : ajouter `CleanArch.DevKit.Mediator.Results` si nécessaire
- **Nouveau** `src/Rok.Application/Errors/OperationError.cs` :

```csharp
namespace Rok.Application.Errors;

using CleanArch.DevKit.Mediator.Results;

public sealed record OperationError(string Code, string Message) : Error(Code, Message);
```

- `src/Rok.Application/DependencyInjection.cs` : ajouter `services.AddResultBehavior();` après `services.AddValidationBehavior();`

Build après T1 attendu : rouge (Result.Success/.Fail/.IsError n'existent plus). C'est normal — résolu par T2-T8.

**Commit** : `refactor(app): swap MiF.Result for CleanArch.DevKit.Mediator.Results + OperationError`

## T2. Migrer feature **Tracks** (pattern de référence)

5 handlers : `GetTrackByIdRequestHandler`, `UpdateTrackLastListenRequestHandler`, `UpdateTrackGetLyricsLastAttemptRequestHandler`, `UpdateSkipCountRequestHandler`, `UpdateScoreRequestHandler`, `ResetTrackListenCountRequestHandler`.

Patterns appliqués :

```csharp
// Avant
return Result<TrackDto>.Fail("NotFound", "Track not found");
return Result<bool>.Fail("Failed to update last listen.");

// Après
return Result<TrackDto>.Fail(NotFoundError.ForEntity("Track", message.Id));
return Result<bool>.Fail(new OperationError("track.last_listen_update_failed", "Failed to update last listen."));
```

Tests à adapter : `tests/UnitTests/Rok.ApplicationTests/Features/Tracks/Requests/Track*HandlerTests.cs`.

Pattern test :

```csharp
// Avant
Assert.Equal("NotFound", result.Error?.Code);

// Après
Assert.True(result.IsFailure);
Assert.IsType<NotFoundError>(result.Errors[0]);
Assert.Equal("track.not_found", result.Errors[0].Code);
```

**Commit** : `refactor(app): migrate Tracks handlers to CleanArch.DevKit.Mediator.Results`

## T3. Migrer feature **Albums** (12 handlers + tests)

Mêmes patterns que T2. Handler clé : `GetAlbumByIdRequestHandler` (NotFound), `UpdateAlbumRequestHandler` (NotFound inline + Failed), `UpdateAlbumTagsRequestHandler` (Failed).

**Commit** : `refactor(app): migrate Albums handlers to CleanArch.DevKit.Mediator.Results`

## T4. Migrer feature **Artists** (13 handlers + tests)

Idem.

**Commit** : `refactor(app): migrate Artists handlers to CleanArch.DevKit.Mediator.Results`

## T5. Migrer feature **Genres** (4 handlers + tests)

Idem. Préserver le typo `UpdateGenretLastListen`.

**Commit** : `refactor(app): migrate Genres handlers to CleanArch.DevKit.Mediator.Results`

## T6. Migrer feature **Playlists** (14 handlers + tests) — inclut fix DUPLICATE

Particularités :
- `AddTrackToPlaylistRequestHandler.cs:31` : **corriger arguments inversés** vers `Fail(new ConflictError("playlist.duplicate_track", "Track already exists in the playlist."))`
- `RemoveTrackFromPlaylistRequestHandler`, `AddArtistToPlaylistRequestHandler`, `AddAlbumToPlaylistRequestHandler` : "Track not found." inline → `NotFoundError.ForEntity("Track", trackId)` (utiliser l'id de la request)
- `ExportPlaylistRequestHandler`, `ImportPlaylistRequestHandler` : codes courts (`UnsupportedFormat`, `ParseError`, `DatabaseError`, `NameCollisionExhausted`, `WriteError`, `PlaylistNotFound`) → `new OperationError("playlist.unsupported_format", "Unsupported format")` etc.

**Commit** : `refactor(app): migrate Playlists handlers to CleanArch.DevKit.Mediator.Results (+ fix duplicate-track error)`

## T7. Migrer feature **EqualizerPresets** (3 handlers + tests)

`GetEqualizerPresetRequestHandler` : `Fail("NotFound", "Equalizer preset not found")` → `Fail(NotFoundError.ForEntity("EqualizerPreset", message.Scope))` ou similaire.

**Commit** : `refactor(app): migrate EqualizerPresets handlers to CleanArch.DevKit.Mediator.Results`

## T8. Migrer feature **ListeningEvents** (1 handler + tests)

`CreateListeningEventRequestHandler` : `Fail("Failed to create listening event.")` → `Fail(new OperationError("listening_event.create_failed", "..."))`.

**Commit** : `refactor(app): migrate ListeningEvents handler to CleanArch.DevKit.Mediator.Results`

## T9. Mettre à jour consumers Presentation + Import

Fichiers Presentation (`.IsError` → `.IsFailure`, `.Error?...` → `.Errors[0]?...`) :
- `ArtistsDataLoader.cs`, `AlbumsDataLoader.cs`, `PlaylistsDataLoader.cs`, `PlaylistCreationService.cs` (2x), `PlaylistUpdateService.cs`, `PlayerDataLoader.cs` (2x), `TrackDetailDataLoader.cs`, `PlayerCommandService.cs`, `PlaylistMenuService.cs`, `PlaylistExportService.cs`, `PlaylistImportService.cs`, `PlaylistDataLoader.cs`, `AlbumDataLoader.cs`, `ArtistDataLoader.cs`, `GenreDataLoader.cs`

Fichiers Import :
- `PostImportApiEnrichmentTask.cs`

Tests Presentation :
- `ArtistsDataLoaderTests.cs`, `AlbumDataLoaderTests.cs`, `ArtistDataLoaderTests.cs`, `GenreDataLoaderTests.cs`, `PlaylistDataLoaderTests.cs`, `PlaylistExportServiceTests.cs`, `PlaylistImportServiceTests.cs`, `PlaylistUpdateServiceTests.cs`, `PlaylistsDataLoaderTests.cs`, `PlaylistMenuServiceTests.cs`, `TrackDetailDataLoaderTests.cs`

Tests Import :
- `PostImportApiEnrichmentTaskTests.cs`

**Commit** : `refactor(presentation): adapt consumers to CleanArch.DevKit.Mediator.Results API`

## T10. Audit grep + build + tests

```bash
grep -r "MiF.Result" src/ tests/ --include="*.cs" --include="*.csproj"  # expect 0
grep -rn "\.IsError\b" src/ tests/ --include="*.cs"                       # expect 0
grep -rn "Result\.\(Success\|Fail\)" src/ tests/ --include="*.cs"         # check all migrated
dotnet build Rok.slnx /p:Platform=x64                                     # expect 0 errors/warnings
dotnet test Rok.slnx /p:Platform=x64 --no-build                           # expect all green
```

Si échec : fixer la cause racine, **nouveau** commit (never amend).

## T11. Smoke test WinUI manuel (utilisateur)

- Lancement app, démarrage sans crash
- Navigation Albums, Artists, Playlists
- Créer une playlist (Result success + persistence)
- Provoquer un échec connu (track introuvable dans une playlist par ex.) et vérifier qu'il remonte avec `NotFoundError` + bon code

## T12. Squash + PR (à valider avec l'utilisateur après smoke test)

`git reset --soft refactor/migrate-to-cleanarch-mediator` puis un seul commit `refactor(app): migrate from MiF.Result to CleanArch.DevKit.Mediator.Results`.
