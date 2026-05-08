# Design — Enrichissement API lors de l'import

**Date :** 2026-05-08  
**Branche :** bump-1.11.0  
**Statut :** approuvé

## Contexte

Lors de l'import de la bibliothèque musicale, les entités `Artist` et `Album` sont créées en base sans image ni métadonnées enrichies. L'enrichissement (images, biographie, liens, MusicBrainzID…) n'est déclenché que quand l'utilisateur ouvre la fiche correspondante en Presentation, via `ArtistApiService` / `AlbumApiService`.

L'objectif est de pré-charger les données pour les premières entités créées lors d'un run d'import, sans ralentir l'import et sans surcharger l'API.

## Objectifs

- Appeler `MusicDataApiService` pour les artistes et albums **nouvellement créés** lors d'un import
- Limiter à **100 artistes** et **100 albums** par run (constantes modifiables)
- Ne pas impacter le pipeline d'import (appels hors du thread d'import principal)
- Respecter le rate limiting existant de `MusicDataApiService`
- Récupérer images + métadonnées complètes (même périmètre que `ArtistApiService`/`AlbumApiService`)
- Après enrichissement, la cooldown de 7 jours s'applique → pas de double appel API quand l'utilisateur ouvre la fiche

## Périmètre hors-scope

- Modification du pipeline d'import principal (`FolderImportProcessor`, `TrackImport`, etc.)
- Changement du comportement pour les artistes/albums déjà existants
- Nouveaux appels API lors des mises à jour de tracks (seules les créations sont concernées)

## Architecture

### Couche concernée

`Rok.Import` — tâche post-import, dans le même pattern que `PostImportDominantColorTask`.

La dépendance sur `ArtistApiService` / `AlbumApiService` (dans `Rok.Application`) est valide : `Rok.Import` référence déjà `Rok.Application`.

### Rate limiting — état actuel (aucune modification nécessaire)

`MusicDataApiService` gère déjà :
- `SemaphoreSlim(5)` — max 5 requêtes concurrentes ; la tâche post-import est séquentielle, donc 1 seule à la fois
- Réception 429 → `_ignoreRequestsUntil` positionné, tous les appels suivants retournent `null` immédiatement sans appel réseau
- `IsApiRetryAllowed(GetMetaDataLastAttempt)` → pour une entité nouvellement créée, `GetMetaDataLastAttempt == null`, donc toujours autorisé

## Composants modifiés / créés

### 1. `ArtistImport` (modification mineure)

Ajout d'un collecteur d'IDs plafonné :

```
+ private const int MaxArtistsApiEnrichment = 100
+ private readonly List<long> _newlyCreatedIds = new()
+ public IReadOnlyList<long> NewlyCreatedIds => _newlyCreatedIds

~ LoadCacheAsync() : vide _newlyCreatedIds en début de run
~ CreateAsync()   : si _newlyCreatedIds.Count < MaxArtistsApiEnrichment → ajoute l'ID
```

### 2. `AlbumImport` (modification mineure)

Même pattern :

```
+ private const int MaxAlbumsApiEnrichment = 100
+ private readonly List<long> _newlyCreatedIds = new()
+ public IReadOnlyList<long> NewlyCreatedIds => _newlyCreatedIds

~ LoadCacheAsync() : vide _newlyCreatedIds en début de run
~ CreateAsync()   : si _newlyCreatedIds.Count < MaxAlbumsApiEnrichment → ajoute l'ID
```

### 3. `PostImportApiEnrichmentTask` (nouveau)

**Localisation :** `src/Rok.Import/Services/PostImportApiEnrichmentTask.cs`

**Dépendances injectées :**

