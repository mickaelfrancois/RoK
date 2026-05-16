# Migration MiF.Mediator → CleanArch.DevKit.Mediator (+ Validation)

**Date** : 2026-05-15
**Branche** : `refactor/migrate-to-cleanarch-mediator`
**Étape** : 1 / 3 d'un plan global de remplacement de la stack MiF par CleanArch.DevKit.

## Contexte

Rok utilise actuellement trois paquets MiF côté Application/Présentation :

| Paquet MiF | Rôle | Étape de migration |
|------------|------|--------------------|
| `MiF.Mediator` 1.2.0 | Dispatch CQRS injecté + validation DataAnnotations automatique + pipeline IRequestPreProcessor | **Étape 1 (ce spec)** |
| `MiF.Result` 1.1.0 | Type `Result`/`Result<T>` retourné par les handlers | Étape 2 |
| `MiF.Messenger` 1.0.0 + `MiF.SimpleMessenger` | Bus pub/sub statique côté UI | Étape 3 |

Inspection des sources MiF (`D:/Development/MF/MF.Simple.Mediator/`) révèle trois fonctionnalités embarquées :

1. **Dispatch CQRS** (`IMediator.SendMessageAsync`) — Scoped, sans réflexion sur le chemin chaud (mais avec `MakeGenericType` à la première résolution).
2. **Validation automatique** (`IValidationService` + `ValidationService`) — Mediator appelle `_validationService?.Validate(message)` avant chaque handler, exécutant les `[Required]`, `[RequiredGreaterThanZero]`, etc. présents sur les classes Command/Query. Rok exploite cette fonctionnalité sur **35 fichiers** (sources de l'`Application`).
3. **Pipeline IRequestPreProcessor** — Rok l'utilise via `QueryPreProcessor<TMessage, TResponse>` (logging générique avant chaque handler).

CleanArch.DevKit.Mediator couvre (1) et (3) mais **pas (2)** : pas de validation DataAnnotations automatique. Le remplacement passe par le paquet séparé `CleanArch.DevKit.Mediator.Validation` (style FluentValidation, rule builders fluides, générateur Roslyn pour la découverte des `Validator<T>`).

Décision projet : **adopter `Mediator.Validation` dans cette étape**. Plus de boilerplate qu'un simple comportement DataAnnotations reproduit localement, mais alignement durable avec l'écosystème CleanArch et capacités plus expressives (règles conditionnelles, messages personnalisés, validators imbriqués).

## Périmètre

### Modifications atomiques (PR unique)

**Couche cible — `src/Rok.Application/`**
- 67 fichiers handlers sous `Features/<Area>/Command|Query/*.cs` : renommages d'interfaces, méthodes, suffixes de classes, et déplacement vers `Features/<Area>/Requests/*.cs`
- `Features/QueryPreProcessor.cs` : migration `IRequestPreProcessor` → `IPipelineBehavior` (renommage suggéré : `LoggingPipelineBehavior.cs`, déplacement vers `Pipeline/`)
- `DependencyInjection.cs` : retrait `AddScoped<IValidationService, ValidationService>()` + `AddSimpleMediator()` ; ajout `AddMediator()`, `AddValidators()`, `AddValidationBehavior()`
- `GlobalUsings.cs` : `MiF.Mediator.Interfaces` → `CleanArch.DevKit.Mediator` + ajout `CleanArch.DevKit.Mediator.Validation` ; retrait `System.ComponentModel.DataAnnotations` et `Rok.Shared.ValidationAttributes` (plus utilisés)
- `Rok.Application.csproj` : retrait `MiF.Mediator` ; ajout `CleanArch.DevKit.Mediator` + `CleanArch.DevKit.Mediator.Validation`
- **Nouveau fichier** : `Mediator.cs` portant la `public partial class Mediator { }`
- **35 nouveaux validators** : un `<RequestName>Validator : Validator<TRequest>` par classe ayant actuellement des attributs DataAnnotations, colocalisé dans le fichier `*RequestHandler.cs` correspondant (3 types par fichier : Request + Handler + Validator)

**Couche `src/Rok.Shared/`**
- `ValidationAttributes/RequiredGreaterThanZero.cs` : **suppression** (devient orphelin après migration des validators)
- `tests/UnitTests/Rok.ApplicationTests/Shared/ValidationAttributes/RequiredGreaterThanZeroTests.cs` : suppression (test du code supprimé)

**Couches consommatrices**
- `src/Presentation/` : ~25 sites d'injection `IMediator` + appels `_mediator.SendMessageAsync(...)` ; `GlobalUsings.cs` ; 1 fichier (`PlaylistExportService.cs`) avec using nominatif
- `src/Rok.Import/` : `PostImportApiEnrichmentTask.cs` (using nominatif)

**Tests** — ~40 fichiers
- `tests/UnitTests/Rok.ApplicationTests/GlobalUsings.cs` + ~10 fichiers individuels
- `tests/UnitTests/Rok.ImportTests/Services/PostImportApiEnrichmentTaskTests.cs`
- `tests/UnitTests/Rok.PresentationTests/` : ~30 fichiers (mocks `IMediator.SendMessageAsync` → `IMediator.Send`)
- Suppression de `Shared/ValidationAttributes/RequiredGreaterThanZeroTests.cs`

### Hors périmètre — préservé tel quel

- `MiF.Result` 1.1.0 — étape 2
- `MiF.Messenger` 1.0.0 et `MiF.SimpleMessenger` (statique) — étape 3
- `MiF.Guard`, `MiF.RelayCommand` — pas dans le plan de remplacement
- Aucun changement de comportement runtime sur les handlers eux-mêmes (logique métier inchangée)
- Pas d'introduction de notifications (`INotification`) — Rok n'en utilise pas
- Pas d'introduction de stream requests — Rok n'en utilise pas
- Pas d'introduction du paquet `Mediator.Behaviors` (Logging/Performance/UnhandledException prêts à l'emploi) — le `LoggingPipelineBehavior` local équivalent est conservé

