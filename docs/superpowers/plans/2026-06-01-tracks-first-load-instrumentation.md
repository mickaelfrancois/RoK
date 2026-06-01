# Tracks First-Load Instrumentation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Instrumenter le premier chargement de la page Titres pour attribuer le temps mur entre fetch SQL, mapping DTO, création des ViewModels et FilterAndSort, et corriger le double-mapping DTO.

**Architecture:** On enveloppe quatre régions du chemin de chargement dans le `PerfLogger` existant (log `LogInformation`). On matérialise le mapping DTO une seule fois dans le handler, ce qui supprime la double-énumération et rend la région `DTO map` mesurable indépendamment. Aucune optimisation lourde n'est engagée à ce stade — la décision viendra des mesures.

**Tech Stack:** .NET 10 / C# 13, CleanArch.DevKit.Mediator, Dapper/SQLite, Serilog (via `PerfLogger` dans `Rok.Shared`), xUnit + Moq.

**Spec source:** `docs/superpowers/specs/2026-06-01-tracks-first-load-instrumentation-design.md`

**Branche:** `perf/tracks-first-load-instrumentation` (déjà créée et active).

---

## Référence : emplacements clés

- Handler liste : `src/Rok.Application/Features/Tracks/Requests/GetAllTracksRequestHandler.cs`
- Création VM : `src/Presentation/ViewModels/Tracks/Services/TracksDataLoader.cs` (`LoadTracksAsync`, ligne 14-25)
- FilterAndSort : `src/Presentation/ViewModels/Tracks/TracksViewModel.cs` (`LoadDataAsync`, ligne 80-98)
- Test handler existant : `tests/UnitTests/Rok.ApplicationTests/Features/Tracks/Requests/TrackQueryHandlerTests.cs` (`GetAllTracksQueryHandlerTests`, ligne 43-64)
- `PerfLogger` : `src/Rok.Shared/PerfLogger.cs` — `new PerfLogger(logger).Parameters("label")` dans un `using`, log `LogInformation` à la libération.
- Fichier log runtime : `%LOCALAPPDATA%\Packages\<package-Rok>\LocalState\Logs\rok-<yyyyMMdd>.log` (rolling quotidien, niveau Information écrit en Debug comme en Release).

Global usings d'`Rok.Application` : `Rok.Application.Mapping`, `Rok.Domain.Entities`, `Rok.Application.Dto`, `System.Linq` sont déjà en scope. `Microsoft.Extensions.Logging` et `Rok.Shared` ne le sont **pas** → à ajouter explicitement dans le handler.

---

## Task 1: Matérialiser le mapping DTO + instrumenter le handler

**Files:**
- Modify: `src/Rok.Application/Features/Tracks/Requests/GetAllTracksRequestHandler.cs`
- Test: `tests/UnitTests/Rok.ApplicationTests/Features/Tracks/Requests/TrackQueryHandlerTests.cs` (classe `GetAllTracksQueryHandlerTests`)

- [ ] **Step 1: Écrire le test de matérialisation (et adapter au nouveau constructeur)**

Dans `TrackQueryHandlerTests.cs`, remplacer la classe `GetAllTracksQueryHandlerTests` (ligne 43-64) par :

```csharp
public class GetAllTracksQueryHandlerTests
{
    [Fact(DisplayName = "Handle should return all mapped tracks from repository")]
    public async Task Handle_ShouldReturnAllMappedTracks_FromRepository()
    {
        // Arrange
        List<TrackEntity> tracks = new()
        {
            new() { Id = 1, Title = "One" },
            new() { Id = 2, Title = "Two" }
        };
        Mock<ITrackRepository> repository = new();
        repository.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(tracks);
        GetAllTracksRequestHandler handler = new(repository.Object, new Mock<ILogger<GetAllTracksRequestHandler>>().Object);

        // Act
        IEnumerable<TrackDto> result = await handler.Handle(new GetAllTracksRequest(), CancellationToken.None);

        // Assert
        Assert.Equal(2, result.Count());
    }

    [Fact(DisplayName = "Handle should materialize the mapping so it runs only once")]
    public async Task Handle_ShouldMaterializeMapping_RunningItOnlyOnce()
    {
        // Arrange
        List<TrackEntity> tracks = new() { new() { Id = 1, Title = "One" } };
        Mock<ITrackRepository> repository = new();
        repository.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(tracks);
        GetAllTracksRequestHandler handler = new(repository.Object, new Mock<ILogger<GetAllTracksRequestHandler>>().Object);

        // Act
        IEnumerable<TrackDto> result = await handler.Handle(new GetAllTracksRequest(), CancellationToken.None);
        TrackDto firstEnumeration = result.First();
        TrackDto secondEnumeration = result.First();

        // Assert
        Assert.Same(firstEnumeration, secondEnumeration);
    }
}
```

