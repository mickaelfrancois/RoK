# Design — Accessibilité clavier (raccourcis + SMTC)

- **Date** : 2026-04-30
- **Statut** : design validé, prêt pour planification
- **Scope** : v1 — raccourcis clavier figés (architecturés pour personnalisation future) + complétion de l'intégration `SystemMediaTransportControls`

## Objectif

Rendre Rok pleinement utilisable au clavier pour deux populations :

1. **Power users** — raccourcis pour la lecture (Play/Pause, Suivant, Précédent, Volume, Mute, Shuffle, Repeat, Seek ±5s) et pour la navigation entre pages (Albums, Artistes, Pistes, Playlists, Insights, Listening, Recherche).
2. **Utilisateurs Windows hors application** — touches média globales du clavier (Play/Pause/Next/Previous/Stop) actives même quand Rok n'a pas le focus, et présence de Rok dans le panneau "Lecture en cours" de Windows avec pochette d'album et barre de progression.

Hors scope v1 :
- UI de remappage des raccourcis (architecture l'autorise, mais aucun écran de réglage)
- Audit lecteur d'écran (Narrator) et navigation Tab page-par-page (sujets séparés)
- Mode contraste élevé / accessibilité visuelle

## Décisions verrouillées

| # | Décision |
|---|---|
| 1 | **Raccourcis figés** en v1 ; pas d'UI de remappage. Architecture les centralise dans un catalogue (`KeyboardShortcutCatalog`) pour activer la personnalisation V2 sans casser les consommateurs. |
| 2 | **Source unique de vérité** : `KeyboardShortcutCatalog` (Application). Les `KeyboardAccelerator` XAML sont attachés en code-behind à partir du catalogue (pas de bindings figés en XAML). |
| 3 | **Tooltips auto-enrichis** par WinUI : attacher un `KeyboardAccelerator` à un contrôle qui a déjà un tooltip suffit, WinUI ajoute la combinaison localisée. |
| 4 | **Dialogue d'aide F1** + bouton "Voir les raccourcis clavier" dans la page Options. |
| 5 | **SMTC déjà branché** : on **étend** `ISystemMediaTransportControlsService` au lieu de le réécrire — pas de nouveau service, pas de nouvelle classe d'abonnement. |
| 6 | **Compléments SMTC** : pochette d'album (`DisplayUpdater.Thumbnail` via `RandomAccessStreamReference.CreateFromFile`), barre de progression (`UpdateTimelineProperties` à 1 Hz), état `Stopped` réel en bout de queue. |
| 7 | **Signature SMTC modifiée** : `UpdatePlaybackState(bool)` → `UpdatePlaybackState(PlaybackStatus)` (enum `Playing | Paused | Stopped | Closed`). `UpdateTrackInfo(TrackDto)` → `UpdateTrackInfo(TrackDto track, string? coverPath)`. |
| 8 | **Garde de focus pour `Espace`** : si `FocusManager.GetFocusedElement` est un `TextBox`/`AutoSuggestBox`/`PasswordBox`/`Slider`, l'accélérateur ne déclenche pas Play/Pause (idem `Shift+←/→` quand le slider de progression a le focus). |
| 9 | **Modes plein écran/compact** : `Ctrl+1..5`, `Ctrl+0`, `Ctrl+F` basculent **automatiquement** en mode normal puis exécutent l'action (UX moins surprenante que d'ignorer). Les raccourcis lecture fonctionnent dans tous les modes. |
| 10 | **AZERTY supporté nativement** via les codes virtuels `D0..D5` (les touches numériques en haut de clavier produisent ces codes sans Shift). |
| 11 | **Localisation** : libellés d'action (catalogue) traduits via `Resources.resw` aux 4 langues (en-US, fr-FR, es-ES, uk-UA). Le rendu des touches utilise `VirtualKey.ToString()` + table de mapping pour les symboles courants (`Ctrl`, `Shift`, `Alt`, flèches). |
| 12 | **Cycle de vie** : raccourcis attachés à `MainWindow` à `Loaded`, jamais désinscrits (durée de vie = fenêtre). |
| 13 | **Aucun changement domaine, aucune migration DB.** |

