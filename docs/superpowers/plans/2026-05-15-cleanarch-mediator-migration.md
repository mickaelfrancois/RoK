# Migration CleanArch.DevKit.Mediator — Plan d'implémentation

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Migrer Rok de `MiF.Mediator` 1.2.0 vers `CleanArch.DevKit.Mediator` 1.0.0 + `CleanArch.DevKit.Mediator.Validation` 1.0.0 (étape 1/3). Renommer les suffixes Command/Query → Request, déplacer les dossiers vers `Requests/`, migrer la validation DataAnnotations vers des validators fluides, convertir `QueryPreProcessor` (IRequestPreProcessor) en `IPipelineBehavior`, supprimer l'attribut custom `RequiredGreaterThanZero` devenu orphelin.

**Architecture:** `Rok.Application` reste la couche cible — c'est la seule où vivent les handlers (contrainte du source generator CleanArch). Une seule `public partial class Mediator { }` y est déclarée. Les validators sont colocalisés dans les fichiers `*RequestHandler.cs` (3 types par fichier : Request + Handler + Validator). Le `LoggingPipelineBehavior` est isolé dans un nouveau dossier `Pipeline/`. La validation utilise `AddValidators()` + `AddValidationBehavior()` (générateur Roslyn pour la découverte zéro-réflexion).

**Tech Stack:** .NET 10 / C# 13, xUnit + Moq, **CleanArch.DevKit.Mediator** 1.0.0, **CleanArch.DevKit.Mediator.Validation** 1.0.0, suppression de `MiF.Mediator` 1.2.0.

**Spec lié :** `docs/superpowers/specs/2026-05-15-cleanarch-mediator-migration-design.md` (lecture obligatoire avant exécution).

**Branche :** `refactor/migrate-to-cleanarch-mediator` (déjà créée, commit du spec `0d148f9`).

---

## File Map

### Créés

| Fichier | Rôle |
|---|---|
| `src/Rok.Application/Mediator.cs` | `public partial class Mediator { }` — déclencheur du source generator |
| `src/Rok.Application/Pipeline/LoggingPipelineBehavior.cs` | Behavior de logging générique (remplace `QueryPreProcessor`) |
| `tests/UnitTests/Rok.ApplicationTests/Pipeline/LoggingPipelineBehaviorTests.cs` | Couverture du behavior |
| `tests/UnitTests/Rok.ApplicationTests/Features/<Area>/Validators/<Name>RequestValidatorTests.cs` | 34 fichiers de tests négatifs des validators (un par validator créé) |

### Renommés (git mv obligatoire)

- 67 fichiers `src/Rok.Application/Features/<Area>/{Command,Query}/<Name>{Command,Query}Handler.cs` → `src/Rok.Application/Features/<Area>/Requests/<Name>RequestHandler.cs`
- ~30 fichiers tests `tests/UnitTests/Rok.ApplicationTests/Features/<Area>/{Command,Query}/...` → équivalent dans `Requests/`
- `src/Rok.Application/Features/QueryPreProcessor.cs` → `src/Rok.Application/Pipeline/LoggingPipelineBehavior.cs`

### Modifiés

| Fichier | Changement |
|---|---|
| `src/Rok.Application/Rok.Application.csproj` | -MiF.Mediator +CleanArch.DevKit.Mediator +CleanArch.DevKit.Mediator.Validation |
| `src/Rok.Application/DependencyInjection.cs` | AddSimpleMediator → AddMediator + AddValidators + AddValidationBehavior ; retirer AddScoped IValidationService |
| `src/Rok.Application/GlobalUsings.cs` | Adapter les usings (retirer DataAnnotations + ValidationAttributes ; ajouter CleanArch usings) |
| `src/Presentation/GlobalUsings.cs` | `MiF.Mediator.Interfaces` → `CleanArch.DevKit.Mediator` |
| `src/Presentation/ViewModels/Playlist/Services/PlaylistExportService.cs` | using `MiF.Mediator.Interfaces` → `CleanArch.DevKit.Mediator` |
| `src/Rok.Import/Services/PostImportApiEnrichmentTask.cs` | using `MiF.Mediator.Interfaces` → `CleanArch.DevKit.Mediator` |
| `tests/UnitTests/Rok.ApplicationTests/GlobalUsings.cs` | Adapter les usings |
| 67 handlers renommés | Suffixes Command/Query → Request, ICommand/IQuery → IRequest, HandleAsync → Handle, ajout du validator inline |
| ~40 fichiers tests | Mocks `SendMessageAsync<T>` → `Send<T>`, renommages des types Command/Query → Request |

### Supprimés

| Fichier | Motif |
|---|---|
| `src/Rok.Shared/ValidationAttributes/RequiredGreaterThanZero.cs` | Orphelin après migration |
| `src/Rok.Shared/ValidationAttributes/` (dossier) | Devient vide |
| `tests/UnitTests/Rok.ApplicationTests/Shared/ValidationAttributes/RequiredGreaterThanZeroTests.cs` | Test du code supprimé |
| `src/Rok.Application/Features/Command/` (dossiers, par feature) | Tous remplacés par `Requests/` |
| `src/Rok.Application/Features/Query/` (dossiers, par feature) | Tous remplacés par `Requests/` |

---

## Pré-requis externes

Le repo `D:/Development/CleanArch.DevKit/` doit avoir publié ses paquets sur nuget.org avant le démarrage. Voir **Task 1**.

---

## Task 1 — Publier CleanArch.DevKit.Mediator + Validation sur nuget.org

**Files (hors Rok) :**
- Lecture seule : `D:/Development/CleanArch.DevKit/scripts/publish.ps1`

Tâche externe au repo Rok — exécutée dans le repo `D:/Development/CleanArch.DevKit/`.

- [ ] **Step 1: Vérifier la propreté du worktree CleanArch.DevKit**

```bash
cd D:/Development/CleanArch.DevKit
git status
```

Expected : working tree clean. Sinon committer/stasher avant la suite.

- [ ] **Step 2: Vérifier que `NUGET_API_KEY` est défini**

```bash
[ -n "$NUGET_API_KEY" ] && echo "API key set" || echo "MISSING — set it before continuing"
```

Expected : `API key set`. Sinon, exporter la clé d'API nuget.org.

- [ ] **Step 3: Lancer un dry-run pour valider le packaging**

```bash
cd D:/Development/CleanArch.DevKit && pwsh ./scripts/publish.ps1 -Version 0.1.0 -DryRun
```

Expected : build vert, tests verts, 8 paquets `.nupkg` produits dans `./artifacts/`, message "Dry run — skipping push".

- [ ] **Step 4: Publier réellement (uniquement les deux paquets nécessaires)**

Si `publish.ps1` publie tous les paquets, c'est OK — `--skip-duplicate` ignore ce qui existe déjà. Sinon adapter pour ne pousser que les deux requis. Pour cette migration, on a besoin de :
- `CleanArch.DevKit.Mediator` 0.1.0
- `CleanArch.DevKit.Mediator.Validation` 0.1.0

```bash
cd D:/Development/CleanArch.DevKit && pwsh ./scripts/publish.ps1 -Version 0.1.0
```

Expected : prompt `Push N package(s) to https://api.nuget.org/v3/index.json ? (y/N)` — répondre `y`. Sortie "Pushing to nuget.org" puis "Done.".

