# Design — Import / Export de playlists

- **Date** : 2026-04-28
- **Statut** : design validé, prêt pour planification
- **Scope** : v1, format M3U8 uniquement

## Objectif

Permettre à l'utilisateur d'exporter une playlist Rok vers un fichier interopérable, et d'importer un fichier de playlist externe pour créer une playlist Rok. La v1 cible le format **M3U8** (UTF-8) uniquement ; PLS, XSPF et WPL sont repoussés à une itération ultérieure.

## Décisions verrouillées

| # | Décision |
|---|---|
| 1 | Bidirectionnel : import **et** export |
| 2 | Format v1 : **M3U8** seulement. La lecture accepte aussi l'extension `.m3u` ; l'écriture produit toujours `.m3u8` |
| 3 | **Aucune** pochette de playlist écrite/lue (M3U8 ne le supporte pas) |
| 4 | Matching à l'import : **chemin absolu strict, case-insensitive Windows**. Les pistes absentes de la BDD sont **ignorées** (pas une erreur) |
| 5 | Smart playlists à l'export : lecture des `PlaylistTrackEntity` persistés, **pas de régénération** des règles. Une `ContentDialog` d'avertissement est affichée côté VM |
| 6 | Contenu fichier exporté : chemin **absolu** + ligne `#EXTINF` **toujours** présente (durée + `Artist - Title`) |
| 7 | UI export : `MenuFlyoutItem "Exporter…"` sur l'item de `PlaylistsPage` + bouton sur `PlaylistPage`. Une playlist à la fois, `FileSavePicker`, extension par défaut `.m3u8` |
| 8 | UI import : un bouton "Importer…" sur `PlaylistsPage`, `FileOpenPicker` multi-sélection, **1 fichier = 1 playlist** créée |
| 9 | Conflit de nom à l'import : auto-suffixe `(2)`, `(3)`… (hard cap à 999, au-delà → erreur DB normale) |
| 10 | Feedback : toast récap unique en fin d'opération (`N importée(s) — X piste(s), Y ignorée(s) [+Z vide(s) ignorée(s)] [+F en échec]`) |
| 11 | Atomicité : **une transaction SQLite par playlist**. Erreur (parse / DB) → rollback de cette playlist, on continue avec les suivantes |
| 12 | Si **0 piste** ne matche dans un fichier → on **ne crée pas** de playlist vide, compteur `skipped` |
| 13 | Écriture export atomique : `.tmp` adjacent puis `File.Move` |
| 14 | Architecture : strategy pattern avec interfaces `IPlaylistFormatReader` / `IPlaylistFormatWriter`. Une seule implémentation (M3U8) en v1, l'ajout d'un format ultérieur = 1 reader + 1 writer + 1 ligne de DI |

## Architecture et arborescence

Découpage strictement aligné sur la Clean Architecture en place dans Rok.

```
Rok.Application/Features/Playlists/
  IO/
    IPlaylistFormatReader.cs
    IPlaylistFormatWriter.cs
    IPlaylistFormatResolver.cs
    PlaylistFileModel.cs
    ExportPlaylistFormat.cs              ← enum (M3u8 en v1)
  Command/
    ImportPlaylistCommandHandler.cs
    ExportPlaylistCommandHandler.cs
  PlaylistImportResult.cs                ← DTO retourné par l'import

Rok.Application/Features/Playlists/Messages/
  PlaylistImportedMessage.cs             ← messenger MiF, pour invalidation des listes

Rok.Infrastructure/Playlists/
  PlaylistFormatResolver.cs              ← extension → reader/writer via DI
  Formats/
    M3u8PlaylistReader.cs
    M3u8PlaylistWriter.cs

Rok.Infrastructure/Repositories/
  TrackRepository.cs                     ← + GetByFilePathAsync

Presentation/
  ViewModels/Playlists/Services/
    PlaylistImportService.cs             ← FilePicker multi + dispatch + toast
  ViewModels/Playlist/Services/
    PlaylistExportService.cs             ← dialog Smart + FileSavePicker + appel handler + toast
  ViewModels/Playlists/PlaylistsViewModel.cs   ← + ImportPlaylistsCommand
  ViewModels/Playlist/PlaylistViewModel.cs     ← + ExportPlaylistCommand
  ViewModels/Playlists/Handlers/
    PlaylistImportedMessageHandler.cs    ← rafraîchissement de la liste
  Pages/PlaylistsPage.xaml               ← bouton + MenuFlyoutItem
  Pages/PlaylistPage.xaml                ← bouton "Exporter"
```