## Catalogue des raccourcis

### Lecture (in-app, fenêtre active)

| Raccourci | Action | Cible/notes |
|---|---|---|
| `Espace` | Play/Pause | Attaché au `playPauseButton` (PlayerView). Garde de focus. |
| `Ctrl+→` | Piste suivante | |
| `Ctrl+←` | Piste précédente | |
| `Ctrl+↑` | Volume + | |
| `Ctrl+↓` | Volume − | |
| `Ctrl+M` | Mute toggle | |
| `Ctrl+H` | Shuffle toggle | (`H` = sHuffle) |
| `Ctrl+T` | Repeat toggle | |
| `Shift+→` | Seek +5s | Garde de focus sur `progressSlider` |
| `Shift+←` | Seek −5s | Idem |

### Navigation

| Raccourci | Action |
|---|---|
| `Ctrl+1` | Page Albums |
| `Ctrl+2` | Page Artistes |
| `Ctrl+3` | Page Pistes |
| `Ctrl+4` | Page Playlists |
| `Ctrl+5` | Page Insights |
| `Ctrl+0` | Page Listening (lecture en cours) |
| `Ctrl+F` | Focus champ recherche |
| `Alt+←` | Retour (déjà géré par `NavigationView`, documenté) |
| `Échap` | Retour si backstack non vide ; sinon no-op (les flyouts/dialogs traitent Échap en priorité native WinUI) |

### Modes

| Raccourci | Action |
|---|---|
| `F11` | Bascule plein écran |
| `Ctrl+Shift+M` | Bascule mode compact |
| `Échap` (depuis plein écran/compact) | Retour mode normal |

### Aide

| Raccourci | Action |
|---|---|
| `F1` | Ouvre le dialogue listant tous les raccourcis |

### Touches média globales (SMTC, hors catalogue in-app)

`Play`, `Pause`, `Next`, `Previous`, `Stop` du clavier média → captées par `SystemMediaTransportControls` même quand Rok est en arrière-plan. Rok apparaît dans le panneau "Lecture en cours" de Windows avec pochette + titre/artiste/album + barre de progression.

## Architecture et arborescence

> **Note d'implémentation (2026-04-30)** : `Rok.Application` cible `net10.0` sans SDK Windows. Les types WinRT `Windows.System.VirtualKey` / `VirtualKeyModifiers` ne sont donc pas accessibles depuis Application. Le catalogue a été placé en **Presentation** (qui cible `net10.0-windows10.0.26100.0`) plutôt que de upgrader le TFM d'Application. Le catalogue reste néanmoins la source unique de vérité pour ses consommateurs.

```
Rok.Application/
  Player/
    PlaybackStatus.cs              ← enum (Playing, Paused, Stopped, Closed)

  Interfaces/
    ISystemMediaTransportControlsService.cs   ← signature étendue (cf. § SMTC)

Rok.Infrastructure/
  Player/
    SystemMediaTransportControlsService.cs    ← thumbnail + timeline + enum

Presentation/
  Services/
    Accessibility/
      KeyboardShortcut.cs           ← record { ShortcutId Id, ShortcutCategory Category,
                                                VirtualKeyModifiers Modifiers, VirtualKey Key,
                                                string LabelResourceKey }
      KeyboardShortcutCatalog.cs    ← static : All, ByCategory(...), ById(...)
      ShortcutId.cs                 ← enum (PlayPause, Next, Previous, VolumeUp, …, OpenAlbums, OpenListening, …)
      ShortcutCategory.cs           ← enum (Playback, Navigation, Modes, Help)
    KeyboardShortcutInstaller.cs    ← convertit catalogue → KeyboardAccelerator,
                                      attache à Window ou contrôles cibles
  Dialogs/
    KeyboardShortcutsDialog.xaml(.cs)
  Commons/
    KeyVisualBox.xaml(.cs)         ← rendu d'une combinaison "Ctrl + →"

tests/UnitTests/
  Rok.PresentationTests/
    Accessibility/
      KeyboardShortcutCatalogTests.cs       ← 7 tests : unicité id, unicité combinaison, etc.
  Rok.ApplicationTests/
    Player/
      PlayerServiceSmtcTests.cs              ← 4 tests : cover, Stopped, timeline 1 Hz
```