- [ ] **Step 5: Vérifier la disponibilité sur nuget.org (peut prendre 1-5 min d'indexation)**

```bash
curl -sf "https://api.nuget.org/v3-flatcontainer/cleanarch.devkit.mediator/index.json" | head -c 200
curl -sf "https://api.nuget.org/v3-flatcontainer/cleanarch.devkit.mediator.validation/index.json" | head -c 200
```

Expected : JSON contenant `"0.1.0"`. Réessayer après 1 min si absent.

Pas de commit pour cette task (changement externe au repo Rok).

---

## Task 2 — Ajouter les paquets NuGet à Rok.Application

**Files:**
- Modify : `src/Rok.Application/Rok.Application.csproj`

- [ ] **Step 1: Modifier le `<ItemGroup>` des PackageReference**

Remplacer le contenu actuel des références :
```xml
    <PackageReference Include="Dapper" Version="2.1.72" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.5" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.5" />
    <PackageReference Include="MiF.Guard" Version="1.0.0" />
    <PackageReference Include="MiF.Mediator" Version="1.2.0" />
    <PackageReference Include="MiF.Messenger" Version="1.0.0" />
    <PackageReference Include="MiF.Result" Version="1.1.0" />
```

Par :
```xml
    <PackageReference Include="CleanArch.DevKit.Mediator" Version="1.0.0" />
    <PackageReference Include="CleanArch.DevKit.Mediator.Validation" Version="1.0.0" />
    <PackageReference Include="Dapper" Version="2.1.72" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.5" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.5" />
    <PackageReference Include="MiF.Guard" Version="1.0.0" />
    <PackageReference Include="MiF.Messenger" Version="1.0.0" />
    <PackageReference Include="MiF.Result" Version="1.1.0" />
```

- [ ] **Step 2: Restaurer pour confirmer la résolution NuGet**

```bash
cd D:/Development/MF/Rok && dotnet restore src/Rok.Application/Rok.Application.csproj /p:Platform=x64
```

Expected : `Restore complete`. Aucune erreur `Unable to find package`.

- [ ] **Step 3: Build → s'attendre à beaucoup d'erreurs de compile (référence cassée à MiF.Mediator.Interfaces)**

```bash
cd D:/Development/MF/Rok && dotnet build /p:Platform=x64 2>&1 | head -50
```

Expected : nombreuses erreurs `CS0246 The type or namespace name 'IMediator'/'ICommandHandler'/...` etc. C'est attendu — on les corrigera dans les tâches suivantes.

- [ ] **Step 4: Commit le swap NuGet**

```bash
cd D:/Development/MF/Rok && git add src/Rok.Application/Rok.Application.csproj
git commit -m "refactor(app): swap MiF.Mediator package for CleanArch.DevKit.Mediator + Validation"
```

Note : le build est rouge à ce point — c'est attendu, les tâches suivantes le remettent au vert.

---

## Task 3 — Créer la classe partielle `Mediator`

**Files:**
- Create : `src/Rok.Application/Mediator.cs`

- [ ] **Step 1: Créer le fichier**

```csharp
// src/Rok.Application/Mediator.cs
namespace Rok.Application;

public partial class Mediator { }
```

- [ ] **Step 2: Vérifier qu'aucun autre `partial class Mediator` n'existe ailleurs**

```bash
cd D:/Development/MF/Rok && grep -rn "partial class Mediator" src/ tests/ --include="*.cs" | grep -v "/obj/"
```

Expected : une seule ligne pointant vers `src/Rok.Application/Mediator.cs`.

- [ ] **Step 3: Commit**

```bash
cd D:/Development/MF/Rok && git add src/Rok.Application/Mediator.cs
git commit -m "refactor(app): add Mediator partial class for CleanArch source generator"
```

---

## Task 4 — Migrer `QueryPreProcessor` → `LoggingPipelineBehavior`

**Files:**
- Move : `src/Rok.Application/Features/QueryPreProcessor.cs` → `src/Rok.Application/Pipeline/LoggingPipelineBehavior.cs`
- Create : `tests/UnitTests/Rok.ApplicationTests/Pipeline/LoggingPipelineBehaviorTests.cs`

- [ ] **Step 1: Créer le dossier `Pipeline/` et écrire le test négatif d'abord**

```bash
cd D:/Development/MF/Rok && mkdir -p src/Rok.Application/Pipeline tests/UnitTests/Rok.ApplicationTests/Pipeline
```

```csharp
// tests/UnitTests/Rok.ApplicationTests/Pipeline/LoggingPipelineBehaviorTests.cs
using CleanArch.DevKit.Mediator;
using Microsoft.Extensions.Logging;
using Moq;
using Rok.Application.Pipeline;

namespace Rok.ApplicationTests.Pipeline;

public class LoggingPipelineBehaviorTests
{
    private sealed record FakeRequest(int Value) : IRequest<int>;

    [Fact(DisplayName = "logs_request_name_and_returns_next_result")]
    public async Task logs_request_name_and_returns_next_result()
    {
        Mock<ILogger<LoggingPipelineBehavior<FakeRequest, int>>> loggerMock = new();
        LoggingPipelineBehavior<FakeRequest, int> sut = new(loggerMock.Object);
        FakeRequest request = new(42);
        RequestHandlerDelegate<int> next = _ => Task.FromResult(99);

        int result = await sut.Handle(request, next, CancellationToken.None);

        Assert.Equal(99, result);
        loggerMock.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("FakeRequest")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact(DisplayName = "rethrows_and_logs_when_next_throws")]
    public async Task rethrows_and_logs_when_next_throws()
    {
        Mock<ILogger<LoggingPipelineBehavior<FakeRequest, int>>> loggerMock = new();
        LoggingPipelineBehavior<FakeRequest, int> sut = new(loggerMock.Object);
        FakeRequest request = new(42);
        InvalidOperationException expected = new("boom");
        RequestHandlerDelegate<int> next = _ => throw expected;

        InvalidOperationException actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.Handle(request, next, CancellationToken.None));

        Assert.Same(expected, actual);
        loggerMock.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                expected,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
```

- [ ] **Step 2: Run test → fail (type pas encore défini)**

```bash
cd D:/Development/MF/Rok && dotnet test tests/UnitTests/Rok.ApplicationTests/Rok.ApplicationTests.csproj /p:Platform=x64 --filter "DisplayName~logs_request_name_and_returns_next_result" 2>&1 | tail -20
```

Expected : compile error `LoggingPipelineBehavior` not found.

- [ ] **Step 3: Créer le behavior et supprimer l'ancien fichier**

```bash
cd D:/Development/MF/Rok && git mv src/Rok.Application/Features/QueryPreProcessor.cs src/Rok.Application/Pipeline/LoggingPipelineBehavior.cs
```

Puis remplacer le contenu du fichier déplacé par :

```csharp
// src/Rok.Application/Pipeline/LoggingPipelineBehavior.cs
using CleanArch.DevKit.Mediator;
using Microsoft.Extensions.Logging;

namespace Rok.Application.Pipeline;

public sealed class LoggingPipelineBehavior<TRequest, TResponse>(
    ILogger<LoggingPipelineBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Processing message: {Message}", typeof(TRequest).Name);

        try
        {
            return await next(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Processing message error: {Message}", typeof(TRequest).Name);
            throw;
        }
    }
}
```

- [ ] **Step 4: Run test → toujours red (DependencyInjection.cs casse car AddSimpleMediator pas remplacé)**

C'est attendu — la compile cassera plus loin (chaîne d'erreurs). On laisse pour Task 5.

- [ ] **Step 5: Commit**

```bash
cd D:/Development/MF/Rok && git add -A && git status --short
git commit -m "refactor(app): convert QueryPreProcessor to LoggingPipelineBehavior (IPipelineBehavior)"
```

---

## Task 5 — Mettre à jour `DependencyInjection.cs`

**Files:**
- Modify : `src/Rok.Application/DependencyInjection.cs`

- [ ] **Step 1: Remplacer le contenu complet du fichier**

```csharp
// src/Rok.Application/DependencyInjection.cs
using CleanArch.DevKit.Mediator;
using CleanArch.DevKit.Mediator.Validation;
using Microsoft.Extensions.DependencyInjection;
using Rok.Application.Features.Albums.Services;
using Rok.Application.Features.Artists.Services;
using Rok.Application.Features.EqualizerPresets;
using Rok.Application.Features.Playlists;
using Rok.Application.Features.Tracks.Services;
using Rok.Application.Interfaces;
using Rok.Application.Options;
using Rok.Application.Pipeline;
using Rok.Application.Player;
using Rok.Application.Services;

namespace Rok.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IAppOptions, AppOptions>();
        services.AddSingleton<IPlaylistService, PlaylistService>();
        services.AddSingleton<IPlayerService, PlayerService>();
        services.AddSingleton<IReviewPromptEligibilityService, ReviewPromptEligibilityService>();
        services.AddSingleton<IEqualizerPresetResolver, EqualizerPresetResolver>();

        services.AddSingleton<IPlayerSleepModeService, PlayerSleepModeService>();

        services.AddTransient<TrackLyricsService>();
        services.AddTransient<IArtistApiService, ArtistApiService>();
        services.AddTransient<IAlbumApiService, AlbumApiService>();

        services.AddMediator();
        services.AddValidators();
        services.AddValidationBehavior();

        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingPipelineBehavior<,>));

        return services;
    }
}
```