## Faits vérifiés sur les libs

### MiF.Mediator 1.2.0

```csharp
// D:/Development/MF/MF.Simple.Mediator/MF.Mediator/Interfaces/IMediator.cs
public interface IMediator
{
    Task<TResponse> SendMessageAsync<TResponse>(IRequest<TResponse> message, CancellationToken cancellationToken = default);
}
```

- Une seule surcharge, `CancellationToken` optionnel
- `Mediator.SendMessageAsync` appelle `_validationService?.Validate(message)` AVANT d'invoquer le handler (validation automatique)
- DI : `services.TryAddScoped<IMediator, Mediator>()` + `TryAddScoped<IServiceFactory>` + auto-scan des assemblies non-système
- Aucun analyseur Roslyn embarqué (`.csproj` ne référence que `Microsoft.Extensions.DependencyInjection`)
- `Unit.Result` est un getter qui retourne une nouvelle instance (`public static Unit Result => new();`)

### CleanArch.DevKit.Mediator 0.1.0

```csharp
// D:/Development/CleanArch.DevKit/src/Mediator.Core/Abstractions/IMediator.cs
public interface IMediator
{
    Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);
    Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification;
    IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default);
}
```

- DI émis par le générateur (cf. `MediatorRegistrationWriter.cs`) :
  - `IMediator` → **Scoped** (équivalent MiF — pas de régression de lifetime)
  - Handlers → Transient
- `Unit.Value` est un `readonly record struct` (vs `sealed class` chez MiF — pas de pression GC)
- Analyseurs Roslyn embarqués : `MED001` (handler manquant), `MED002` (multiples handlers), `MED003` (partial class Mediator manquante) — filet de sécurité à la compilation
- Pas de validation automatique intégrée : nécessite `Mediator.Validation`

## Renommages