Trois principes :

1. **Tout l'IO disque et le parsing M3U8 reste en `Infrastructure`.** Les handlers de `Application` ne connaissent que les interfaces et un modèle neutre `PlaylistFileModel`.
2. **Les handlers CQRS portent le métier** : matching `FilePath → TrackEntity`, gestion des doublons de nom, comptage matched / ignored / skipped.
3. **Les ViewModels n'ont aucune logique métier ni IO disque** : ils délèguent à un service dédié (pattern existant dans Rok, cf. `PlaylistCreationService`, `PlaylistUpdateService`).

L'ajout d'un format futur (PLS, XSPF, WPL) sera : 1 nouveau `XxxReader.cs` + 1 nouveau `XxxWriter.cs` + 1 valeur d'enum + 1 entrée dans `PlaylistFormatResolver` + 1 ligne `services.AddTransient`. Aucun changement dans `Application` ni dans `Domain`.

## Composants

### Modèle neutre `PlaylistFileModel` (Application)

Pivot entre le parser et les handlers, format-agnostique :

```csharp
public sealed record PlaylistFileModel(
    string Name,
    IReadOnlyList<PlaylistFileEntry> Entries);

public sealed record PlaylistFileEntry(
    string FilePath,
    string? Title,
    string? Artist,
    TimeSpan? Duration);
```

À l'export, `Name` = nom de la playlist Rok. À l'import, `Name` = nom du fichier sans extension (les directives non-standard `#PLAYLIST:` sont ignorées).

### `IPlaylistFormatReader` / `IPlaylistFormatWriter` (Application)

```csharp
public interface IPlaylistFormatReader
{
    ExportPlaylistFormat Format { get; }
    Task<PlaylistFileModel> ReadAsync(Stream stream, string fileNameHint, CancellationToken ct);
}

public interface IPlaylistFormatWriter
{
    ExportPlaylistFormat Format { get; }
    Task WriteAsync(Stream stream, PlaylistFileModel model, CancellationToken ct);
}
```

`Stream` plutôt que `string path` : tests triviaux via `MemoryStream`, IO disque isolée dans le handler.

### `IPlaylistFormatResolver` (Application) → `PlaylistFormatResolver` (Infrastructure)

Reçoit en DI `IEnumerable<IPlaylistFormatReader>` et `IEnumerable<IPlaylistFormatWriter>` enregistrés. Expose :

```csharp
bool TryGetReader(string extension, out IPlaylistFormatReader? reader);
bool TryGetWriter(string extension, out IPlaylistFormatWriter? writer);
```

Comparaison d'extension **case-insensitive**. En v1, lecture acceptée pour `.m3u` et `.m3u8` ; écriture pour `.m3u8` uniquement.

### `M3u8PlaylistReader` (Infrastructure)

Parse ligne à ligne :
- `#EXTM3U` toléré présent ou absent
- `#EXTINF:<seconds>,<artist> - <title>` → durée + `Artist`/`Title` séparés sur le **premier** ` - ` (un titre contenant ` - ` reste correct ; un artiste contenant ` - ` est rare et accepté tel quel)
- Toute autre ligne `#…` → ignorée
- Lignes vides → ignorées
- Toute ligne non-`#` non-vide → considérée comme chemin de fichier

Encodage : `StreamReader` avec détection BOM, fallback UTF-8. Les bytes invalides sont remplacés silencieusement (comportement par défaut .NET).

`Name` produit = `fileNameHint` sans extension.

### `M3u8PlaylistWriter` (Infrastructure)

Sortie type :

```
#EXTM3U
#EXTINF:215,Daft Punk - One More Time
D:\Music\Daft Punk\Discovery\01 - One More Time.mp3
#EXTINF:227,Daft Punk - Aerodynamic
D:\Music\Daft Punk\Discovery\02 - Aerodynamic.mp3
```

Règles :
- `#EXTM3U` toujours en première ligne
- `#EXTINF` **toujours** émis. Durée arrondie à l'entier ; `-1` si inconnue. Format `Artist - Title` ; si l'un est null on émet ce qu'on a (`Artist - `, ` - Title`, ou virgule terminale vide)
- Encodage **UTF-8 sans BOM**
- Sauts de ligne `\n` (LF)

### `ImportPlaylistCommandHandler` (Application)