## Catalogue (détail)

`KeyboardShortcut` est un `record` immuable :

```csharp
public sealed record KeyboardShortcut(
    ShortcutId Id,
    ShortcutCategory Category,
    VirtualKeyModifiers Modifiers,
    VirtualKey Key,
    string LabelResourceKey);
```

`KeyboardShortcutCatalog` expose une `IReadOnlyList<KeyboardShortcut> All` figée à l'init (pas de mutabilité), plus :
- `KeyboardShortcut ById(ShortcutId id)`
- `IEnumerable<KeyboardShortcut> ByCategory(ShortcutCategory category)`

Aucune dépendance sortante. Testable trivialement.

**Pour la V2 personnalisable** : remplacer la classe statique par un `IKeyboardShortcutProvider` injecté ; les consommateurs (Installer, Dialog) prennent l'interface au lieu de la statique. Aucun autre changement requis.

## Wiring `KeyboardAccelerator` (code-behind)

`KeyboardShortcutInstaller` convertit chaque `KeyboardShortcut` en `KeyboardAccelerator` et l'attache à la cible appropriée :

| `ShortcutId` | Cible d'attachement |
|---|---|
| `PlayPause`, `Next`, `Previous`, volume, mute, shuffle, repeat, seek | Boutons existants du `PlayerView` (tooltips auto-enrichis) |
| `OpenAlbums`, `OpenArtists`, `OpenTracks`, `OpenPlaylists`, `OpenInsights` | `NavigationViewItem` correspondants (`albumsItem`, …) |
| `OpenListening` | Bouton `MusicInfo` du `PlayerView` (ligne 361) |
| `FocusSearch`, `Help`, `ToggleFullScreen`, `ToggleCompact`, `Back` | Globaux : ajoutés à `Window.KeyboardAccelerators` (pas de tooltip, exposés uniquement via le dialogue F1) |

L'installer est appelé une fois depuis `MainWindow.OnLoaded`. Les handlers (méthodes `KeyboardAcceleratorInvoked`) résident dans `MainWindow.xaml.cs` et `PlayerView.xaml.cs` — l'installer n'a pas connaissance des actions, il pose seulement les `KeyboardAccelerator` et exposes les `Invoked` events que les `xaml.cs` câblent.

### Garde de focus

Pour `Espace` Play/Pause :

```csharp
private void OnPlayPauseAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
{
    if (FocusManager.GetFocusedElement(XamlRoot) is TextBox or AutoSuggestBox or PasswordBox)
    {
        args.Handled = false;
        return;
    }
    args.Handled = true;
    ViewModel.TogglePlayPauseCommand.Execute(null);
}
```

Pour `Shift+←/→` Seek : même pattern, on ajoute `Slider` à la liste des types interceptés.

### Échap — ordre de priorité

1. `ContentDialog` ouvert → ferme le dialogue (WinUI natif)
2. `Flyout`/`MenuFlyout` ouvert → ferme le flyout (WinUI natif)
3. Mode plein écran ou compact → retour mode normal (notre handler)
4. Backstack `NavigationView` non vide → retour (notre handler)
5. Sinon → no-op

## SMTC — compléments

### Signature étendue

```csharp
public interface ISystemMediaTransportControlsService : IDisposable
{
    void Initialize();
    void SetPlayerService(IPlayerService playerService);
    void UpdatePlaybackState(PlaybackStatus status);          // BREAKING : bool → enum
    void UpdateTrackInfo(TrackDto track, string? coverPath);  // ajout coverPath
    void UpdateTimeline(TimeSpan position, TimeSpan duration); // nouveau
}
```