Notes :
- `IValidationService` retiré — remplacé par `AddValidators()` + `AddValidationBehavior()`
- `AddMediator()` est généré par le source generator (résolu globalement, sans namespace requis sur le `using`)
- `AddValidators()` est aussi généré par le source generator de `Mediator.Validation`
- L'ordre d'enregistrement du `LoggingPipelineBehavior` après `AddValidationBehavior` le place **en interne** (plus proche du handler) — la validation s'exécute donc avant le logging. C'est inverse au comportement MiF (logging d'abord). Si on veut conserver l'ordre MiF, intervertir les deux `services.AddTransient`.

Décision : on garde validation avant logging — un log qui n'enregistre rien sur une requête refusée est cohérent.

- [ ] **Step 2: Build pour vérifier que la couche Application restante a au moins les bons usings (compile partiel)**

```bash
cd D:/Development/MF/Rok && dotnet build src/Rok.Application/Rok.Application.csproj /p:Platform=x64 2>&1 | tail -30
```

Expected : énormément d'erreurs résiduelles sur les handlers eux-mêmes (`ICommand` not found etc.) — c'est attendu. Mais aucune erreur sur `DependencyInjection.cs` lui-même.

- [ ] **Step 3: Commit**

```bash
cd D:/Development/MF/Rok && git add src/Rok.Application/DependencyInjection.cs
git commit -m "refactor(app): wire DI to CleanArch.DevKit AddMediator + AddValidators + AddValidationBehavior"
```

---

## Task 6 — Mettre à jour `GlobalUsings.cs` (Application + Présentation + Tests)

**Files:**
- Modify : `src/Rok.Application/GlobalUsings.cs`
- Modify : `src/Presentation/GlobalUsings.cs`
- Modify : `tests/UnitTests/Rok.ApplicationTests/GlobalUsings.cs`

- [ ] **Step 1: Mettre à jour `src/Rok.Application/GlobalUsings.cs`**

Remplacer le contenu par :
```csharp
global using CleanArch.DevKit.Mediator;
global using CleanArch.DevKit.Mediator.Validation;
global using MiF.Result;
global using Rok.Application.Dto;
global using Rok.Application.Mapping;
global using Rok.Domain.Entities;
```

Suppressions : `System.ComponentModel.DataAnnotations`, `MiF.Mediator.Interfaces`, `Rok.Shared.ValidationAttributes`.
Ajouts : `CleanArch.DevKit.Mediator`, `CleanArch.DevKit.Mediator.Validation`.

- [ ] **Step 2: Mettre à jour `src/Presentation/GlobalUsings.cs`**

Remplacer la ligne :
```csharp
global using MiF.Mediator.Interfaces;
```
par :
```csharp
global using CleanArch.DevKit.Mediator;
```

Toutes les autres lignes restent intactes (MiF.SimpleMessenger, MiF.Result, etc. — hors scope étape 1).

- [ ] **Step 3: Mettre à jour `tests/UnitTests/Rok.ApplicationTests/GlobalUsings.cs`**

Lire d'abord le fichier pour voir son contenu actuel :
```bash
cd D:/Development/MF/Rok && cat tests/UnitTests/Rok.ApplicationTests/GlobalUsings.cs
```

Remplacer toute ligne `global using MiF.Mediator;` ou `global using MiF.Mediator.Interfaces;` par :
```csharp
global using CleanArch.DevKit.Mediator;
global using CleanArch.DevKit.Mediator.Validation;
```

- [ ] **Step 4: Commit**

```bash
cd D:/Development/MF/Rok && git add src/Rok.Application/GlobalUsings.cs src/Presentation/GlobalUsings.cs tests/UnitTests/Rok.ApplicationTests/GlobalUsings.cs
git commit -m "refactor: switch GlobalUsings to CleanArch.DevKit namespaces"
```

---

## Task 7 — Migrer la feature **Tracks** (template détaillé)

Tracks contient 14 handlers dont 5 ont des DataAnnotations. C'est le walkthrough de référence — toutes les tâches feature suivantes appliquent le même schéma.

**Files (`git mv` obligatoire pour préserver l'historique) :**

Sources à déplacer :
- `src/Rok.Application/Features/Tracks/Command/ResetTrackListenCountCommandHandler.cs` → `src/Rok.Application/Features/Tracks/Requests/ResetTrackListenCountRequestHandler.cs`
- `src/Rok.Application/Features/Tracks/Command/UpdateScoreCommandHandler.cs` → `Requests/UpdateScoreRequestHandler.cs`
- `src/Rok.Application/Features/Tracks/Command/UpdateSkipCountCommandHandler.cs` → `Requests/UpdateSkipCountRequestHandler.cs`
- `src/Rok.Application/Features/Tracks/Command/UpdateTrackGetLyricsLastAttemptCommandHandler.cs` → `Requests/UpdateTrackGetLyricsLastAttemptRequestHandler.cs`
- `src/Rok.Application/Features/Tracks/Command/UpdateTrackLastListenCommandHandler.cs` → `Requests/UpdateTrackLastListenRequestHandler.cs`
- `src/Rok.Application/Features/Tracks/Query/GetAllTracksQueryHandler.cs` → `Requests/GetAllTracksRequestHandler.cs`
- `src/Rok.Application/Features/Tracks/Query/GetTrackByIdQueryHandler.cs` → `Requests/GetTrackByIdRequestHandler.cs`
- `src/Rok.Application/Features/Tracks/Query/GetTracksByAlbumIdQueryHandler.cs` → `Requests/GetTracksByAlbumIdRequestHandler.cs`
- `src/Rok.Application/Features/Tracks/Query/GetTracksByAlbumListQueryHandler.cs` → `Requests/GetTracksByAlbumListRequestHandler.cs`
- `src/Rok.Application/Features/Tracks/Query/GetTracksByArtistIdQueryHandler.cs` → `Requests/GetTracksByArtistIdRequestHandler.cs`
- `src/Rok.Application/Features/Tracks/Query/GetTracksByArtistListQueryHandler.cs` → `Requests/GetTracksByArtistListRequestHandler.cs`
- `src/Rok.Application/Features/Tracks/Query/GetTracksByGenreIdQueryHandler.cs` → `Requests/GetTracksByGenreIdRequestHandler.cs`
- `src/Rok.Application/Features/Tracks/Query/GetTracksByPlaylistIdQueryHandler.cs` → `Requests/GetTracksByPlaylistIdRequestHandler.cs`
- `src/Rok.Application/Features/Tracks/Query/GetTracksCountQueryHandler.cs` → `Requests/GetTracksCountRequestHandler.cs`

Tests correspondants : déplacer aussi `tests/UnitTests/Rok.ApplicationTests/Features/Tracks/Command/*.cs` et `Query/*.cs` vers `tests/UnitTests/Rok.ApplicationTests/Features/Tracks/Requests/*.cs` quand ils existent (vérifier ce qui existe avec `find` avant).

Validators à écrire (5) : pour `GetTrackByIdRequest`, `GetTracksByAlbumIdRequest`, `GetTracksByArtistIdRequest`, `GetTracksByGenreIdRequest`, `GetTracksByPlaylistIdRequest`.

- [ ] **Step 1: Créer le dossier `Requests/`**

```bash
cd D:/Development/MF/Rok && mkdir -p src/Rok.Application/Features/Tracks/Requests tests/UnitTests/Rok.ApplicationTests/Features/Tracks/Requests tests/UnitTests/Rok.ApplicationTests/Features/Tracks/Validators
```

- [ ] **Step 2: Renommer tous les fichiers handlers via `git mv`**

```bash
cd D:/Development/MF/Rok
git mv src/Rok.Application/Features/Tracks/Command/ResetTrackListenCountCommandHandler.cs src/Rok.Application/Features/Tracks/Requests/ResetTrackListenCountRequestHandler.cs
git mv src/Rok.Application/Features/Tracks/Command/UpdateScoreCommandHandler.cs src/Rok.Application/Features/Tracks/Requests/UpdateScoreRequestHandler.cs
git mv src/Rok.Application/Features/Tracks/Command/UpdateSkipCountCommandHandler.cs src/Rok.Application/Features/Tracks/Requests/UpdateSkipCountRequestHandler.cs
git mv src/Rok.Application/Features/Tracks/Command/UpdateTrackGetLyricsLastAttemptCommandHandler.cs src/Rok.Application/Features/Tracks/Requests/UpdateTrackGetLyricsLastAttemptRequestHandler.cs
git mv src/Rok.Application/Features/Tracks/Command/UpdateTrackLastListenCommandHandler.cs src/Rok.Application/Features/Tracks/Requests/UpdateTrackLastListenRequestHandler.cs
git mv src/Rok.Application/Features/Tracks/Query/GetAllTracksQueryHandler.cs src/Rok.Application/Features/Tracks/Requests/GetAllTracksRequestHandler.cs
git mv src/Rok.Application/Features/Tracks/Query/GetTrackByIdQueryHandler.cs src/Rok.Application/Features/Tracks/Requests/GetTrackByIdRequestHandler.cs
git mv src/Rok.Application/Features/Tracks/Query/GetTracksByAlbumIdQueryHandler.cs src/Rok.Application/Features/Tracks/Requests/GetTracksByAlbumIdRequestHandler.cs
git mv src/Rok.Application/Features/Tracks/Query/GetTracksByAlbumListQueryHandler.cs src/Rok.Application/Features/Tracks/Requests/GetTracksByAlbumListRequestHandler.cs
git mv src/Rok.Application/Features/Tracks/Query/GetTracksByArtistIdQueryHandler.cs src/Rok.Application/Features/Tracks/Requests/GetTracksByArtistIdRequestHandler.cs
git mv src/Rok.Application/Features/Tracks/Query/GetTracksByArtistListQueryHandler.cs src/Rok.Application/Features/Tracks/Requests/GetTracksByArtistListRequestHandler.cs
git mv src/Rok.Application/Features/Tracks/Query/GetTracksByGenreIdQueryHandler.cs src/Rok.Application/Features/Tracks/Requests/GetTracksByGenreIdRequestHandler.cs
git mv src/Rok.Application/Features/Tracks/Query/GetTracksByPlaylistIdQueryHandler.cs src/Rok.Application/Features/Tracks/Requests/GetTracksByPlaylistIdRequestHandler.cs
git mv src/Rok.Application/Features/Tracks/Query/GetTracksCountQueryHandler.cs src/Rok.Application/Features/Tracks/Requests/GetTracksCountRequestHandler.cs
```

Vérifier que `git status` montre des renommages :
```bash
git status --short
```

Expected : lignes commençant par `R` (renamed).

- [ ] **Step 3: Supprimer les dossiers `Command/` et `Query/` devenus vides**

```bash
cd D:/Development/MF/Rok && rmdir src/Rok.Application/Features/Tracks/Command src/Rok.Application/Features/Tracks/Query
```

- [ ] **Step 4: Adapter le contenu de chaque fichier — pattern de transformation**

Pour chaque fichier déplacé, appliquer ces remplacements à l'intérieur :

| Avant | Après |
|---|---|
| `using MiF.Mediator;` / `using MiF.Mediator.Interfaces;` | (à retirer — usings globaux dans GlobalUsings.cs) |
| `class <Name>Command` / `class <Name>Query` | `class <Name>Request` |
| `: ICommand<...>` / `: IQuery<...>` | `: IRequest<...>` |
| `class <Name>CommandHandler` / `class <Name>QueryHandler` | `class <Name>RequestHandler` |
| `: ICommandHandler<<Name>Command, ...>` / `: IQueryHandler<<Name>Query, ...>` | `: IRequestHandler<<Name>Request, ...>` |
| `Task<...> HandleAsync(<Name>Command/Query` | `Task<...> Handle(<Name>Request` |
| `return Unit.Result;` | `return Unit.Value;` |
| `[Required]` / `[RequiredGreaterThanZero]` | (à supprimer) |

Pour les 5 fichiers nécessitant un validator, ajouter une classe validator entre le Request et le Handler (cf. Step 5).

**Exemple complet — `GetTrackByIdRequestHandler.cs`** (avait `[RequiredGreaterThanZero] long Id`) :

```csharp
// src/Rok.Application/Features/Tracks/Requests/GetTrackByIdRequestHandler.cs
using Rok.Application.Interfaces.Repositories;

namespace Rok.Application.Features.Tracks.Requests;

public class GetTrackByIdRequest(long id) : IRequest<Result<TrackDto>>
{
    public long Id { get; } = id;
}

public sealed class GetTrackByIdRequestValidator : Validator<GetTrackByIdRequest>
{
    public GetTrackByIdRequestValidator()
    {
        Rule(x => x.Id).GreaterThan(0L);
    }
}

public class GetTrackByIdRequestHandler(ITrackRepository trackRepository) : IRequestHandler<GetTrackByIdRequest, Result<TrackDto>>
{
    public async Task<Result<TrackDto>> Handle(GetTrackByIdRequest query, CancellationToken cancellationToken)
    {
        TrackEntity? track = await trackRepository.GetByIdAsync(query.Id);
        if (track == null)
            return Result<TrackDto>.Fail("NotFound", "Track not found");
        else
            return Result<TrackDto>.Success(TrackDtoMapping.Map(track));
    }
}
```

**Exemple sans DataAnnotations — `ResetTrackListenCountRequestHandler.cs`** (renommage pur) :

Ouvrir le fichier, conserver le using `Rok.Application.Interfaces.Repositories`, retirer le using MiF si présent, renommer la classe `ResetTrackListenCountCommand` → `ResetTrackListenCountRequest`, `: ICommand<Unit>` → `: IRequest<Unit>` (ou `: IRequest` qui est équivalent), `: ICommandHandler<ResetTrackListenCountCommand, Unit>` → `: IRequestHandler<ResetTrackListenCountRequest, Unit>`, `HandleAsync` → `Handle`, `return Unit.Result;` → `return Unit.Value;`. Pas de validator.

**Pour chacun des 14 fichiers**, appliquer cette adaptation. Spécifiquement pour les 5 fichiers avec DataAnnotations, voici les validators :

```csharp
// Dans GetTrackByIdRequestHandler.cs (voir Step 4 exemple)
public sealed class GetTrackByIdRequestValidator : Validator<GetTrackByIdRequest>
{
    public GetTrackByIdRequestValidator()
    {
        Rule(x => x.Id).GreaterThan(0L);
    }
}

// Dans GetTracksByAlbumIdRequestHandler.cs
public sealed class GetTracksByAlbumIdRequestValidator : Validator<GetTracksByAlbumIdRequest>
{
    public GetTracksByAlbumIdRequestValidator()
    {
        Rule(x => x.AlbumId).GreaterThan(0L);
    }
}

// Dans GetTracksByArtistIdRequestHandler.cs
public sealed class GetTracksByArtistIdRequestValidator : Validator<GetTracksByArtistIdRequest>
{
    public GetTracksByArtistIdRequestValidator()
    {
        Rule(x => x.ArtistId).GreaterThan(0L);
    }
}

// Dans GetTracksByGenreIdRequestHandler.cs
public sealed class GetTracksByGenreIdRequestValidator : Validator<GetTracksByGenreIdRequest>
{
    public GetTracksByGenreIdRequestValidator()
    {
        Rule(x => x.GenreId).GreaterThan(0L);
    }
}

// Dans GetTracksByPlaylistIdRequestHandler.cs
public sealed class GetTracksByPlaylistIdRequestValidator : Validator<GetTracksByPlaylistIdRequest>
{
    public GetTracksByPlaylistIdRequestValidator()
    {
        Rule(x => x.PlaylistId).GreaterThan(0L);
    }
}
```

Important : avant d'écrire le validator, ouvrir l'ancien fichier pour récupérer le nom exact de la propriété (`Id`, `AlbumId`, etc.). Toutes les propriétés ici sont `long`, donc `GreaterThan(0L)`.

- [ ] **Step 5: Écrire les tests négatifs pour les 5 validators**

Créer dans `tests/UnitTests/Rok.ApplicationTests/Features/Tracks/Validators/` :

```csharp
// tests/UnitTests/Rok.ApplicationTests/Features/Tracks/Validators/GetTrackByIdRequestValidatorTests.cs
using CleanArch.DevKit.Mediator.Validation;
using Rok.Application.Features.Tracks.Requests;

namespace Rok.ApplicationTests.Features.Tracks.Validators;

public class GetTrackByIdRequestValidatorTests
{
    [Fact(DisplayName = "fails_when_id_is_zero")]
    public async Task fails_when_id_is_zero()
    {
        GetTrackByIdRequestValidator sut = new();
        GetTrackByIdRequest request = new(0);

        ValidationResult result = await sut.ValidateAsync(request, CancellationToken.None);

        Assert.False(result.IsValid);
    }

    [Fact(DisplayName = "succeeds_when_id_is_positive")]
    public async Task succeeds_when_id_is_positive()
    {
        GetTrackByIdRequestValidator sut = new();
        GetTrackByIdRequest request = new(42);

        ValidationResult result = await sut.ValidateAsync(request, CancellationToken.None);

        Assert.True(result.IsValid);
    }
}
```

Répliquer le même schéma pour `GetTracksByAlbumIdRequestValidatorTests`, `GetTracksByArtistIdRequestValidatorTests`, `GetTracksByGenreIdRequestValidatorTests`, `GetTracksByPlaylistIdRequestValidatorTests` — adapter le nom du request, le constructor positionnel et la propriété testée. Chaque test passe 0 dans le `fails_when_*` et un entier positif dans le `succeeds_when_*`.

- [ ] **Step 6: Build pour vérifier que la feature Tracks compile (l'erreur sera maintenant sur les autres features)**

```bash
cd D:/Development/MF/Rok && dotnet build /p:Platform=x64 2>&1 | grep -E "error.*Tracks" | head -10
```

Expected : 0 erreur dans `Features/Tracks/`. Les erreurs résiduelles concernent les autres features (Albums, Artists, etc.) — c'est attendu.

- [ ] **Step 7: Commit**

```bash
cd D:/Development/MF/Rok && git add -A && git status --short
git commit -m "refactor(app): migrate Tracks feature to CleanArch IRequest + validators"
```

---

## Task 8 — Migrer la feature **Albums**

Albums : 12 handlers (8 commands + 4 queries), 9 validators à écrire.

**Files à `git mv`** :

```
Command/ResetAlbumListenCountCommandHandler.cs → Requests/ResetAlbumListenCountRequestHandler.cs
Command/UpdateAlbumCommandHandler.cs → Requests/UpdateAlbumRequestHandler.cs                     [validator]
Command/UpdateAlbumFavoriteCommandHandler.cs → Requests/UpdateAlbumFavoriteRequestHandler.cs       [validator]
Command/UpdateAlbumGetMetaDataLastAttemptCommandHandler.cs → Requests/UpdateAlbumGetMetaDataLastAttemptRequestHandler.cs
Command/UpdateAlbumLastListenCommandHandler.cs → Requests/UpdateAlbumLastListenRequestHandler.cs   [validator]
Command/UpdateAlbumPictureDominantColorCommandHandler.cs → Requests/UpdateAlbumPictureDominantColorRequestHandler.cs   [validator]
Command/UpdateAlbumStatisticsCommandHandler.cs → Requests/UpdateAlbumStatisticsRequestHandler.cs   [validator]
Command/UpdateAlbumTagsCommandHandler.cs → Requests/UpdateAlbumTagsRequestHandler.cs               [validator]
Query/GetAlbumByIdQueryHandler.cs → Requests/GetAlbumByIdRequestHandler.cs                         [validator]
Query/GetAlbumsByArtistIdQueryHandler.cs → Requests/GetAlbumsByArtistIdRequestHandler.cs           [validator]
Query/GetAlbumsByGenreIdQueryHandler.cs → Requests/GetAlbumsByGenreIdRequestHandler.cs             [validator]
Query/GetAllAlbumsQueryHandler.cs → Requests/GetAllAlbumsRequestHandler.cs
```

- [ ] **Step 1: Créer les dossiers**

```bash
cd D:/Development/MF/Rok && mkdir -p src/Rok.Application/Features/Albums/Requests tests/UnitTests/Rok.ApplicationTests/Features/Albums/Requests tests/UnitTests/Rok.ApplicationTests/Features/Albums/Validators
```

- [ ] **Step 2: `git mv` tous les fichiers**

```bash
cd D:/Development/MF/Rok
git mv src/Rok.Application/Features/Albums/Command/ResetAlbumListenCountCommandHandler.cs src/Rok.Application/Features/Albums/Requests/ResetAlbumListenCountRequestHandler.cs
git mv src/Rok.Application/Features/Albums/Command/UpdateAlbumCommandHandler.cs src/Rok.Application/Features/Albums/Requests/UpdateAlbumRequestHandler.cs
git mv src/Rok.Application/Features/Albums/Command/UpdateAlbumFavoriteCommandHandler.cs src/Rok.Application/Features/Albums/Requests/UpdateAlbumFavoriteRequestHandler.cs
git mv src/Rok.Application/Features/Albums/Command/UpdateAlbumGetMetaDataLastAttemptCommandHandler.cs src/Rok.Application/Features/Albums/Requests/UpdateAlbumGetMetaDataLastAttemptRequestHandler.cs
git mv src/Rok.Application/Features/Albums/Command/UpdateAlbumLastListenCommandHandler.cs src/Rok.Application/Features/Albums/Requests/UpdateAlbumLastListenRequestHandler.cs
git mv src/Rok.Application/Features/Albums/Command/UpdateAlbumPictureDominantColorCommandHandler.cs src/Rok.Application/Features/Albums/Requests/UpdateAlbumPictureDominantColorRequestHandler.cs
git mv src/Rok.Application/Features/Albums/Command/UpdateAlbumStatisticsCommandHandler.cs src/Rok.Application/Features/Albums/Requests/UpdateAlbumStatisticsRequestHandler.cs
git mv src/Rok.Application/Features/Albums/Command/UpdateAlbumTagsCommandHandler.cs src/Rok.Application/Features/Albums/Requests/UpdateAlbumTagsRequestHandler.cs
git mv src/Rok.Application/Features/Albums/Query/GetAlbumByIdQueryHandler.cs src/Rok.Application/Features/Albums/Requests/GetAlbumByIdRequestHandler.cs
git mv src/Rok.Application/Features/Albums/Query/GetAlbumsByArtistIdQueryHandler.cs src/Rok.Application/Features/Albums/Requests/GetAlbumsByArtistIdRequestHandler.cs
git mv src/Rok.Application/Features/Albums/Query/GetAlbumsByGenreIdQueryHandler.cs src/Rok.Application/Features/Albums/Requests/GetAlbumsByGenreIdRequestHandler.cs
git mv src/Rok.Application/Features/Albums/Query/GetAllAlbumsQueryHandler.cs src/Rok.Application/Features/Albums/Requests/GetAllAlbumsRequestHandler.cs
rmdir src/Rok.Application/Features/Albums/Command src/Rok.Application/Features/Albums/Query
```

- [ ] **Step 3: Adapter chaque fichier** (mêmes transformations que Task 7 Step 4 — usings, suffixes Command/Query → Request, interface, HandleAsync → Handle, Unit.Result → Unit.Value, retirer DataAnnotations)

- [ ] **Step 4: Inclure les validators dans les 9 fichiers concernés**

Toutes les propriétés ci-dessous sont `long` → `GreaterThan(0L)`.

```csharp
// UpdateAlbumRequestHandler.cs — propriété : Id (long)
public sealed class UpdateAlbumRequestValidator : Validator<UpdateAlbumRequest>
{
    public UpdateAlbumRequestValidator() { Rule(x => x.Id).GreaterThan(0L); }
}

// UpdateAlbumFavoriteRequestHandler.cs — propriété : Id (long)
public sealed class UpdateAlbumFavoriteRequestValidator : Validator<UpdateAlbumFavoriteRequest>
{
    public UpdateAlbumFavoriteRequestValidator() { Rule(x => x.Id).GreaterThan(0L); }
}

// UpdateAlbumLastListenRequestHandler.cs — propriété : Id (long)
public sealed class UpdateAlbumLastListenRequestValidator : Validator<UpdateAlbumLastListenRequest>
{
    public UpdateAlbumLastListenRequestValidator() { Rule(x => x.Id).GreaterThan(0L); }
}

// UpdateAlbumPictureDominantColorRequestHandler.cs — propriété : Id (long)
public sealed class UpdateAlbumPictureDominantColorRequestValidator : Validator<UpdateAlbumPictureDominantColorRequest>
{
    public UpdateAlbumPictureDominantColorRequestValidator() { Rule(x => x.Id).GreaterThan(0L); }
}

// UpdateAlbumStatisticsRequestHandler.cs — propriété : Id (long)
public sealed class UpdateAlbumStatisticsRequestValidator : Validator<UpdateAlbumStatisticsRequest>
{
    public UpdateAlbumStatisticsRequestValidator() { Rule(x => x.Id).GreaterThan(0L); }
}

// UpdateAlbumTagsRequestHandler.cs — propriété : Id (long)
public sealed class UpdateAlbumTagsRequestValidator : Validator<UpdateAlbumTagsRequest>
{
    public UpdateAlbumTagsRequestValidator() { Rule(x => x.Id).GreaterThan(0L); }
}

// GetAlbumByIdRequestHandler.cs — propriété : Id (long)
public sealed class GetAlbumByIdRequestValidator : Validator<GetAlbumByIdRequest>
{
    public GetAlbumByIdRequestValidator() { Rule(x => x.Id).GreaterThan(0L); }
}

// GetAlbumsByArtistIdRequestHandler.cs — propriété : ArtistId (long)
public sealed class GetAlbumsByArtistIdRequestValidator : Validator<GetAlbumsByArtistIdRequest>
{
    public GetAlbumsByArtistIdRequestValidator() { Rule(x => x.ArtistId).GreaterThan(0L); }
}

// GetAlbumsByGenreIdRequestHandler.cs — propriété : GenreId (long)
public sealed class GetAlbumsByGenreIdRequestValidator : Validator<GetAlbumsByGenreIdRequest>
{
    public GetAlbumsByGenreIdRequestValidator() { Rule(x => x.GenreId).GreaterThan(0L); }
}
```

Vérifier les noms de propriétés en ouvrant l'ancien fichier source ; si différents, adapter le `Rule(x => x.…)`.

- [ ] **Step 5: Écrire 9 tests négatifs (un par validator) — pattern identique à Task 7 Step 5**

Pour chaque validator, créer le fichier `tests/UnitTests/Rok.ApplicationTests/Features/Albums/Validators/<Name>RequestValidatorTests.cs` avec un test `fails_when_<id>_is_zero` et `succeeds_when_<id>_is_positive`. Exemple :

```csharp
// tests/UnitTests/Rok.ApplicationTests/Features/Albums/Validators/GetAlbumByIdRequestValidatorTests.cs
using CleanArch.DevKit.Mediator.Validation;
using Rok.Application.Features.Albums.Requests;

namespace Rok.ApplicationTests.Features.Albums.Validators;

public class GetAlbumByIdRequestValidatorTests
{
    [Fact(DisplayName = "fails_when_id_is_zero")]
    public async Task fails_when_id_is_zero()
    {
        GetAlbumByIdRequestValidator sut = new();
        GetAlbumByIdRequest request = new(0);

        ValidationResult result = await sut.ValidateAsync(request, CancellationToken.None);

        Assert.False(result.IsValid);
    }

    [Fact(DisplayName = "succeeds_when_id_is_positive")]
    public async Task succeeds_when_id_is_positive()
    {
        GetAlbumByIdRequestValidator sut = new();
        GetAlbumByIdRequest request = new(42);

        ValidationResult result = await sut.ValidateAsync(request, CancellationToken.None);

        Assert.True(result.IsValid);
    }
}
```

- [ ] **Step 6: Si des tests d'intégration existent pour ces handlers** (`tests/UnitTests/Rok.ApplicationTests/Features/Albums/Command/*.cs` et `Query/*.cs`), les déplacer aussi via `git mv` et adapter les references aux types.

```bash
cd D:/Development/MF/Rok && find tests/UnitTests/Rok.ApplicationTests/Features/Albums -type f -name "*.cs"
```

Si des fichiers existent dans `Command/` ou `Query/`, les déplacer vers `Requests/` et renommer (`*CommandHandlerTests.cs` → `*RequestHandlerTests.cs`).

- [ ] **Step 7: Build pour vérifier**

```bash
cd D:/Development/MF/Rok && dotnet build /p:Platform=x64 2>&1 | grep -E "error.*Albums" | head -10
```

Expected : 0 erreur dans `Features/Albums/`.

- [ ] **Step 8: Commit**

```bash
cd D:/Development/MF/Rok && git add -A && git commit -m "refactor(app): migrate Albums feature to CleanArch IRequest + validators"
```

---

## Task 9 — Migrer la feature **Artists**

Artists : 13 handlers (10 commands + 3 queries), 10 validators à écrire.

**Files à `git mv`** :

```
Command/CreateArtistCommandHandler.cs → Requests/CreateArtistRequestHandler.cs              [validator]
Command/DeleteArtistCommandHandler.cs → Requests/DeleteArtistRequestHandler.cs              [validator]
Command/ResetArtistListenCountCommandHandler.cs → Requests/ResetArtistListenCountRequestHandler.cs
Command/UpdateArtistCommandHandler.cs → Requests/UpdateArtistRequestHandler.cs              [validator]
Command/UpdateArtistFavoriteCommandHandler.cs → Requests/UpdateArtistFavoriteRequestHandler.cs   [validator]
Command/UpdateArtistGetMetaDataLastAttemptCommandHandler.cs → Requests/UpdateArtistGetMetaDataLastAttemptRequestHandler.cs
Command/UpdateArtistLastListenCommandHandler.cs → Requests/UpdateArtistLastListenRequestHandler.cs   [validator]
Command/UpdateArtistPictureDominantColorCommandHandler.cs → Requests/UpdateArtistPictureDominantColorRequestHandler.cs   [validator]
Command/UpdateArtistStatisticsCommandHandler.cs → Requests/UpdateArtistStatisticsRequestHandler.cs   [validator]
Command/UpdateArtistTagsCommandHandler.cs → Requests/UpdateArtistTagsRequestHandler.cs              [validator]
Query/GetAllArtistsQueryHandler.cs → Requests/GetAllArtistsRequestHandler.cs
Query/GetArtistByIdQueryHandler.cs → Requests/GetArtistByIdRequestHandler.cs                [validator]
Query/GetArtistByNameQueryHandler.cs → Requests/GetArtistByNameRequestHandler.cs            [validator]
```

- [ ] **Step 1: Créer les dossiers + `git mv` les fichiers + supprimer Command/Query vides** (même pattern que Task 8 Steps 1-2)

- [ ] **Step 2: Adapter le contenu de chaque fichier** (mêmes transformations Task 7 Step 4)

- [ ] **Step 3: Validators**

`CreateArtist` a `[Required] string Name` (vérifier le nom de la propriété et son type — si `string`, utiliser `Required()` ; si `string?`, idem). Tous les autres validators portent sur des `long Id` / `long ArtistId`.

```csharp
// CreateArtistRequestHandler.cs — propriété : Name (string)
public sealed class CreateArtistRequestValidator : Validator<CreateArtistRequest>
{
    public CreateArtistRequestValidator() { Rule(x => x.Name).Required(); }
}

// DeleteArtistRequestHandler.cs — propriété : Id (long)
public sealed class DeleteArtistRequestValidator : Validator<DeleteArtistRequest>
{
    public DeleteArtistRequestValidator() { Rule(x => x.Id).GreaterThan(0L); }
}

// UpdateArtistRequestHandler.cs — propriété : Id (long)
public sealed class UpdateArtistRequestValidator : Validator<UpdateArtistRequest>
{
    public UpdateArtistRequestValidator() { Rule(x => x.Id).GreaterThan(0L); }
}

// UpdateArtistFavoriteRequestHandler.cs — propriété : Id (long)
public sealed class UpdateArtistFavoriteRequestValidator : Validator<UpdateArtistFavoriteRequest>
{
    public UpdateArtistFavoriteRequestValidator() { Rule(x => x.Id).GreaterThan(0L); }
}

// UpdateArtistLastListenRequestHandler.cs — propriété : Id (long)
public sealed class UpdateArtistLastListenRequestValidator : Validator<UpdateArtistLastListenRequest>
{
    public UpdateArtistLastListenRequestValidator() { Rule(x => x.Id).GreaterThan(0L); }
}

// UpdateArtistPictureDominantColorRequestHandler.cs — propriété : Id (long)
public sealed class UpdateArtistPictureDominantColorRequestValidator : Validator<UpdateArtistPictureDominantColorRequest>
{
    public UpdateArtistPictureDominantColorRequestValidator() { Rule(x => x.Id).GreaterThan(0L); }
}

// UpdateArtistStatisticsRequestHandler.cs — propriété : Id (long)
public sealed class UpdateArtistStatisticsRequestValidator : Validator<UpdateArtistStatisticsRequest>
{
    public UpdateArtistStatisticsRequestValidator() { Rule(x => x.Id).GreaterThan(0L); }
}

// UpdateArtistTagsRequestHandler.cs — propriété : Id (long)
public sealed class UpdateArtistTagsRequestValidator : Validator<UpdateArtistTagsRequest>
{
    public UpdateArtistTagsRequestValidator() { Rule(x => x.Id).GreaterThan(0L); }
}

// GetArtistByIdRequestHandler.cs — propriété : Id (long)
public sealed class GetArtistByIdRequestValidator : Validator<GetArtistByIdRequest>
{
    public GetArtistByIdRequestValidator() { Rule(x => x.Id).GreaterThan(0L); }
}

// GetArtistByNameRequestHandler.cs — propriété : Name (string)
public sealed class GetArtistByNameRequestValidator : Validator<GetArtistByNameRequest>
{
    public GetArtistByNameRequestValidator() { Rule(x => x.Name).Required(); }
}
```

- [ ] **Step 4: 10 tests négatifs** dans `tests/UnitTests/Rok.ApplicationTests/Features/Artists/Validators/` — un par validator. Pour les validators portant sur `Id` (long), pattern identique au Task 8. Pour les validators sur `Name` (string), utiliser `string.Empty` comme cas d'échec :

```csharp
// tests/UnitTests/Rok.ApplicationTests/Features/Artists/Validators/CreateArtistRequestValidatorTests.cs
using CleanArch.DevKit.Mediator.Validation;
using Rok.Application.Features.Artists.Requests;

namespace Rok.ApplicationTests.Features.Artists.Validators;

public class CreateArtistRequestValidatorTests
{
    [Fact(DisplayName = "fails_when_name_is_empty")]
    public async Task fails_when_name_is_empty()
    {
        CreateArtistRequestValidator sut = new();
        CreateArtistRequest request = new(string.Empty);   // adapter au constructor réel

        ValidationResult result = await sut.ValidateAsync(request, CancellationToken.None);

        Assert.False(result.IsValid);
    }

    [Fact(DisplayName = "succeeds_when_name_is_provided")]
    public async Task succeeds_when_name_is_provided()
    {
        CreateArtistRequestValidator sut = new();
        CreateArtistRequest request = new("Pink Floyd");   // adapter au constructor réel

        ValidationResult result = await sut.ValidateAsync(request, CancellationToken.None);

        Assert.True(result.IsValid);
    }
}
```

Si la classe a un constructor sans paramètre ou un setter, adapter (par exemple `new CreateArtistRequest { Name = string.Empty }`).

- [ ] **Step 5: Déplacer les tests d'intégration `Command/`/`Query/` → `Requests/`** (si existants)

- [ ] **Step 6: Build vert sur Artists**

```bash
cd D:/Development/MF/Rok && dotnet build /p:Platform=x64 2>&1 | grep -E "error.*Artists" | head -10
```

- [ ] **Step 7: Commit**

```bash
cd D:/Development/MF/Rok && git add -A && git commit -m "refactor(app): migrate Artists feature to CleanArch IRequest + validators"
```

---

## Task 10 — Migrer la feature **Genres**

Genres : 5 handlers (3 commands + 2 queries), 3 validators.

**Files à `git mv`** :

```
Command/ResetGenreListenCountCommandHandler.cs → Requests/ResetGenreListenCountRequestHandler.cs
Command/UpdateGenreFavoriteCommandHandler.cs → Requests/UpdateGenreFavoriteRequestHandler.cs     [validator]
Command/UpdateGenretLastListenCommandHandler.cs → Requests/UpdateGenretLastListenRequestHandler.cs   [validator]
Query/GetAllGenresQueryHandler.cs → Requests/GetAllGenresRequestHandler.cs
Query/GetGenreByIdQueryHandler.cs → Requests/GetGenreByIdRequestHandler.cs                       [validator]
```

Note : la coquille `Genret` (au lieu de `Genre`) dans `UpdateGenretLastListen` est conservée pour rester en simple renommage. Une correction ortho est hors scope.

- [ ] **Step 1: Dossiers + git mv + suppression Command/Query**
- [ ] **Step 2: Adapter chaque fichier**
- [ ] **Step 3: Validators**

```csharp
// UpdateGenreFavoriteRequestHandler.cs — propriété : Id (long)
public sealed class UpdateGenreFavoriteRequestValidator : Validator<UpdateGenreFavoriteRequest>
{
    public UpdateGenreFavoriteRequestValidator() { Rule(x => x.Id).GreaterThan(0L); }
}

// UpdateGenretLastListenRequestHandler.cs — propriété : Id (long)
public sealed class UpdateGenretLastListenRequestValidator : Validator<UpdateGenretLastListenRequest>
{
    public UpdateGenretLastListenRequestValidator() { Rule(x => x.Id).GreaterThan(0L); }
}

// GetGenreByIdRequestHandler.cs — propriété : Id (long)
public sealed class GetGenreByIdRequestValidator : Validator<GetGenreByIdRequest>
{
    public GetGenreByIdRequestValidator() { Rule(x => x.Id).GreaterThan(0L); }
}
```

- [ ] **Step 4: Tests négatifs (3 fichiers)** — même pattern que Task 8 Step 5
- [ ] **Step 5: Déplacer les tests d'intégration**
- [ ] **Step 6: Build + commit**

```bash
cd D:/Development/MF/Rok && dotnet build /p:Platform=x64 2>&1 | grep -E "error.*Genres" | head -10 && git add -A && git commit -m "refactor(app): migrate Genres feature to CleanArch IRequest + validators"
```

---

## Task 11 — Migrer la feature **Playlists**

Playlists : 14 handlers (11 commands + 3 queries), 4 validators.

**Files à `git mv`** :

```
Command/AddAlbumToPlaylistCommandHandler.cs → Requests/AddAlbumToPlaylistRequestHandler.cs
Command/AddArtistToPlaylistCommandHandler.cs → Requests/AddArtistToPlaylistRequestHandler.cs
Command/AddTrackToPlaylistCommandHandler.cs → Requests/AddTrackToPlaylistRequestHandler.cs
Command/CreatePlaylistCommandHandler.cs → Requests/CreatePlaylistRequestHandler.cs
Command/CreatePlaylistTracksCommandHandler.cs → Requests/CreatePlaylistTracksRequestHandler.cs
Command/DeletePlaylistCommandHandler.cs → Requests/DeletePlaylistRequestHandler.cs                      [validator]
Command/ExportPlaylistCommandHandler.cs → Requests/ExportPlaylistRequestHandler.cs
Command/ImportPlaylistCommandHandler.cs → Requests/ImportPlaylistRequestHandler.cs
Command/MovePlaylistTracksCommandHandler.cs → Requests/MovePlaylistTracksRequestHandler.cs
Command/RemoveTrackFromPlaylistCommandHandler.cs → Requests/RemoveTrackFromPlaylistRequestHandler.cs
Command/UpdatePlaylistCommandHandler.cs → Requests/UpdatePlaylistRequestHandler.cs                      [validator]
Command/UpdatePlaylistPictureCommandHandler.cs → Requests/UpdatePlaylistPictureRequestHandler.cs        [validator]
Query/GeneratePlaylistTracksQueryHandler.cs → Requests/GeneratePlaylistTracksRequestHandler.cs
Query/GetAllPlaylistsQueryHandler.cs → Requests/GetAllPlaylistsRequestHandler.cs
Query/GetPlaylistByIdQueryHandler.cs → Requests/GetPlaylistByIdRequestHandler.cs                        [validator]
```

**Cas particulier `ExportPlaylistCommandHandler.cs`** : il utilise déjà `_mediator.SendMessageAsync(...)` à l'intérieur (cf. spec). En plus du renommage usuel, ces appels deviennent `_mediator.Send(...)`. Le using nominatif `using MiF.Mediator.Interfaces;` en haut de fichier devient `using CleanArch.DevKit.Mediator;`.

- [ ] **Step 1: Dossiers + git mv**
- [ ] **Step 2: Adapter chaque fichier** — attention à `ExportPlaylistRequestHandler.cs` qui contient des appels mediator internes (déjà utilisés)
- [ ] **Step 3: Validators**

Vérifier les noms exacts en ouvrant chaque fichier. Probablement :

```csharp
// DeletePlaylistRequestHandler.cs — propriété : Id (long) [Required]
// Note : Required sur long n'a aucun effet en DataAnnotations mais on garde la sémantique « valeur explicite »
public sealed class DeletePlaylistRequestValidator : Validator<DeletePlaylistRequest>
{
    public DeletePlaylistRequestValidator() { Rule(x => x.Id).GreaterThan(0L); }
}

// UpdatePlaylistRequestHandler.cs — propriété [Required] : Id (long). Le Name n'a pas d'attribut.
public sealed class UpdatePlaylistRequestValidator : Validator<UpdatePlaylistRequest>
{
    public UpdatePlaylistRequestValidator()
    {
        Rule(x => x.Id).GreaterThan(0L);
    }
}

// UpdatePlaylistPictureRequestHandler.cs — propriété [Required] : Id (long). Picture est string sans attribut.
public sealed class UpdatePlaylistPictureRequestValidator : Validator<UpdatePlaylistPictureRequest>
{
    public UpdatePlaylistPictureRequestValidator()
    {
        Rule(x => x.Id).GreaterThan(0L);
    }
}

// GetPlaylistByIdRequestHandler.cs — propriété : Id (long)
public sealed class GetPlaylistByIdRequestValidator : Validator<GetPlaylistByIdRequest>
{
    public GetPlaylistByIdRequestValidator() { Rule(x => x.Id).GreaterThan(0L); }
}
```

- [ ] **Step 4: Tests négatifs (4 fichiers)**
- [ ] **Step 5: Déplacer les tests d'intégration**
- [ ] **Step 6: Build + commit**

```bash
cd D:/Development/MF/Rok && dotnet build /p:Platform=x64 2>&1 | grep -E "error.*Playlists" | head -10 && git add -A && git commit -m "refactor(app): migrate Playlists feature to CleanArch IRequest + validators"
```

---

## Task 12 — Migrer la feature **EqualizerPresets**

EqualizerPresets : 3 handlers (2 commands + 1 query), 1 validator. `DeleteEqualizerPresetCommand` n'a qu'un `[Required]` inerte sur enum — aucune règle non-inerte à porter, pas de validator créé.

**Files à `git mv`** :

```
Command/DeleteEqualizerPresetCommandHandler.cs → Requests/DeleteEqualizerPresetRequestHandler.cs      [validator]
Command/SaveEqualizerPresetCommandHandler.cs → Requests/SaveEqualizerPresetRequestHandler.cs          [validator]
Query/GetEqualizerPresetQueryHandler.cs → Requests/GetEqualizerPresetRequestHandler.cs
```

**Note sur `[Required]` sur enum** (`SaveEqualizerPreset.Scope`, `DeleteEqualizerPreset.Scope`) : cf. spec — le `[Required]` MiF est inerte sur enum, donc **pas de règle ajoutée**. Sauf si l'analyse fonctionnelle révèle qu'une valeur par défaut doit être refusée — dans ce cas ajouter `NotEqual(default(EqualizerScope))`.

- [ ] **Step 1: Dossiers + git mv**
- [ ] **Step 2: Adapter chaque fichier — retirer les `[Required]` enum sans les remplacer (décision documentée)**
- [ ] **Step 3: Validators**

```csharp
// SaveEqualizerPresetRequestHandler.cs
// Propriétés : Scope (enum, [Required] inerte → règle omise), BuiltinPresetKey (string?), ScopeId (long?), Bands (float[] [Required])
public sealed class SaveEqualizerPresetRequestValidator : Validator<SaveEqualizerPresetRequest>
{
    public SaveEqualizerPresetRequestValidator()
    {
        Rule(x => x.Bands).NotNull();
    }
}
```

**Pas de validator pour `DeleteEqualizerPresetRequest`** : son unique `[Required]` porte sur `Scope` (enum), qui est inerte en DataAnnotations. Supprimer le `[Required]` du fichier sans ajouter de validator — il n'y a aucune règle non-inerte à porter.

- [ ] **Step 4: Test négatif (1 fichier — uniquement pour SaveEqualizerPreset)**

Créer :
```csharp
// tests/UnitTests/Rok.ApplicationTests/Features/EqualizerPresets/Validators/SaveEqualizerPresetRequestValidatorTests.cs
using CleanArch.DevKit.Mediator.Validation;
using Rok.Application.Features.EqualizerPresets.Requests;

namespace Rok.ApplicationTests.Features.EqualizerPresets.Validators;

public class SaveEqualizerPresetRequestValidatorTests
{
    [Fact(DisplayName = "fails_when_bands_is_null")]
    public async Task fails_when_bands_is_null()
    {
        SaveEqualizerPresetRequestValidator sut = new();
        SaveEqualizerPresetRequest request = new() { Bands = null! };

        ValidationResult result = await sut.ValidateAsync(request, CancellationToken.None);

        Assert.False(result.IsValid);
    }
}
```

- [ ] **Step 5: Build + commit**

```bash
cd D:/Development/MF/Rok && dotnet build /p:Platform=x64 2>&1 | grep -E "error.*EqualizerPresets" | head -10 && git add -A && git commit -m "refactor(app): migrate EqualizerPresets feature to CleanArch IRequest + validators"
```

---

## Task 13 — Migrer les features simples (**Insights**, **Statistics**, **Tags**, **Search**, **ListeningEvents**)

Tous regroupés en une tâche : 6 handlers au total, 2 validators (Search + ListeningEvents).

**Files à `git mv`** :

```
Insights/Query/GetInsightsQueryHandler.cs → Insights/Requests/GetInsightsRequestHandler.cs
ListeningEvents/Command/CreateListeningEventCommandHandler.cs → ListeningEvents/Requests/CreateListeningEventRequestHandler.cs   [validator]
Search/Query/SearchQueryHandler.cs → Search/Requests/SearchRequestHandler.cs                                              [validator]
Statistics/Query/GetLyricsStatisticsQueryHandler.cs → Statistics/Requests/GetLyricsStatisticsRequestHandler.cs
Statistics/Query/GetStatisticsQueryHandler.cs → Statistics/Requests/GetStatisticsRequestHandler.cs
Tags/Query/GetAllTagsQueryHandler.cs → Tags/Requests/GetAllTagsRequestHandler.cs
```

- [ ] **Step 1: Créer les dossiers + git mv tous les fichiers + supprimer Command/Query**

```bash
cd D:/Development/MF/Rok
for f in Insights ListeningEvents Search Statistics Tags ; do
  mkdir -p src/Rok.Application/Features/$f/Requests
done
# git mv calls similar to previous tasks — one per file
```

- [ ] **Step 2: Adapter le contenu de chaque fichier**

- [ ] **Step 3: Validators**

```csharp
// SearchRequestHandler.cs — propriété : Name (string) [Required]
public sealed class SearchRequestValidator : Validator<SearchRequest>
{
    public SearchRequestValidator() { Rule(x => x.Name).Required(); }
}

// CreateListeningEventRequestHandler.cs — propriété [Required] : TrackId (long). Les autres (ArtistId, AlbumId, GenreId, durations) n'ont pas d'attribut.
public sealed class CreateListeningEventRequestValidator : Validator<CreateListeningEventRequest>
{
    public CreateListeningEventRequestValidator()
    {
        Rule(x => x.TrackId).GreaterThan(0L);
    }
}
```

- [ ] **Step 4: Tests négatifs (2 fichiers)** — `tests/UnitTests/Rok.ApplicationTests/Features/Search/Validators/SearchRequestValidatorTests.cs` et idem pour ListeningEvents.

- [ ] **Step 5: Build + commit**

```bash
cd D:/Development/MF/Rok && dotnet build /p:Platform=x64 2>&1 | grep -E "error.*Application" | head -20 && git add -A && git commit -m "refactor(app): migrate Insights/Statistics/Tags/Search/ListeningEvents to CleanArch IRequest + validators"
```

---

## Task 14 — Mettre à jour les sites d'appel en Présentation et Import

**Files (sources principales — ouverte une par une) :**

Tous les fichiers qui utilisent `MiF.Mediator.Interfaces` ou appellent `SendMessageAsync` :

```
src/Presentation/ViewModels/Playlist/Services/PlaylistExportService.cs
src/Presentation/Services/PlaylistMenuService.cs (10+ appels)
src/Presentation/Services/PlaylistsSeed.cs (3 appels)
src/Presentation/Services/TagsProvider.cs (1 appel)
src/Presentation/ViewModels/Main/MainViewModel.cs (1 appel)
src/Presentation/ViewModels/Main/SearchSuggestionsViewModel.cs (3 appels)
src/Presentation/ViewModels/Player/EqualizerViewModel.cs (3 appels)
src/Presentation/ViewModels/Player/PlayerViewModel.cs (1 appel)
src/Presentation/ViewModels/Playlists/Services/PlaylistImportService.cs
src/Presentation/ViewModels/Start/StartViewModel.cs (1 appel)
src/Rok.Import/Services/PostImportApiEnrichmentTask.cs
```

(et tous les autres `*DataLoader.cs`, `*Provider.cs`, `*Service.cs` qui injectent `IMediator` — leurs appels `_mediator.SendMessageAsync` doivent passer à `Send` ET les `new <Name>Command(...)` / `new <Name>Query(...)` doivent passer à `new <Name>Request(...)`)

- [ ] **Step 1: Mettre à jour tous les usings nominatifs**

```bash
cd D:/Development/MF/Rok && grep -rln "using MiF.Mediator.Interfaces" src/Presentation src/Rok.Import --include="*.cs" | grep -v "/obj/"
```

Pour chaque fichier listé, ouvrir et remplacer `using MiF.Mediator.Interfaces;` par `using CleanArch.DevKit.Mediator;`.

(Cas où le using est en doublon avec un `global using` : la directive nominative peut être simplement supprimée.)

- [ ] **Step 2: Remplacer tous les appels `_mediator.SendMessageAsync` par `_mediator.Send`**

```bash
cd D:/Development/MF/Rok && grep -rn "SendMessageAsync" src/Presentation src/Rok.Import --include="*.cs" | grep -v "/obj/"
```

Pour chaque occurrence, remplacer `SendMessageAsync` par `Send`. Garder les arguments inchangés.

- [ ] **Step 3: Renommer les instanciations `new <Name>Command(...)` → `new <Name>Request(...)`**

```bash
cd D:/Development/MF/Rok && grep -rE "new [A-Z][A-Za-z0-9_]+Command\b" src/Presentation src/Rok.Import --include="*.cs" | grep -v "/obj/" | grep -v "System.Windows.Input"
```

Pour chaque résultat, renommer en `<Name>Request`. Idem pour `new <Name>Query(...)` :

```bash
cd D:/Development/MF/Rok && grep -rE "new [A-Z][A-Za-z0-9_]+Query\b" src/Presentation src/Rok.Import --include="*.cs" | grep -v "/obj/"
```

Note : prudence aux types DTO ou composants qui pourraient contenir `Query` dans leur nom sans rapport (par exemple un type d'enum, pas vu dans Rok pour le moment).

- [ ] **Step 4: Build pour vérifier**

```bash
cd D:/Development/MF/Rok && dotnet build /p:Platform=x64 2>&1 | tail -30
```

Expected : 0 erreur sur `src/Presentation/` et `src/Rok.Import/`. Erreurs résiduelles éventuelles sur les tests (traités en Task 15).

- [ ] **Step 5: Commit**

```bash
cd D:/Development/MF/Rok && git add -A && git commit -m "refactor: update Presentation and Import call sites to CleanArch.DevKit.Mediator"
```

---

## Task 15 — Mettre à jour les mocks et types dans les tests

**Files (~40 fichiers) :**

```bash
cd D:/Development/MF/Rok && grep -rln "MiF.Mediator\|SendMessageAsync\|ICommand<\|IQuery<\|ICommandHandler\|IQueryHandler\|HandleAsync" tests/ --include="*.cs"
```

Pour chaque fichier :

1. Remplacer `using MiF.Mediator.Interfaces;` (et `using MiF.Mediator;`) par `using CleanArch.DevKit.Mediator;` (souvent redondant avec GlobalUsings, peut être supprimé)
2. `Mock<IMediator>` reste inchangé (le type lui-même garde le même nom)
3. `m.Setup(x => x.SendMessageAsync<TResponse>(...))` → `m.Setup(x => x.Send<TResponse>(...))` — mêmes paramètres
4. Tout `new <Name>Command(...)` ou `new <Name>Query(...)` → `new <Name>Request(...)`
5. Tout `*Command` ou `*Query` dans les déclarations de variables → `*Request`
6. `Unit.Result` → `Unit.Value`
7. Si un test mocke un Handler concret (rare), `IRequestHandler<TReq, TRes>` et `Handle` (au lieu de `HandleAsync`)

- [ ] **Step 1: Lister tous les fichiers concernés**

```bash
cd D:/Development/MF/Rok && grep -rln "SendMessageAsync\|MiF.Mediator" tests/ --include="*.cs" > /tmp/test-files-to-update.txt
cat /tmp/test-files-to-update.txt | wc -l
```

Expected : ~30-40 fichiers.

- [ ] **Step 2: Pour chaque fichier de la liste, appliquer les transformations** (utiliser `Edit` tool avec `replace_all` si confiance ; sinon Edit fichier par fichier)

Patterns à appliquer dans chaque fichier :
- `using MiF.Mediator.Interfaces;` → (supprimer si redondant avec GlobalUsings, sinon `using CleanArch.DevKit.Mediator;`)
- `using MiF.Mediator;` → idem
- `SendMessageAsync` → `Send`
- `HandleAsync` → `Handle`
- `Unit.Result` → `Unit.Value`
- `<X>Command` → `<X>Request` (attention à ne pas toucher à `System.Windows.Input.ICommand` ; faux positif exclu par contexte)
- `<X>Query` → `<X>Request`

- [ ] **Step 3: Build + run tests**

```bash
cd D:/Development/MF/Rok && dotnet test /p:Platform=x64 --no-restore 2>&1 | tail -30
```

Expected : `Passed!` sur tous les projets de tests. Si des tests échouent, c'est en général un mock mal converti — relire l'erreur, corriger, retenter.

- [ ] **Step 4: Commit**

```bash
cd D:/Development/MF/Rok && git add -A && git commit -m "refactor(tests): update Moq setups and request types for CleanArch.DevKit.Mediator"
```

---

## Task 16 — Supprimer `Rok.Shared.ValidationAttributes`

**Files:**
- Delete : `src/Rok.Shared/ValidationAttributes/RequiredGreaterThanZero.cs`
- Delete : `tests/UnitTests/Rok.ApplicationTests/Shared/ValidationAttributes/RequiredGreaterThanZeroTests.cs`

- [ ] **Step 1: Vérifier qu'aucune référence ne subsiste**

```bash
cd D:/Development/MF/Rok && grep -rn "RequiredGreaterThanZero\|Rok.Shared.ValidationAttributes" src/ tests/ --include="*.cs" | grep -v "/obj/"
```

Expected : 0 occurrence hors du fichier à supprimer lui-même et de son test. Si présent ailleurs → un validator a été oublié dans une feature, retourner à la tâche correspondante.

- [ ] **Step 2: Supprimer les fichiers et le dossier vide**

```bash
cd D:/Development/MF/Rok
git rm src/Rok.Shared/ValidationAttributes/RequiredGreaterThanZero.cs
git rm tests/UnitTests/Rok.ApplicationTests/Shared/ValidationAttributes/RequiredGreaterThanZeroTests.cs
rmdir src/Rok.Shared/ValidationAttributes tests/UnitTests/Rok.ApplicationTests/Shared/ValidationAttributes 2>/dev/null
```

- [ ] **Step 3: Retirer le `global using Rok.Shared.ValidationAttributes;`**

Si encore présent dans `src/Rok.Application/GlobalUsings.cs`, le supprimer (déjà fait en Task 6, mais re-vérifier).

```bash
cd D:/Development/MF/Rok && grep "Rok.Shared.ValidationAttributes" src/Rok.Application/GlobalUsings.cs
```

Expected : 0 résultat.

- [ ] **Step 4: Build + commit**

```bash
cd D:/Development/MF/Rok && dotnet build /p:Platform=x64 2>&1 | tail -10
git add -A && git commit -m "refactor(shared): remove orphan RequiredGreaterThanZero validation attribute"
```

---

## Task 17 — Vérification finale et smoke test

- [ ] **Step 1: Vérifier la couverture complète des renommages**

```bash
cd D:/Development/MF/Rok

# Aucun symbole MiF.Mediator restant
grep -rn "MiF\.Mediator\|MiF\.Mediator\.Interfaces\|MiF\.Mediator\.DependencyInjection" src/ tests/ --include="*.cs" | grep -v "/obj/" | grep -v "/docs/"

# Aucun ancien symbole d'interface
grep -rn "ICommandHandler\|IQueryHandler\|IRequestPreProcessor\|HandleAsync\|SendMessageAsync\|Unit\.Result" src/ tests/ --include="*.cs" | grep -v "/obj/" | grep -v "System.Windows.Input"

# Aucun [Required]/[RequiredGreaterThanZero] dans Application
grep -rn '\[Required\]\|\[RequiredGreaterThanZero\]' src/Rok.Application --include="*.cs" | grep -v "/obj/"

# Aucun dossier Command/Query dans Features
find src/Rok.Application/Features -type d -name "Command" -o -name "Query"
```

Expected : toutes les commandes retournent 0 résultat (sauf `System.Windows.Input.ICommand` qui est un faux positif acceptable côté XAML).

- [ ] **Step 2: Vérifier la présence des éléments attendus**

```bash
cd D:/Development/MF/Rok

# Mediator partial class
grep "partial class Mediator" src/Rok.Application/Mediator.cs

# LoggingPipelineBehavior présent et bien placé
[ -f src/Rok.Application/Pipeline/LoggingPipelineBehavior.cs ] && echo "behavior OK"

# 67 fichiers handlers dans Requests/
find src/Rok.Application/Features -path "*/Requests/*RequestHandler.cs" | wc -l

# Validators colocalisés dans 34 fichiers (recherche du pattern `: Validator<`)
grep -rl ": Validator<" src/Rok.Application/Features --include="*.cs" | grep -v "/obj/" | wc -l

# Test négatif par validator (au moins 34 fichiers de test)
find tests/UnitTests/Rok.ApplicationTests/Features -name "*RequestValidatorTests.cs" | wc -l
```

Expected :
- partial class présente
- behavior OK
- 67 handlers
- 34 fichiers avec validator colocalisé
- 34 fichiers de tests négatifs

- [ ] **Step 3: Build x64 complet**

```bash
cd D:/Development/MF/Rok && dotnet build /p:Platform=x64 2>&1 | tail -15
```

Expected : `Build succeeded. 0 Warning(s). 0 Error(s).`

Si erreurs résiduelles : lire et corriger ; éviter `// removed` et autres hacks de contournement.

- [ ] **Step 4: Run tous les tests**

```bash
cd D:/Development/MF/Rok && dotnet test /p:Platform=x64 --no-restore --no-build 2>&1 | tail -30
```

Expected : `Passed: <total>, Failed: 0`.

- [ ] **Step 5: Smoke test manuel WinUI**

Lancer Rok (depuis Visual Studio en x64, ou via la commande de packaging usuelle), puis :

1. ✅ L'app démarre sans crash.
2. ✅ Naviguer vers la page Albums : la liste se charge.
3. ✅ Cliquer sur un album : la vue détail s'affiche (déclenche `GetAlbumByIdRequest` via mediator).
4. ✅ Naviguer vers Artists : la liste se charge.
5. ✅ Créer une playlist depuis le menu contextuel d'une piste : la playlist persiste en base.
6. ✅ Lancer un import library (si bibliothèque dispo) : les `*ImportedMessageHandler` s'exécutent (via MiF.SimpleMessenger — hors scope étape 1).
7. ✅ Tester la validation : envoyer une requête avec un Id de 0 (par exemple via une action UI qui passerait par `GetTrackByIdRequest`) — devrait remonter une `ValidationException` côté `_logger` ou être visible via une notification d'erreur. À défaut d'UI direct pour tester, écrire un test d'intégration ponctuel qui appelle `_mediator.Send(new GetTrackByIdRequest(0))` et vérifie le throw.

Documenter les résultats dans le commit message (✅ ou ❌ par item).

- [ ] **Step 6: Commit final (marqueur de fin de migration)**

```bash
cd D:/Development/MF/Rok && git status --short
# Si tout est commité, on peut créer un commit de "tag" vide ou simplement laisser le commit du dernier ajustement
# Sinon, commiter les retouches issues du smoke test :
git add -A
git commit -m "refactor: finalize CleanArch.DevKit.Mediator migration (smoke-tested)"
```

- [ ] **Step 7: Vérifier la pile de commits sur la branche**

```bash
cd D:/Development/MF/Rok && git log --oneline master..HEAD | wc -l
```

Expected : ~17-20 commits. Ils peuvent être squashés lors du merge (au choix du reviewer).

---

## Self-Review post-rédaction

Avant exécution, l'agent doit relire le plan pour vérifier :

1. **Coverage du spec** :
   - Renommages API mediator (Section A du spec) → couvert Task 2-13
   - Renommages Command/Query → Request (Section B du spec) → couvert Task 7-13 (`git mv`)
   - Pipeline behavior (Section C du spec) → couvert Task 4
   - Validators (Section D du spec) → couvert Task 7-13 (35 validators)
   - Suppression de Rok.Shared.ValidationAttributes → couvert Task 16
   - Critères d'acceptation du spec → couverts Task 17

2. **Pas de placeholders** : aucun "TBD", "TODO", "à compléter" dans le plan.

3. **Cohérence des noms** : `IPipelineBehavior` partout, `RequestHandlerDelegate<TResponse>` (pas de variante), `ValidationException`, `ValidationResult.IsValid`, etc.

4. **Commandes shell exactes** : toutes les commandes `git mv` et `grep` sont copiables sans modification.

5. **Pour les fichiers où le nom des propriétés est incertain** (UpdatePlaylistPicture, CreateListeningEvent, EqualizerPresets), le plan indique de **lire le fichier source d'abord** pour confirmer.