```csharp
public sealed record ImportPlaylistCommand(string FilePath)
    : ICommand<Result<PlaylistImportResult>>;

public enum PlaylistImportStatus
{
    Imported,
    Skipped     // zéro piste de la playlist n'est en BDD ; pas une erreur
}

public sealed record PlaylistImportResult(
    PlaylistImportStatus Status,
    long? PlaylistId,
    string? FinalName,
    int MatchedCount,
    int IgnoredCount);
```

`Result.Fail` est réservé aux vraies erreurs (parse KO, DB KO). `Skipped` est un succès sans création de playlist. Le `PlaylistImportService` branche sur ces trois cas pour ses compteurs.

Pipeline :

1. Résoudre le reader via l'extension. Inconnu → `Result.Fail("UnsupportedFormat")`.
2. Ouvrir le fichier en lecture, parser → `PlaylistFileModel`. Échec → `Result.Fail`.
3. Pour chaque `entry.FilePath` → `ITrackRepository.GetByFilePathAsync(path)`. Match strict case-insensitive. Compteurs `matched` / `ignored` accumulés **hors transaction** (lecture seule).
4. **Si `matched.Count == 0`** → retourner `Result.Ok(new PlaylistImportResult(Skipped, null, null, 0, ignored))`. Pas de transaction ouverte, pas de header créé.
5. Calculer `FinalName` : si le `Name` parsé existe en BDD, suffixer `(2)`, `(3)`… jusqu'à libre. **Hard cap à 999** : au-delà, retourner `Result.Fail("NameCollisionExhausted")`.
6. **Ouvrir une transaction SQLite** sur la connexion partagée. Insérer `PlaylistHeaderEntity` (Type=Classic, `TrackCount`, `Duration` agrégés sur les pistes matched). Insérer les `PlaylistTrackEntity` avec `Position` séquentielle (0..N-1, `Listened=false`). Sur échec d'insertion → rollback automatique, propager en `Result.Fail`.
7. Émettre `PlaylistImportedMessage(playlistId)` **après commit**.
8. Retourner `Result.Ok(new PlaylistImportResult(Imported, id, finalName, matched.Count, ignored))`.

### `ExportPlaylistCommandHandler` (Application)

```csharp
public sealed record ExportPlaylistCommand(long PlaylistId, string FilePath) : ICommand<Result>;
```

Pipeline :

1. `GetPlaylistByIdQuery(playlistId)` → header (`Result.Fail("not found")` si null).
2. `GetPlaylistTracksByPlaylistIdQuery(playlistId)` → liste de `TrackEntity` ordonnée par `Position`. **Identique pour Smart et Classic** (pas de régénération).
3. Mapper en `PlaylistFileModel` (Name=header.Name, Entries={FilePath, Title, Artist, Duration}).
4. Résoudre le writer via l'extension. Inconnu → `Result.Fail`.
5. Écriture atomique : ouvrir un `<final>.tmp` adjacent, `WriteAsync`, fermer, `File.Move(tmp, final, overwrite:true)`. Sur exception → tenter de supprimer le `.tmp`, propager.

### `PlaylistImportService` (Presentation)

Enregistré comme `Transient` dans `Presentation/DependencyInjection.cs`.

```csharp
public Task<PlaylistImportSummary> RunAsync(IntPtr hwnd, CancellationToken ct);
```

- Ouvre `FileOpenPicker` filtré sur `.m3u`, `.m3u8`, multi-sélection.
- User cancel → retour silencieux, pas de toast.
- Pour chaque fichier sélectionné → `mediator.Send(new ImportPlaylistCommand(path), ct)`. Branche selon `Result` :
  - `Ok` + `Status == Imported` → `imported++`, `tracksTotal += MatchedCount`, `ignoredTotal += IgnoredCount`
  - `Ok` + `Status == Skipped` → `skipped++`
  - `Fail` → `failed++` (log de la cause)
- Construit le toast récap unique :
  - `"N importée(s) — X piste(s), Y ignorée(s)"`
  - `+ " — Z vide(s) ignorée(s)"` si `skipped > 0`
  - `+ " — F en échec"` si `failed > 0`
- Si `imported == 0 && (skipped > 0 || failed > 0)` → toast d'avertissement plutôt que succès.

### `PlaylistExportService` (Presentation)

```csharp
public Task RunAsync(IntPtr hwnd, PlaylistHeaderEntity playlist, CancellationToken ct);
```

- Si `playlist.Type == PlaylistType.Smart` → `ContentDialog` *"La playlist sera exportée telle qu'elle est actuellement. Ses règles intelligentes ne seront pas conservées dans le fichier."* (boutons OK / Annuler). Cancel → retour silencieux.
- Ouvre `FileSavePicker`, pré-rempli avec `<playlist.Name>.m3u8`, filtre `.m3u8`. Cancel → retour silencieux.
- `mediator.Send(new ExportPlaylistCommand(playlist.Id, path), ct)`.
- Succès → toast `"Playlist exportée"`. Échec → toast d'erreur avec message.