### Implémentation `SystemMediaTransportControlsService`

- `UpdateTrackInfo` charge la pochette via `StorageFile.GetFileFromPathAsync(coverPath)` puis `RandomAccessStreamReference.CreateFromFile()` → `DisplayUpdater.Thumbnail`. Try/catch pour fichier manquant ou inaccessible — le titre/artiste/album sont mis à jour quoi qu'il arrive.
- `UpdateTimeline` appelle `_smtc.UpdateTimelineProperties(new SystemMediaTransportControlsTimelineProperties { StartTime = TimeSpan.Zero, EndTime = duration, Position = position, MinSeekTime = TimeSpan.Zero, MaxSeekTime = duration })`.
- `UpdatePlaybackState` map enum → `MediaPlaybackStatus.Playing|Paused|Stopped|Closed`.

### Côté `PlayerService`

- Récupération de la pochette : injection de `IAlbumPicture` (déjà existant en Application/Interfaces) ; lors d'un changement de piste, `coverPath = _albumPicture.PictureFileExists(album.Path) ? _albumPicture.GetPictureFile(album.Path) : null`.
- Émission timeline : un `ITimer` (via `TimeProvider.CreateTimer`, déjà injecté) à 1 Hz pendant `Playing` ; arrêté en `Paused`/`Stopped`. Appelle `_smtcService.UpdateTimeline(currentPosition, duration)`.
- Bout de queue : remplacer l'appel `_smtcService.UpdatePlaybackState(false)` par `_smtcService.UpdatePlaybackState(PlaybackStatus.Stopped)` quand il n'y a plus de piste suivante.
- Tous les autres callers : `true` → `Playing`, `false` → `Paused`.

## Dialogue d'aide (F1)

`Presentation/Dialogs/KeyboardShortcutsDialog.xaml` — `ContentDialog` :

- Titre localisé "Raccourcis clavier"
- Liste groupée par `ShortcutCategory` : **Lecture / Navigation / Modes / Aide**
- Chaque ligne : nom de l'action (depuis `LabelResourceKey` via `Resources.resw`) à gauche, raccourci rendu via `KeyVisualBox` à droite
- Largeur fixe raisonnable (~520 px)
- Bouton "Fermer" + `Échap` (natif `ContentDialog`)
- Ouvert via le `KeyboardAccelerator` global `F1` **et** via le bouton "Voir les raccourcis clavier" de la page Options

`KeyVisualBox` — `UserControl` minimaliste :
- Reçoit une `KeyboardShortcut`
- Affiche les modifieurs dans l'ordre `Ctrl > Shift > Alt > Win`, séparés par "+"
- Touche principale : symbole pour les flèches (`→`, `←`, `↑`, `↓`), nom WinUI sinon (`Espace`, `Échap`, `F1`, `1`, …)
- Style cohérent avec les "key boxes" de Windows Settings (bordure fine, fond légèrement contrasté)

## Découvrabilité

- **Tooltips** : auto-enrichis par WinUI dès qu'un `KeyboardAccelerator` est attaché à un contrôle qui a un tooltip. Aucune chaîne `Resources.resw` à modifier pour les raccourcis sur boutons.
- **F1** : convention Windows, ouvre le dialogue.
- **Page Options** : un bouton "Voir les raccourcis clavier" (libellé localisé) ouvre le même dialogue.
- **Pas d'astuce intrusive** type info-bulle "Appuyez sur F1…" en surcouche.

## Localisation

Nouveaux libellés (un par action du catalogue + un pour le bouton Options + un pour le titre du dialogue) ajoutés à `Resources.resw` aux 4 langues (en-US, fr-FR, es-ES, uk-UA) :

