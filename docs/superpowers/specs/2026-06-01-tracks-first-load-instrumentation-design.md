# Instrumentation du premier chargement des Titres

**Date** : 2026-06-01
**Branche** : `perf/tracks-first-load-instrumentation`
**Statut** : design validé, prêt pour plan d'implémentation

## Contexte

L'utilisateur signale une lenteur de chargement des données, ciblée à la page **Titres** lors du **premier affichage après lancement** (les navigations suivantes sont rapides : `TracksViewModel` est singleton et met les données en cache). Bibliothèque réelle : **10 000 à 50 000 titres**.

Le chemin du premier chargement (`TracksPage.OnNavigatedTo` → `TracksViewModel.LoadDataAsync(forceReload: false)`) enchaîne quatre sous-étapes dont le poids relatif est inconnu :

1. Requête SQL `GetAllTracksRequest` (`GROUP_CONCAT(tags)` + 5 `LEFT JOIN` + `GROUP BY`) et matérialisation Dapper.
2. Mapping entités → DTO.
3. Création de N `TrackViewModel` via `TrackViewModelMap.CreateViewModels` — chaque VM est résolu par le conteneur DI (11 dépendances) **et s'abonne au messenger** (`_messenger.Subscribe<TrackScoreUpdateMessage>`), soit potentiellement 50 000 abonnements conservés.
4. `FilterAndSort()` (filtrage + regroupement) sur le thread UI.

## Objectif

Attribuer le temps mur du premier `LoadDataAsync` des Titres entre ces quatre sous-étapes, sur la vraie bibliothèque, **avant** d'engager toute optimisation lourde. Principe : mesurer avant d'optimiser.

Cette phase ne produit **aucune** optimisation spéculative. Elle produit des mesures et un fix « gratuit » nécessaire à une mesure propre.

## Défaut concret identifié

`TrackViewModelMap.CreateViewModels` :

```csharp
int capacity = tracks.Count();   // 1re énumération
foreach (TrackDto track in tracks) { ... }   // 2e énumération
```

`tracks` est l'`IEnumerable<TrackDto>` paresseux retourné par `GetAllTracksRequestHandler` (`tracks.Select(a => TrackDtoMapping.Map(a))`). Les deux énumérations déclenchent donc le mapping **deux fois** sur l'ensemble de la collection.

## Périmètre

### Inclus

Instrumentation via `PerfLogger` (utilitaire existant, log `LogInformation` à la libération) sur quatre régions :

| Région                  | Emplacement                                                         | Ce qu'elle mesure                                  |
|-------------------------|---------------------------------------------------------------------|----------------------------------------------------|
| `Tracks: DB fetch`      | `GetAllTracksRequestHandler.Handle`, autour de `GetAllAsync()`      | requête SQL + matérialisation Dapper               |
| `Tracks: DTO map`       | même handler, matérialisation du `Select(Map)` en `List` une fois   | coût réel du mapping (corrige le double-mapping)   |
| `Tracks: VM create (N)` | `TrackViewModelMap.CreateViewModels`, autour de la boucle           | N × (résolution DI + abonnement messenger)         |
| `Tracks: FilterAndSort` | `TracksViewModel.LoadDataAsync`, autour de `FilterAndSort()`        | filtrage + regroupement sur thread UI              |

Le timer global existant `"Tracks loaded"` (dans `TracksDataLoader` / provider) reste en place comme total de référence. Le nombre N de titres est inclus dans le label de la région `VM create`.

### Fix appliqué dès cette phase

Matérialiser le mapping DTO **une seule fois** dans `GetAllTracksRequestHandler.Handle` (retourner une collection déjà matérialisée). Effet double :
- supprime le double-mapping ;
- rend la région `DTO map` mesurable indépendamment de `VM create`.

Après ce fix, le `tracks.Count()` de `CreateViewModels` opère sur une `List` déjà matérialisée (O(n) sans re-mapping).

### Exclu

- Pages **Albums** et **Artistes** (même schéma de chargement ; les leçons y seront appliquées dans un chantier ultérieur).
- **Pagination / virtualisation** de la création des VM (approche B écartée : conflit avec le regroupement qui exige tous les items).
- Coût de **re-binding à la navigation** (symptôme distinct ; déprioritisé car la lenteur ressentie est surtout au premier chargement).
- Toute optimisation lourde (index SQL, modèle de ligne léger, dispatch messenger partagé) : décidée au vu des mesures, dans un spec de suivi.

## Lecture des résultats

Après un lancement de l'application suivi d'une navigation vers la page Titres, les cinq lignes (`DB fetch`, `DTO map`, `VM create (N)`, `FilterAndSort`, `Tracks loaded`) apparaissent dans les logs Serilog. Le sink fichier sera localisé dans `App.xaml.cs` ; les valeurs seront lues depuis ce fichier, ou fournies par l'utilisateur.

## Arbre de décision (pilote le spec de suivi)

| Sous-étape dominante | Optimisation envisagée |
|----------------------|------------------------|
| **VM create**        | (1) remplacer l'abonnement messenger par item par un dispatcher partagé unique ; (2) si encore lourd, modèle de ligne léger (record d'affichage immuable, `TrackViewModel` complet créé à la demande). |
| **DB fetch**         | index couvrant sur les clés étrangères de `tracks` ; projection liste allégée évitant `GROUP_CONCAT`. |
| **DTO map**          | le fix double-mapping suffit probablement ; sinon réglage record/struct du mapping. |
| **FilterAndSort**    | déplacer le regroupement hors du thread UI ; optimiser l'algorithme de regroupement. |

## Critères de succès

- Les cinq mesures sont émises dans les logs lors du premier chargement des Titres, avec N affiché.
- Le mapping DTO ne s'exécute plus qu'une fois (vérifiable : `DTO map` < ancien comportement, et `CreateViewModels` ne re-mappe plus).
- Le build reste *warning-free* (`TreatWarningsAsErrors`) et tous les tests passent.
- Aucune régression de comportement fonctionnel de la page Titres.