### `TrackRepository.GetByFilePathAsync` (Infrastructure)

Nouvelle méthode sur `ITrackRepository` :

```csharp
Task<TrackEntity?> GetByFilePathAsync(string filePath, CancellationToken ct);
```

SQL : `SELECT * FROM Tracks WHERE filePath = @filePath COLLATE NOCASE LIMIT 1`. Aucun changement de schéma.

### `PlaylistImportedMessage` + handler

`Messages/PlaylistImportedMessage(long PlaylistId)` ; handler `Presentation/ViewModels/Playlists/Handlers/PlaylistImportedMessageHandler` qui force `PlaylistsViewModel` à recharger sa liste. Pattern existant identique à `AlbumImportedMessageHandler` / `TrackImportedMessageHandler`.

## Flux de données

### Import (un fichier = une transaction)

```
For each file selected:
  Open file (Stream)
  Parse → PlaylistFileModel
    [parse error → log + count failed, continue]

  For each entry: ITrackRepository.GetByFilePathAsync
    match → matched++ (queued)
    miss  → ignored++

  if matched.Count == 0:
    skipped++, continue (no header created)

  BEGIN TRANSACTION
    INSERT PlaylistHeader (Type=Classic, FinalName auto-suffixed)
    INSERT PlaylistTracks (Position 0..N-1)
    [DB error → ROLLBACK, log + count failed, continue]
  COMMIT

  Publish PlaylistImportedMessage
  Return PlaylistImportResult

Toast récap = somme : N importée(s) — X piste(s), Y ignorée(s) [+Z skipped] [+F failed]
```

### Export

```
PlaylistViewModel.ExportPlaylistCommand
  → PlaylistExportService.RunAsync(playlist)
    → if Type==Smart: ContentDialog "snapshot, sans règles" (cancel → return)
    → FileSavePicker (cancel → return)
    → mediator.Send(ExportPlaylistCommand)
      → GetPlaylistByIdQuery
      → GetPlaylistTracksByPlaylistIdQuery (Smart et Classic, pas de régénération)
      → Map → PlaylistFileModel
      → Resolver.GetWriter(".m3u8")
      → Write to <final>.tmp, then File.Move(tmp, final, overwrite:true)
    → Toast "Playlist exportée" / erreur
```

## Gestion d'erreurs

### Import

| Cas | Niveau | Comportement |
|---|---|---|
| Fichier introuvable / accès refusé | Erreur fichier | Log warn, compteur `failed`, on passe au suivant |
| Encodage illisible / contenu binaire | Erreur fichier | Idem. Si zéro ligne non-`#` non-vide → traité comme parse error |
| Aucune piste de la playlist en BDD | Skipped (pas erreur) | Compteur `skipped`, pas de header créé |
| Piste individuelle absente de la BDD | Pas erreur | Compteur `ignored`. Playlist créée avec le reste |
| `INSERT` SQLite échoue (lock, FK, …) | Erreur DB | `ROLLBACK` cette playlist, log error, compteur `failed` |
| Auto-suffixe collide jusqu'à `(999)` | Très rare | Au-delà, erreur DB normale → `failed` |
| `OperationCanceledException` | Annulation | Propagation, transaction en cours rollback, toast *"Import annulé"* |

### Export

| Cas | Comportement |
|---|---|
| User annule la dialog Smart | Retour silencieux |
| User annule le `FileSavePicker` | Retour silencieux |
| Playlist supprimée entre-temps | `Result.Fail`, toast *"Playlist introuvable"* |
| Playlist vide (0 piste) | Fichier écrit avec juste `#EXTM3U`, toast `"Playlist exportée (vide)"` |
| `IOException` à l'écriture | Toast erreur, `.tmp` supprimé, pas de fichier partiel |

### Logs

`ILogger<T>` (Serilog) :
- `LogInformation` au début/fin de chaque opération avec compteurs et durée
- `LogWarning` pour `skipped` et `ignored` agrégés
- `LogError` (avec exception) pour `failed` et erreurs export

## Tests

Total estimé **≈ 45 tests**, conventions AAA, `DisplayName` snake_case anglais, organisation par feature, fixtures SQLite réelles pour l'infra (cohérent avec la convention en place).

### `Rok.ApplicationTests/Features/Playlists/`