Ajouter en haut du fichier, sous `using Moq;` :

```csharp
using Microsoft.Extensions.Logging;
```

Note : `Assert.Same` échoue avec l'implémentation paresseuse actuelle (`Select` recrée un `TrackDto` à chaque énumération) et passe une fois la collection matérialisée.

- [ ] **Step 2: Lancer le test pour vérifier qu'il échoue**

Run: `dotnet test tests/UnitTests/Rok.ApplicationTests/Rok.ApplicationTests.csproj /p:Platform=x64 --filter "FullyQualifiedName~GetAllTracksQueryHandlerTests"`
Expected: échec de compilation (le constructeur actuel `GetAllTracksRequestHandler(ITrackRepository)` ne prend pas de logger).

- [ ] **Step 3: Modifier le handler**

Remplacer tout le contenu de `GetAllTracksRequestHandler.cs` par :

```csharp
using Microsoft.Extensions.Logging;
using Rok.Application.Interfaces.Repositories;
using Rok.Shared;

namespace Rok.Application.Features.Tracks.Requests;

public class GetAllTracksRequest : IRequest<IEnumerable<TrackDto>>
{
}

public class GetAllTracksRequestHandler(ITrackRepository _trackRepository, ILogger<GetAllTracksRequestHandler> _logger) : IRequestHandler<GetAllTracksRequest, IEnumerable<TrackDto>>
{
    public async Task<IEnumerable<TrackDto>> Handle(GetAllTracksRequest request, CancellationToken cancellationToken)
    {
        IEnumerable<TrackEntity> tracks;

        using (new PerfLogger(_logger).Parameters("Tracks: DB fetch"))
        {
            tracks = await _trackRepository.GetAllAsync();
        }

        using (new PerfLogger(_logger).Parameters("Tracks: DTO map"))
        {
            return tracks.Select(a => TrackDtoMapping.Map(a)).ToList();
        }
    }
}
```

- [ ] **Step 4: Lancer les tests pour vérifier qu'ils passent**

Run: `dotnet test tests/UnitTests/Rok.ApplicationTests/Rok.ApplicationTests.csproj /p:Platform=x64 --filter "FullyQualifiedName~GetAllTracksQueryHandlerTests"`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/Rok.Application/Features/Tracks/Requests/GetAllTracksRequestHandler.cs tests/UnitTests/Rok.ApplicationTests/Features/Tracks/Requests/TrackQueryHandlerTests.cs
git commit -m "perf(tracks): materialize track DTO mapping once and instrument query handler"
```

---

## Task 2: Instrumenter la création des ViewModels

**Files:**
- Modify: `src/Presentation/ViewModels/Tracks/Services/TracksDataLoader.cs` (`LoadTracksAsync`)

Cette tâche est de l'instrumentation pure (pas de test unitaire : `LoadTracksAsync` construit des `TrackViewModel` réels via le conteneur DI). Vérification par build.

- [ ] **Step 1: Envelopper la création des VM dans un PerfLogger imbriqué**

Dans `TracksDataLoader.cs`, remplacer la méthode `LoadTracksAsync` (ligne 14-21) par :

```csharp
    public async Task LoadTracksAsync()
    {
        using (PerfLogger perfLogger = new PerfLogger(logger).Parameters("Tracks loaded"))
        {
            IEnumerable<TrackDto> tracks = await mediator.Send(new GetAllTracksRequest());

            List<TrackDto> trackList = tracks.ToList();

            using (new PerfLogger(logger).Parameters($"Tracks: VM create ({trackList.Count})"))
            {
                ViewModels = TrackViewModelMap.CreateViewModels(trackList, trackViewModelFactory);
            }
        }
    }