| Dépendance | Usage |
|---|---|
| `ArtistImport` | Lire `NewlyCreatedIds` |
| `AlbumImport` | Lire `NewlyCreatedIds` |
| `ArtistApiService` | Enrichissement artiste (images + métadonnées) |
| `AlbumApiService` | Enrichissement album (images + métadonnées) |
| `IArtistPictureService` | Passé en paramètre à `ArtistApiService` |
| `IAlbumPictureService` | Passé en paramètre à `AlbumApiService` |
| `IBackdropPicture` | Passé en paramètre à `ArtistApiService` |
| `IMediator` | Charger `ArtistDto` / `AlbumDto` par ID |
| `ILogger<PostImportApiEnrichmentTask>` | Logging |

**Algorithme `RunAsync(CancellationToken)` :**

```
Pour chaque artistId dans ArtistImport.NewlyCreatedIds :
  si cancellationToken annulé → arrêt
  ArtistDto? artist = GetArtistByIdQuery(artistId) via mediator
  si null → skip (log warning)
  ArtistApiService.GetAndUpdateArtistDataAsync(artist, pictureService, backdropPicture)

Pour chaque albumId dans AlbumImport.NewlyCreatedIds :
  si cancellationToken annulé → arrêt
  AlbumDto? album = GetAlbumByIdQuery(albumId) via mediator
  si null → skip (log warning)
  AlbumApiService.GetAndUpdateAlbumDataAsync(album, albumPictureService)

Log résumé : N artistes enrichis, M albums enrichis
```

Les erreurs par entité sont absorbées (log + continue), pour ne pas interrompre le reste du traitement.

### 4. `ImportService` (modification)

Dans `ImportAsync()`, après `PostImportDominantColorTask` :

```csharp
if (!errorOccurred)
    await _postImportDominantColorTask.RunAsync(cancellationToken);

if (!errorOccurred)
    await _postImportApiEnrichmentTask.RunAsync(cancellationToken);
```

### 5. `Rok.Import.DependencyInjection` (modification)

```csharp
services.AddTransient<PostImportApiEnrichmentTask>();
```

`ArtistApiService` et `AlbumApiService` sont déjà enregistrés en `AddTransient` dans `Rok.Application.DependencyInjection` — aucune modification nécessaire.

`PostImportApiEnrichmentTask` doit être `AddTransient` (et non `AddSingleton`) pour éviter la captive dependency : il injecte `ArtistApiService`/`AlbumApiService` qui sont `Transient`. `ImportService` étant `AddScoped`, il peut sans problème résoudre un `Transient`.

## Flux de données

```
ImportAsync()
  └─ FolderImportProcessor (inchangé)
       └─ ArtistImport.CreateAsync()  → collecte ID si < 100
       └─ AlbumImport.CreateAsync()   → collecte ID si < 100
  └─ PostImportDominantColorTask.RunAsync()  (inchangé)
  └─ PostImportApiEnrichmentTask.RunAsync()  ← nouveau
       └─ GetArtistByIdQuery × N  →  ArtistApiService  →  MusicDataApiService
       └─ GetAlbumByIdQuery × M   →  AlbumApiService   →  MusicDataApiService
```

## Gestion des erreurs

| Cas | Comportement |
|---|---|
| `MusicDataApiService` désactivé (`IsEnable = false`) | `ArtistApiService` retourne `None` silencieusement |
| Rate limit 429 | `_ignoreRequestsUntil` positionné, appels suivants retournent null |
| Artiste/album introuvable en base (GetById) | Log warning, skip |
| Exception réseau sur un artiste | Log error, continue avec l'artiste suivant |
| Annulation pendant la tâche | Arrêt propre via `CancellationToken` |

## Tests

- `ArtistImport` : vérifier que `NewlyCreatedIds` se remplit jusqu'à 100 puis s'arrête, et est vidé à `LoadCacheAsync()`
- `AlbumImport` : même vérification
- `PostImportApiEnrichmentTask` : mock `ArtistApiService`/`AlbumApiService`, vérifier que :
  - le nombre d'appels correspond aux IDs collectés
  - une exception sur un artiste n'interrompt pas les albums
  - `CancellationToken` arrête proprement la boucle