### A — API mediator (mécanique)

| Avant (MiF) | Après (CleanArch.DevKit) |
|---|---|
| `using MiF.Mediator;` | `using CleanArch.DevKit.Mediator;` |
| `using MiF.Mediator.Interfaces;` | `using CleanArch.DevKit.Mediator;` |
| `using MiF.Mediator.DependencyInjection;` | (à retirer — `AddMediator` est en top-level extension) |
| `services.AddSimpleMediator()` | `services.AddMediator()` |
| `services.AddScoped<IValidationService, ValidationService>()` | (retirer — remplacé par `AddValidators()` + `AddValidationBehavior()`) |
| `: ICommand<TResponse>` / `: IQuery<TResponse>` | `: IRequest<TResponse>` |
| `: ICommand` (sans T) | `: IRequest` (≡ `IRequest<Unit>`) |
| `: ICommandHandler<TCmd, TRes>` / `: IQueryHandler<TQry, TRes>` | `: IRequestHandler<TReq, TRes>` |
| `Task<TRes> HandleAsync(req, ct)` | `Task<TRes> Handle(req, ct)` |
| `_mediator.SendMessageAsync(req, ct)` | `_mediator.Send(req, ct)` |
| `return Unit.Result;` | `return Unit.Value;` |

### B — Renommages CQRS Command/Query → Request

Pour aligner sur la terminologie unifiée `IRequest` de la lib, les suffixes de classes et les noms de dossiers sont renommés.

**Suffixes de classes**
- `*Command` → `*Request`
- `*Query` → `*Request`
- `*CommandHandler` → `*RequestHandler`
- `*QueryHandler` → `*RequestHandler`

**Exemples**
- `CreatePlaylistCommand` → `CreatePlaylistRequest`
- `CreatePlaylistCommandHandler` → `CreatePlaylistRequestHandler`
- `GetAlbumByIdQuery` → `GetAlbumByIdRequest`
- `GetAlbumByIdQueryHandler` → `GetAlbumByIdRequestHandler`

**Dossiers**

```
Avant :                                  Après :
src/Rok.Application/Features/Albums/     src/Rok.Application/Features/Albums/
├── Command/                             ├── Requests/
│   ├── UpdateAlbumCommandHandler.cs     │   ├── UpdateAlbumRequestHandler.cs
│   └── ...                              │   ├── GetAlbumByIdRequestHandler.cs
└── Query/                               │   └── ...
    ├── GetAlbumByIdQueryHandler.cs      └── Services/
    └── ...
```

**Tests** : mêmes renommages côté `tests/UnitTests/Rok.ApplicationTests/Features/<Area>/`.

**Mode édition** : `git mv` pour chaque renommage de fichier afin de préserver l'historique par fichier.

### C — Pipeline behavior

Le `QueryPreProcessor` actuel implémente le pattern MiF `IRequestPreProcessor<TMessage, TResponse>`. Migration vers `IPipelineBehavior<TRequest, TResponse>` :

| Avant (MiF) | Après (CleanArch.DevKit) |
|---|---|
| `IRequestPreProcessor<TMessage, TResponse>` | `IPipelineBehavior<TRequest, TResponse>` |
| `RunAsync(message, next, ct)` | `Handle(request, next, ct)` |
| `HandleRequestDelegate<TMessage, TResponse>` (signature `(message, ct) => Task<TResponse>`) | `RequestHandlerDelegate<TResponse>` (signature `(ct) => Task<TResponse>` — le request est capturé par closure) |
| Auto-enregistré par MiF via réflexion (`AddSimpleMediatorPreProcessor`) | À enregistrer explicitement : `services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingPipelineBehavior<,>))` |