- `KeyboardShortcut_Action_PlayPause`, `…_Next`, `…_Previous`, `…_VolumeUp`, `…_VolumeDown`, `…_Mute`, `…_Shuffle`, `…_Repeat`, `…_SeekForward`, `…_SeekBackward`
- `KeyboardShortcut_Action_OpenAlbums`, `…_OpenArtists`, `…_OpenTracks`, `…_OpenPlaylists`, `…_OpenInsights`, `…_OpenListening`, `…_FocusSearch`, `…_Back`
- `KeyboardShortcut_Action_ToggleFullScreen`, `…_ToggleCompact`, `…_Help`
- `KeyboardShortcut_Dialog_Title`, `KeyboardShortcut_Dialog_Group_Playback`, `…_Group_Navigation`, `…_Group_Modes`, `…_Group_Help`, `KeyboardShortcut_Options_Button`

## Tests

### Unitaires (`Rok.ApplicationTests/Accessibility/KeyboardShortcutCatalogTests.cs`)

- `Catalog_should_contain_no_duplicate_id`
- `Catalog_should_contain_no_duplicate_modifiers_and_key_combination`
- `Each_shortcut_should_have_non_empty_label_resource_key`
- `ById_should_return_expected_shortcut_for_each_id`
- `ByCategory_should_group_all_shortcuts_correctly`

### Unitaires (`Rok.ApplicationTests/Player/PlayerServiceTests.cs` — extensions)

- `when_track_changes_smtc_receives_track_info_with_cover_path`
- `when_track_changes_and_no_cover_file_smtc_receives_null_cover_path`
- `when_queue_ends_smtc_receives_playback_state_stopped`
- `when_playing_timeline_updates_throttled_to_one_hz` (`TimeProvider` avancé manuellement, vérifier nb d'appels à `UpdateTimeline`)

### Intégration manuelle (documentée en checklist du PR)

- Espace dans la barre de recherche n'arrête pas la lecture
- Ctrl+1..5 navigue correctement, y compris depuis plein écran/compact (avec retour automatique en mode normal)
- F1 ouvre le dialogue, Échap le ferme
- Touches média du clavier (Play/Pause/Next/Previous) fonctionnent quand Rok est en arrière-plan
- Rok apparaît dans le panneau "Lecture en cours" de Windows (Win+G ou clic sur l'icône volume) avec pochette, titre, artiste, album, et barre de progression qui avance
- Tooltips de boutons du PlayerView affichent bien la combinaison du raccourci

## Impacts code

**Nouveaux fichiers (~7)** : `KeyboardShortcut.cs`, `KeyboardShortcutCatalog.cs`, `ShortcutId.cs`, `ShortcutCategory.cs`, `PlaybackStatus.cs`, `KeyboardShortcutInstaller.cs`, `KeyboardShortcutsDialog.xaml(.cs)`, `KeyVisualBox.xaml(.cs)`.

**Fichiers modifiés (~9)** :
- `Rok.Application/Interfaces/ISystemMediaTransportControlsService.cs`
- `Rok.Infrastructure/Player/SystemMediaTransportControlsService.cs`
- `Rok.Application/Player/PlayerService.cs`
- `Presentation/MainWindow.xaml.cs`
- `Presentation/Pages/PlayerView.xaml.cs`
- `Presentation/DependencyInjection.cs`
- `Presentation/Pages/OptionsPage.xaml(.cs)`
- `Presentation/Strings/{en-US,fr-FR,es-ES,uk-UA}/Resources.resw`

**Total estimé** : ~15 fichiers touchés (7 nouveaux, 8 modifiés). Aucune migration DB. Aucun changement domaine. Compatible architecture en couches. Le seek ±5s utilise `IPlayerService.Position` (déjà existant).

## Hors scope (V2+)

- UI de remappage des raccourcis dans la page Options
- Audit lecteur d'écran (Narrator) : `AutomationProperties.Name`, `HelpText`, `LabeledBy` sur tous les contrôles
- Navigation Tab cohérente page-par-page (focus visible, ordre logique, ouverture par Enter, menus contextuels via touche Menu/F10)
- Mode contraste élevé Windows
- Animations réduites (respect du paramètre Accessibility Windows)
- Annonces dynamiques (live regions) pour le changement de piste