```

Note : `tracks` arrive déjà matérialisé du handler (Task 1) ; le `ToList()` est ici un no-op de coût négligeable qui fige le nombre `trackList.Count` pour le label. Le timer `Tracks loaded` reste le total de référence.

- [ ] **Step 2: Build pour vérifier la compilation**

Run: `dotnet build src/Presentation/Rok.csproj /p:Platform=x64 --no-restore -v quiet`
Expected: build réussi, zéro warning (TreatWarningsAsErrors).

- [ ] **Step 3: Commit**

```bash
git add src/Presentation/ViewModels/Tracks/Services/TracksDataLoader.cs
git commit -m "perf(tracks): instrument viewmodel creation during track load"
```

---

## Task 3: Instrumenter FilterAndSort au premier chargement

**Files:**
- Modify: `src/Presentation/ViewModels/Tracks/TracksViewModel.cs` (`LoadDataAsync`)

Instrumentation pure. On enveloppe uniquement l'appel `FilterAndSort()` du chemin de premier chargement (`LoadDataAsync`), pas la méthode `FilterAndSort` elle-même (aussi appelée par les commandes filtre/recherche). `PerfLogger` et `ILogger` sont déjà en scope dans Presentation via les global usings (`Rok.Shared`, `Microsoft.Extensions.Logging`) — aucun `using` à ajouter.

- [ ] **Step 1: Envelopper l'appel FilterAndSort dans LoadDataAsync**

Dans `TracksViewModel.cs`, dans `LoadDataAsync` (ligne 80-98), remplacer la dernière ligne `FilterAndSort();` (ligne 97) par :

```csharp
        using (new PerfLogger(_logger).Parameters("Tracks: FilterAndSort"))
        {
            FilterAndSort();
        }
```

- [ ] **Step 2: Build pour vérifier la compilation**

Run: `dotnet build src/Presentation/Rok.csproj /p:Platform=x64 --no-restore -v quiet`
Expected: build réussi, zéro warning.

- [ ] **Step 3: Commit**

```bash
git add src/Presentation/ViewModels/Tracks/TracksViewModel.cs
git commit -m "perf(tracks): instrument FilterAndSort during first load"
```

---

## Task 4: Vérification complète et campagne de mesure

**Files:** aucun (build, tests, exécution manuelle).

- [ ] **Step 1: Build complet**

Run: `dotnet build /p:Platform=x64`
Expected: build réussi, zéro warning sur toute la solution.

- [ ] **Step 2: Tous les tests**

Run: `dotnet test /p:Platform=x64`
Expected: tous les tests passent (aucune régression).

- [ ] **Step 3: Lancer l'application et déclencher le premier chargement**

Lancer Rok (depuis Visual Studio ou l'app déployée), puis naviguer **une fois** vers la page Titres après le démarrage. Fermer ensuite l'application.

- [ ] **Step 4: Relever les mesures dans le log**

Ouvrir le fichier log du jour :
`%LOCALAPPDATA%\Packages\<package-Rok>\LocalState\Logs\rok-<yyyyMMdd>.log`

Y chercher les lignes contenant `Tracks:` et `Tracks loaded`. On attend cinq valeurs :

- `Tracks: DB fetch -> Nms`
- `Tracks: DTO map -> Nms`
- `Tracks: VM create (N) -> Nms`
- `Tracks: FilterAndSort -> Nms`
- `Tracks loaded -> Nms` (total de référence)

Reporter ces cinq valeurs + N (nombre de titres). Elles déterminent la sous-étape dominante et donc l'optimisation ciblée du spec de suivi (voir l'arbre de décision du spec de design).

- [ ] **Step 5: Confirmation finale**

Vérifier que les cinq lignes sont présentes et que `DB fetch + DTO map + VM create + FilterAndSort` est cohérent avec `Tracks loaded` (à l'overhead près). Le chantier de mesure est alors terminé ; la décision d'optimisation fait l'objet d'un nouveau cycle brainstorming → spec → plan.

---

## Notes de fin de chantier

Une fois les mesures relevées et l'optimisation décidée, considérer si les régions `PerfLogger` doivent rester (utiles pour suivre l'impact des futures optimisations) ou être retirées/abaissées en niveau Debug. À trancher dans le spec de suivi, pas ici.