**`ImportPlaylistCommandHandlerTests.cs`** (≈ 12 tests) — `IPlaylistFormatReader`, `ITrackRepository`, repos playlist mockés Moq :

- `creates_playlist_with_only_matched_tracks`
- `ignored_count_reflects_unmatched_paths`
- `skipped_when_zero_tracks_match`
- `skipped_does_not_create_header`
- `name_collision_is_suffixed_with_paren_two`
- `name_collision_walks_until_free_slot`
- `parse_error_returns_failed_without_inserting`
- `db_error_during_track_insert_rolls_back_header`
- `cancellation_propagates_and_rolls_back`
- `unsupported_extension_returns_fail`
- `track_positions_are_zero_based_and_sequential`
- `publishes_playlist_imported_message_on_success`

**`ExportPlaylistCommandHandlerTests.cs`** (≈ 6 tests) :

- `writes_classic_playlist_with_all_tracks_in_order`
- `writes_smart_playlist_using_persisted_tracks_only`
- `empty_playlist_writes_only_extm3u_header`
- `playlist_not_found_returns_fail`
- `unsupported_extension_returns_fail`
- `cancellation_propagates_during_write`

### `Rok.Infrastructure.UnitTests/Playlists/`

**`Formats/M3u8PlaylistReaderTests.cs`** (≈ 10 tests, fixtures dans `TestData/Playlists/`) :

- `reads_minimal_playlist_with_paths_only`
- `reads_extm3u_header_when_present`
- `tolerates_missing_extm3u_header`
- `reads_extinf_artist_title_and_duration`
- `parses_extinf_with_dash_in_title`
- `treats_unknown_directives_as_comments`
- `skips_blank_lines`
- `handles_utf8_with_bom`
- `handles_utf8_without_bom`
- `crlf_and_lf_line_endings_supported`

**`Formats/M3u8PlaylistWriterTests.cs`** (≈ 6 tests, asserts via `MemoryStream` + comparaison string) :

- `writes_extm3u_header_first`
- `writes_extinf_with_seconds_and_artist_dash_title`
- `writes_path_after_extinf`
- `writes_extinf_with_minus_one_when_duration_unknown`
- `output_is_utf8_without_bom`
- `roundtrip_with_reader_preserves_paths_and_metadata`

**`PlaylistFormatResolverTests.cs`** (≈ 4 tests) :

- `resolves_reader_for_m3u8_extension`
- `resolves_reader_for_m3u_extension`
- `returns_false_for_unknown_extension`
- `extension_match_is_case_insensitive`

**`Repositories/TrackRepositoryGetByFilePathTests.cs`** (≈ 3 tests, `SqliteDatabaseFixture`) :

- `returns_track_when_file_path_matches_exactly`
- `returns_track_when_file_path_matches_case_insensitive`
- `returns_null_when_no_match`

### `Rok.PresentationTests/`

**`ViewModels/Playlists/Services/PlaylistImportServiceTests.cs`** (≈ 4 tests) :

- `aggregates_counts_across_multiple_files`
- `does_not_show_toast_when_user_cancels_picker`
- `toast_includes_skipped_count_when_present`
- `toast_includes_failed_count_when_present`

**`ViewModels/Playlist/Services/PlaylistExportServiceTests.cs`** (≈ 4 tests) :

- `shows_warning_dialog_for_smart_playlist_before_picker`
- `does_not_show_warning_for_classic_playlist`
- `does_not_call_handler_when_dialog_cancelled`
- `does_not_call_handler_when_picker_cancelled`

### Fixtures M3U8 versionnées (`TestData/Playlists/`)

- `minimal.m3u8` (3 chemins, pas d'EXTINF)
- `extended.m3u8` (avec `#EXTM3U` + `#EXTINF`)
- `with_dash_in_title.m3u8`
- `unknown_directives.m3u8`
- `utf8_bom.m3u8`
- `utf8_no_bom.m3u8`
- `crlf_endings.m3u8`
- `lf_endings.m3u8`

## Out of scope (v1)

- PLS, XSPF, WPL (architecture prête, juste des classes à ajouter)
- Pochettes de playlist (M3U8 ne le supporte pas)
- Export multi-playlists / batch
- Backup / restore complet (Options)
- Matching tolérant aux déplacements de bibliothèque (nom de fichier, MBID, tags)
- M3U legacy ANSI (lecture acceptée via extension `.m3u`, mais pas d'option d'écriture)
- Import qui crée des Smart playlists (impossible : aucun format externe ne porte les règles)