**Décisions de placement** :
- Fichier renommé `QueryPreProcessor.cs` → `LoggingPipelineBehavior.cs`
- Déplacé de `Features/` (mal placé, c'est un cross-cutting) vers nouveau dossier `Pipeline/`
- Le `next.Invoke(message, cancellationToken)` devient `next.Invoke(cancellationToken)` (request est dans la closure)

### D — Validators (35 nouveaux)

Pour chaque fichier `*RequestHandler.cs` ayant actuellement des attributs DataAnnotations :

1. Supprimer les attributs (`[Required]`, `[RequiredGreaterThanZero]`) sur les propriétés de la classe Request
2. Ajouter une classe `<RequestName>Validator : Validator<TRequest>` colocalisée dans le même fichier (3 types par fichier : Request + Handler + Validator)
3. Traduire chaque attribut en règle fluide via le constructeur du validator

**Table de correspondance des attributs**

| Avant (DataAnnotations) | Après (Validator) | Notes |
|---|---|---|
| `[Required]` sur `string` | `Rule(x => x.Field).Required()` | Sémantique légèrement plus stricte chez CleanArch : `!string.IsNullOrWhiteSpace(v)` (whitespace-only refusé). DataAnnotations `[Required]` rejette seulement null/empty. À considérer comme un correctif souhaitable. |
| `[Required]` sur `T?` reference type | `Rule(x => x.Field).NotNull()` | |
| `[Required]` sur `enum` | (à arbitrer par validator) | Le `[Required]` MiF était quasi no-op sur enum (valeur par défaut acceptée) — selon le besoin métier, soit pas de règle, soit `NotEqual(default(TEnum))` |
| `[Required]` sur `T[]` | `Rule(x => x.Field).NotNull()` | |
| `[RequiredGreaterThanZero]` sur `long`/`int` | `Rule(x => x.Id).GreaterThan(0L)` (ou `0`) | Mapping exact |

**Exemple complet**

```csharp
// Avant (fichier GetTrackByIdQueryHandler.cs) :
public class GetTrackByIdQuery(long id) : IQuery<Result<TrackDto>>
{
    [RequiredGreaterThanZero]
    public long Id { get; } = id;
}

public class GetTrackByIdQueryHandler(ITrackRepository repo)
    : IQueryHandler<GetTrackByIdQuery, Result<TrackDto>>
{
    public async Task<Result<TrackDto>> HandleAsync(...) { ... }
}

// Après (fichier GetTrackByIdRequestHandler.cs) :
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

public class GetTrackByIdRequestHandler(ITrackRepository repo)
    : IRequestHandler<GetTrackByIdRequest, Result<TrackDto>>
{
    public async Task<Result<TrackDto>> Handle(...) { ... }
}
```

**Sémantique enum** : les attributs `[Required]` sur enum dans Rok (`SaveEqualizerPresetCommand.Scope`, `DeleteEqualizerPresetCommand.Scope`, etc.) sont **inertes** côté MiF (un enum a toujours une valeur). Décision par défaut : pas de règle ajoutée pour ces propriétés. Si une logique métier exige une valeur explicite, le validator correspondant ajoutera `NotEqual(default(...))` au moment de l'écriture.

## Nouveaux fichiers (synthèse)

| Fichier | Rôle |
|---|---|
| `src/Rok.Application/Mediator.cs` | `public partial class Mediator { }` — déclencheur du générateur Roslyn |
| `src/Rok.Application/Pipeline/LoggingPipelineBehavior.cs` | Behavior générique de logging (équivalent `QueryPreProcessor`) |
| `src/Rok.Application/Features/<Area>/Requests/<Name>RequestHandler.cs` | 67 fichiers de handlers (renommés via `git mv`) ; 35 d'entre eux contiennent un `<Name>RequestValidator` colocalisé |

## Fichiers supprimés

| Fichier | Motif |
|---|---|
| `src/Rok.Application/Features/QueryPreProcessor.cs` | Remplacé par `Pipeline/LoggingPipelineBehavior.cs` (`git mv`) |
| `src/Rok.Shared/ValidationAttributes/RequiredGreaterThanZero.cs` | Orphelin après migration des validators |
| `tests/UnitTests/Rok.ApplicationTests/Shared/ValidationAttributes/RequiredGreaterThanZeroTests.cs` | Test du code supprimé |

`src/Rok.Shared/ValidationAttributes/` devient vide — supprimer le dossier également.

## Acquisition de la dépendance

Décision : **packages publiés sur nuget.org**.

1. Côté CleanArch.DevKit : `./scripts/publish.ps1 -Version <X.Y.Z>` (push réel après confirmation)
2. Côté Rok : ajouter dans `Rok.Application.csproj`
   ```xml
   <PackageReference Include="CleanArch.DevKit.Mediator" Version="<X.Y.Z>" />
   <PackageReference Include="CleanArch.DevKit.Mediator.Validation" Version="<X.Y.Z>" />
   ```
3. Aucun `NuGet.config` ajouté à Rok — feed nuget.org par défaut

## Lifetime DI

Équivalence stricte avec MiF :

| Service | MiF.Mediator | CleanArch.DevKit.Mediator |
|---|---|---|
| `IMediator` | Scoped (`TryAddScoped`) | Scoped (`AddScoped`) |
| Handlers | Scoped (via réflexion) | Transient |
| Pipeline behaviors | Scoped (via réflexion) | Transient |
| Validators (nouveau) | n/a | Transient (par défaut du générateur) |

Pas de changement de comportement runtime pour `IMediator`. Les Singletons existants qui injectent `IMediator` (`PlaylistService`, `PlayerService`, `ReviewPromptEligibilityService`, `IPlayerSleepModeService`) continuent de fonctionner sans modification — Rok résout depuis le root `ServiceProvider` unique (cf. `App.xaml.cs:190`) sans `validateScopes:true`, ce qui rend la capture de Scoped depuis Singleton inoffensive en pratique.

**Vérification d'implémentation** : au PR, `grep -n "AddSingleton.*IMediator\|AddSingleton<.*Service" src/Rok.Application/DependencyInjection.cs src/Presentation/DependencyInjection.cs` pour confirmer qu'aucun nouveau couplage Singleton→Scoped n'a été introduit.

## Risques et contremesures

| Risque | Surface | Contremesure |
|---|---|---|
| Régression silencieuse de la validation (un validator oublié = entrée acceptée alors qu'elle était refusée) | 35 fichiers avec validators | Tests unitaires des validators à ajouter dans `Rok.ApplicationTests/Features/<Area>/Validators/` au minimum sur les cas négatifs (1 test "throws on invalid input" par validator) |
| Mocks Moq cassent (renommage `SendMessageAsync` → `Send<TResponse>`) | ~40 fichiers tests | Recherche/remplacement contrôlée puis `dotnet test` |
| Sémantique `[Required]` sur enum mal interprétée | 4 fichiers (SaveEqualizerPreset, DeleteEqualizerPreset, ListeningEvent, UpdatePlaylist) | Décision documentée par fichier : pas de règle si le défaut MiF était inerte ; `NotEqual(default)` si le code métier en dépend |
| `[Required]` sur reference nullable non détecté lors du remplacement | 12 fichiers `[Required]` sur strings | Audit final : grep `[Required]` dans `src/Rok.Application/` doit retourner 0 résultat |
| Test du `RequiredGreaterThanZero` non supprimé après retrait du code | 1 fichier | Inclure la suppression du test dans le diff initial |
| Smoke test UI absent côté CI | toute la couche XAML/ViewModels | Smoke test manuel obligatoire avant merge |
| `git mv` mal trackés (Renommage perçu comme suppression+ajout) | 67 fichiers handlers + ~30 tests | Utiliser `git mv` explicite, vérifier `git status` montre des "renamed" |

## Critères d'acceptation

1. `dotnet build /p:Platform=x64` vert (treat-warnings-as-errors actif)
2. `dotnet test /p:Platform=x64` vert sur Application, Import, Presentation tests
3. `grep -rn "MiF.Mediator\|MiF\\.Mediator" src/ tests/` retourne 0 occurrence (hors fichiers `.md` du dossier `docs/`)
4. `grep -rn "ICommand<\|IQuery<\|ICommandHandler\|IQueryHandler\|HandleAsync\|SendMessageAsync\|Unit\\.Result\|IRequestPreProcessor" src/ tests/` retourne 0 occurrence (hors `System.Windows.Input.ICommand` côté XAML — faux positif à exclure)
5. `grep -rn "\\[Required\\]\|\\[RequiredGreaterThanZero\\]" src/Rok.Application/` retourne 0 occurrence
6. `src/Rok.Shared/ValidationAttributes/` n'existe plus
7. `Rok.Application.csproj` ne contient plus `MiF.Mediator` ; contient `CleanArch.DevKit.Mediator` et `CleanArch.DevKit.Mediator.Validation`
8. Le diagnostic MED003 (pas de `partial class Mediator`) ne se déclenche pas — la classe est bien présente
9. Chaque ancienne classe `*Command` / `*Query` ayant des DataAnnotations a un `*RequestValidator` correspondant — vérifiable par `grep -l "class.*Request.*Validator.*Validator<" src/Rok.Application/Features`
10. Au moins un test négatif par validator (échec attendu sur entrée invalide) — vérifiable par count des fichiers de test
11. Smoke test manuel WinUI : démarrage app sans crash, navigation Album/Artist OK, création d'une playlist persiste en base, lancement d'un import library exécute les handlers, saisie d'une valeur invalide (par exemple Id=0) déclenche bien une `ValidationException`
12. Commit suit Conventional Commits, format `refactor(app): migrate from MiF.Mediator to CleanArch.DevKit.Mediator + Validation`

## Séquence d'exécution (haut niveau)

Le plan d'implémentation détaillé sera produit en sortie de cette étape de design. Vue d'ensemble :

1. Publier `CleanArch.DevKit.Mediator` et `CleanArch.DevKit.Mediator.Validation` sur nuget.org (côté repo CleanArch.DevKit)
2. Sur la branche `refactor/migrate-to-cleanarch-mediator` :
   - Bump `Rok.Application.csproj` (retrait MiF.Mediator + ajout des deux paquets CleanArch.DevKit)
   - Créer `Mediator.cs` + `Pipeline/LoggingPipelineBehavior.cs`
   - Migrer `QueryPreProcessor` (renommage + déplacement + adaptation signature)
   - Pour chaque feature : `git mv` du dossier `Command/`/`Query/` → `Requests/`, renommages atomiques des suffixes, retrait des DataAnnotations, ajout du validator colocalisé
   - MAJ `DependencyInjection.cs`, `GlobalUsings.cs` (Application + Présentation + tests)
   - MAJ tous les mocks `IMediator` dans les tests
   - Suppression de `Rok.Shared/ValidationAttributes/` + son test
3. Audit lifetime DI (recherche d'introduction accidentelle de captive dependency)
4. `dotnet build /p:Platform=x64` itératif jusqu'à vert (les MED001-003 guident les ratés)
5. Ajout des tests de validators (1 cas négatif minimum par validator)
6. `dotnet test /p:Platform=x64` itératif jusqu'à vert
7. Smoke test manuel WinUI
8. Commit Conventional Commits

## Hors-scope explicite

- Pas d'introduction du paquet `CleanArch.DevKit.Mediator.Behaviors` (Logging/Performance/UnhandledException prêts) — peut être un follow-up
- Pas d'introduction de `Mediator.Results` (étape 2) ni de `Mediator.Domain.Bridge`
- Pas de notifications `INotification` ni de stream requests — Rok n'en utilise pas
- Pas de migration de `MiF.Result` ni de `MiF.SimpleMessenger`
- Pas de refactor des handlers eux-mêmes (logique métier intouchée)
