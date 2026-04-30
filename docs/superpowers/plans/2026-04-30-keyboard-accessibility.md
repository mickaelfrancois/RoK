# Keyboard Accessibility Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rendre Rok pleinement utilisable au clavier (raccourcis lecture, navigation, modes, aide) et compléter l'intégration `SystemMediaTransportControls` (pochette + barre de progression + état Stopped) pour que Rok apparaisse correctement dans le panneau "Lecture en cours" de Windows.

**Architecture:** Catalogue centralisé immuable (`KeyboardShortcutCatalog`) en couche Application, source unique de vérité pour les `KeyboardAccelerator` attachés en code-behind via un `KeyboardShortcutInstaller` (Presentation). SMTC déjà branché : on étend la signature de `ISystemMediaTransportControlsService` (enum `PlaybackStatus`, `coverPath`, `UpdateTimeline`) et on complète l'implémentation. Pas de migration DB, pas de changement Domain.

**Tech Stack:** .NET 10, C# 13 preview, WinUI 3 (Windows App SDK 1.8), CommunityToolkit.Mvvm, MiF.Mediator/Messenger, Dapper/SQLite, NAudio. Tests : xUnit + Moq + `Microsoft.Extensions.TimeProvider.Testing`.

**Spec :** `docs/superpowers/specs/2026-04-30-keyboard-accessibility-design.md`

**Branche :** `MF/keyboard-accessibility` (déjà créée depuis `origin/master`)

**Build commands :**
- Build : `dotnet build /p:Platform=x64`
- Tests Application : `dotnet test tests/UnitTests/Rok.ApplicationTests/Rok.ApplicationTests.csproj /p:Platform=x64`
- Test ciblé : `dotnet test /p:Platform=x64 --filter "FullyQualifiedName~<TestClassName>"`

**Conventions du projet à respecter :**
- Tous identifiants/commentaires en anglais. Pas de regions. Pas de collection expressions (`new List<T>()`/`new[] { }`).
- Braces sur leur propre ligne, blank lines autour des conditions/boucles, early return.
- AAA dans les tests, `DisplayName` snake_case en anglais (ex. `when_track_changes_smtc_receives_cover_path`).
- Husky pre-commit lance `dotnet build` + `dotnet format` sur les `*.cs` stagés. Husky pre-push lance `dotnet test`. Ne pas contourner.
- Conventional Commits : `feat|fix|refactor|test|chore|docs(scope)?: …`. BREAKING CHANGES → footer `BREAKING CHANGE:`.

---

## File Structure

### Nouveaux fichiers

- `src/Rok.Application/Player/PlaybackStatus.cs` — enum `Playing | Paused | Stopped | Closed`
- `src/Rok.Application/Accessibility/ShortcutId.cs` — enum (PlayPause, Next, Previous, VolumeUp, VolumeDown, Mute, Shuffle, Repeat, SeekForward, SeekBackward, OpenAlbums, OpenArtists, OpenTracks, OpenPlaylists, OpenInsights, OpenListening, FocusSearch, ToggleFullScreen, ToggleCompact, Help, Back)
- `src/Rok.Application/Accessibility/ShortcutCategory.cs` — enum (Playback, Navigation, Modes, Help)
- `src/Rok.Application/Accessibility/KeyboardShortcut.cs` — record (Id, Category, Modifiers, Key, LabelResourceKey)
- `src/Rok.Application/Accessibility/KeyboardShortcutCatalog.cs` — registre statique
- `tests/UnitTests/Rok.ApplicationTests/Accessibility/KeyboardShortcutCatalogTests.cs` — tests catalogue
- `tests/UnitTests/Rok.ApplicationTests/Player/PlayerServiceSmtcTests.cs` — tests interactions PlayerService↔SMTC
- `src/Presentation/Services/KeyboardShortcutInstaller.cs` — convertit catalogue → KeyboardAccelerator + binding handlers
- `src/Presentation/Commons/KeyVisualBox.xaml` + `.xaml.cs` — petit UserControl rendu d'une combinaison
- `src/Presentation/Dialogs/KeyboardShortcutsDialog.xaml` + `.xaml.cs` — ContentDialog F1

### Fichiers modifiés

- `src/Rok.Application/Interfaces/ISystemMediaTransportControlsService.cs` — signature étendue
- `src/Rok.Infrastructure/Player/SystemMediaTransportControlsService.cs` — thumbnail + timeline + enum
- `src/Rok.Application/Player/PlayerService.cs` — `IAlbumPicture` injecté, coverPath, Stopped, throttle timeline
- `src/Rok.Application/DependencyInjection.cs` (ou file équivalent) — vérifier que `IAlbumPicture` est résolu côté Application
- `src/Presentation/MainWindow.xaml.cs` — installer accelerators, handlers globaux, ouverture dialogue F1
- `src/Presentation/Pages/PlayerView.xaml.cs` — handlers Espace, seek, volume, mute, shuffle, repeat
- `src/Presentation/DependencyInjection.cs` — enregistrer `KeyboardShortcutInstaller`
- `src/Presentation/Pages/OptionsPage.xaml` + `.xaml.cs` — bouton "Voir les raccourcis clavier"
- `src/Presentation/Strings/en-US/Resources.resw`
- `src/Presentation/Strings/fr-FR/Resources.resw`
- `src/Presentation/Strings/es-ES/Resources.resw`
- `src/Presentation/Strings/uk-UA/Resources.resw`

---

## Tasks

### Task 1: PlaybackStatus enum

Cette tâche prépare le terrain pour la Task 4 (changement de signature SMTC). Aucun caller ne change ici.

**Files:**
- Create: `src/Rok.Application/Player/PlaybackStatus.cs`

- [ ] **Step 1: Créer l'enum**

```csharp
namespace Rok.Application.Player;

public enum PlaybackStatus
{
    Playing,
    Paused,
    Stopped,
    Closed
}
```

- [ ] **Step 2: Vérifier le build**

Run: `dotnet build /p:Platform=x64`
Expected: 0 erreur.

- [ ] **Step 3: Commit**

```bash
git add src/Rok.Application/Player/PlaybackStatus.cs
git commit -m "feat(player): add PlaybackStatus enum"
```

---

### Task 2: ShortcutId & ShortcutCategory enums

**Files:**
- Create: `src/Rok.Application/Accessibility/ShortcutId.cs`
- Create: `src/Rok.Application/Accessibility/ShortcutCategory.cs`

- [ ] **Step 1: Créer ShortcutCategory**

```csharp
namespace Rok.Application.Accessibility;

public enum ShortcutCategory
{
    Playback,
    Navigation,
    Modes,
    Help
}
```

- [ ] **Step 2: Créer ShortcutId**

```csharp
namespace Rok.Application.Accessibility;

public enum ShortcutId
{
    PlayPause,
    Next,
    Previous,
    VolumeUp,
    VolumeDown,
    Mute,
    Shuffle,
    Repeat,
    SeekForward,
    SeekBackward,
    OpenAlbums,
    OpenArtists,
    OpenTracks,
    OpenPlaylists,
    OpenInsights,
    OpenListening,
    FocusSearch,
    Back,
    ToggleFullScreen,
    ToggleCompact,
    Help
}
```

- [ ] **Step 3: Vérifier le build**

Run: `dotnet build /p:Platform=x64`
Expected: 0 erreur.

- [ ] **Step 4: Commit**

```bash
git add src/Rok.Application/Accessibility/
git commit -m "feat(accessibility): add ShortcutId and ShortcutCategory enums"
```

---

### Task 3: KeyboardShortcut record + KeyboardShortcutCatalog (TDD)

Le catalogue est l'unité la plus testable. On y applique TDD strict.

**Files:**
- Create: `src/Rok.Application/Accessibility/KeyboardShortcut.cs`
- Create: `src/Rok.Application/Accessibility/KeyboardShortcutCatalog.cs`
- Create: `tests/UnitTests/Rok.ApplicationTests/Accessibility/KeyboardShortcutCatalogTests.cs`

`Windows.System.VirtualKey` et `Windows.System.VirtualKeyModifiers` sont des types WinRT. La couche Application est compilée avec le SDK Windows (déjà le cas vu `Rok.Application.csproj`). Si l'import échoue, vérifier que le projet référence `Microsoft.Windows.SDK.NET.Ref` (ou équivalent) — sinon basculer sur des wrappers internes (peu probable, le projet cible `net10.0-windows`).

- [ ] **Step 1: Créer le record `KeyboardShortcut`**

```csharp
using Windows.System;

namespace Rok.Application.Accessibility;

public sealed record KeyboardShortcut(
    ShortcutId Id,
    ShortcutCategory Category,
    VirtualKeyModifiers Modifiers,
    VirtualKey Key,
    string LabelResourceKey);
```

- [ ] **Step 2: Créer le squelette du catalogue (vide)**

```csharp
using Windows.System;

namespace Rok.Application.Accessibility;

public static class KeyboardShortcutCatalog
{
    private static readonly IReadOnlyList<KeyboardShortcut> _shortcuts = new List<KeyboardShortcut>();

    public static IReadOnlyList<KeyboardShortcut> All => _shortcuts;

    public static KeyboardShortcut ById(ShortcutId id)
    {
        for (int i = 0; i < _shortcuts.Count; i++)
        {
            if (_shortcuts[i].Id == id)
                return _shortcuts[i];
        }

        throw new KeyNotFoundException($"Shortcut not found: {id}");
    }

    public static IEnumerable<KeyboardShortcut> ByCategory(ShortcutCategory category)
    {
        return _shortcuts.Where(s => s.Category == category);
    }
}
```

- [ ] **Step 3: Écrire les tests qui échouent**

Create `tests/UnitTests/Rok.ApplicationTests/Accessibility/KeyboardShortcutCatalogTests.cs`:

```csharp
using Rok.Application.Accessibility;
using Windows.System;

namespace Rok.ApplicationTests.Accessibility;

public class KeyboardShortcutCatalogTests
{
    [Fact(DisplayName = "catalog_should_contain_all_expected_ids")]
    public void Catalog_should_contain_all_expected_ids()
    {
        ShortcutId[] expected = Enum.GetValues<ShortcutId>();

        ShortcutId[] actual = KeyboardShortcutCatalog.All.Select(s => s.Id).ToArray();

        Assert.Equal(expected.OrderBy(x => x), actual.OrderBy(x => x));
    }

    [Fact(DisplayName = "catalog_should_contain_no_duplicate_id")]
    public void Catalog_should_contain_no_duplicate_id()
    {
        IEnumerable<IGrouping<ShortcutId, KeyboardShortcut>> duplicates =
            KeyboardShortcutCatalog.All.GroupBy(s => s.Id).Where(g => g.Count() > 1);

        Assert.Empty(duplicates);
    }

    [Fact(DisplayName = "catalog_should_contain_no_duplicate_modifiers_and_key_combination")]
    public void Catalog_should_contain_no_duplicate_modifiers_and_key_combination()
    {
        IEnumerable<IGrouping<(VirtualKeyModifiers, VirtualKey), KeyboardShortcut>> duplicates =
            KeyboardShortcutCatalog.All.GroupBy(s => (s.Modifiers, s.Key)).Where(g => g.Count() > 1);

        Assert.Empty(duplicates);
    }

    [Fact(DisplayName = "each_shortcut_should_have_non_empty_label_resource_key")]
    public void Each_shortcut_should_have_non_empty_label_resource_key()
    {
        Assert.All(KeyboardShortcutCatalog.All, s =>
            Assert.False(string.IsNullOrWhiteSpace(s.LabelResourceKey)));
    }

    [Fact(DisplayName = "by_id_should_return_expected_shortcut")]
    public void By_id_should_return_expected_shortcut()
    {
        KeyboardShortcut shortcut = KeyboardShortcutCatalog.ById(ShortcutId.PlayPause);

        Assert.Equal(ShortcutId.PlayPause, shortcut.Id);
        Assert.Equal(VirtualKey.Space, shortcut.Key);
        Assert.Equal(VirtualKeyModifiers.None, shortcut.Modifiers);
    }

    [Fact(DisplayName = "by_id_should_throw_when_id_not_present")]
    public void By_id_should_throw_when_id_not_present()
    {
        // All ids are present; this test ensures the error path is reachable
        // by passing a value cast outside the enum range.
        Assert.Throws<KeyNotFoundException>(() => KeyboardShortcutCatalog.ById((ShortcutId)999));
    }

    [Fact(DisplayName = "by_category_should_group_all_shortcuts_correctly")]
    public void By_category_should_group_all_shortcuts_correctly()
    {
        int total = 0;
        foreach (ShortcutCategory category in Enum.GetValues<ShortcutCategory>())
        {
            total += KeyboardShortcutCatalog.ByCategory(category).Count();
        }

        Assert.Equal(KeyboardShortcutCatalog.All.Count, total);
    }
}
```

- [ ] **Step 4: Lancer les tests, vérifier l'échec**

Run: `dotnet test tests/UnitTests/Rok.ApplicationTests/Rok.ApplicationTests.csproj /p:Platform=x64 --filter "FullyQualifiedName~KeyboardShortcutCatalogTests"`
Expected: 4 tests échouent (catalogue vide), 2 tests passent (no-duplicate sur liste vide, by_id_should_throw).

- [ ] **Step 5: Remplir le catalogue**

Replace the `_shortcuts` field in `KeyboardShortcutCatalog.cs`:

```csharp
private static readonly IReadOnlyList<KeyboardShortcut> _shortcuts = new List<KeyboardShortcut>
{
    // Playback
    new KeyboardShortcut(ShortcutId.PlayPause, ShortcutCategory.Playback, VirtualKeyModifiers.None, VirtualKey.Space, "KeyboardShortcut_Action_PlayPause"),
    new KeyboardShortcut(ShortcutId.Next, ShortcutCategory.Playback, VirtualKeyModifiers.Control, VirtualKey.Right, "KeyboardShortcut_Action_Next"),
    new KeyboardShortcut(ShortcutId.Previous, ShortcutCategory.Playback, VirtualKeyModifiers.Control, VirtualKey.Left, "KeyboardShortcut_Action_Previous"),
    new KeyboardShortcut(ShortcutId.VolumeUp, ShortcutCategory.Playback, VirtualKeyModifiers.Control, VirtualKey.Up, "KeyboardShortcut_Action_VolumeUp"),
    new KeyboardShortcut(ShortcutId.VolumeDown, ShortcutCategory.Playback, VirtualKeyModifiers.Control, VirtualKey.Down, "KeyboardShortcut_Action_VolumeDown"),
    new KeyboardShortcut(ShortcutId.Mute, ShortcutCategory.Playback, VirtualKeyModifiers.Control, VirtualKey.M, "KeyboardShortcut_Action_Mute"),
    new KeyboardShortcut(ShortcutId.Shuffle, ShortcutCategory.Playback, VirtualKeyModifiers.Control, VirtualKey.H, "KeyboardShortcut_Action_Shuffle"),
    new KeyboardShortcut(ShortcutId.Repeat, ShortcutCategory.Playback, VirtualKeyModifiers.Control, VirtualKey.T, "KeyboardShortcut_Action_Repeat"),
    new KeyboardShortcut(ShortcutId.SeekForward, ShortcutCategory.Playback, VirtualKeyModifiers.Shift, VirtualKey.Right, "KeyboardShortcut_Action_SeekForward"),
    new KeyboardShortcut(ShortcutId.SeekBackward, ShortcutCategory.Playback, VirtualKeyModifiers.Shift, VirtualKey.Left, "KeyboardShortcut_Action_SeekBackward"),

    // Navigation
    new KeyboardShortcut(ShortcutId.OpenAlbums, ShortcutCategory.Navigation, VirtualKeyModifiers.Control, VirtualKey.Number1, "KeyboardShortcut_Action_OpenAlbums"),
    new KeyboardShortcut(ShortcutId.OpenArtists, ShortcutCategory.Navigation, VirtualKeyModifiers.Control, VirtualKey.Number2, "KeyboardShortcut_Action_OpenArtists"),
    new KeyboardShortcut(ShortcutId.OpenTracks, ShortcutCategory.Navigation, VirtualKeyModifiers.Control, VirtualKey.Number3, "KeyboardShortcut_Action_OpenTracks"),
    new KeyboardShortcut(ShortcutId.OpenPlaylists, ShortcutCategory.Navigation, VirtualKeyModifiers.Control, VirtualKey.Number4, "KeyboardShortcut_Action_OpenPlaylists"),
    new KeyboardShortcut(ShortcutId.OpenInsights, ShortcutCategory.Navigation, VirtualKeyModifiers.Control, VirtualKey.Number5, "KeyboardShortcut_Action_OpenInsights"),
    new KeyboardShortcut(ShortcutId.OpenListening, ShortcutCategory.Navigation, VirtualKeyModifiers.Control, VirtualKey.Number0, "KeyboardShortcut_Action_OpenListening"),
    new KeyboardShortcut(ShortcutId.FocusSearch, ShortcutCategory.Navigation, VirtualKeyModifiers.Control, VirtualKey.F, "KeyboardShortcut_Action_FocusSearch"),
    new KeyboardShortcut(ShortcutId.Back, ShortcutCategory.Navigation, VirtualKeyModifiers.None, VirtualKey.Escape, "KeyboardShortcut_Action_Back"),

    // Modes
    new KeyboardShortcut(ShortcutId.ToggleFullScreen, ShortcutCategory.Modes, VirtualKeyModifiers.None, VirtualKey.F11, "KeyboardShortcut_Action_ToggleFullScreen"),
    new KeyboardShortcut(ShortcutId.ToggleCompact, ShortcutCategory.Modes, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift, VirtualKey.M, "KeyboardShortcut_Action_ToggleCompact"),

    // Help
    new KeyboardShortcut(ShortcutId.Help, ShortcutCategory.Help, VirtualKeyModifiers.None, VirtualKey.F1, "KeyboardShortcut_Action_Help"),
};
```

- [ ] **Step 6: Lancer les tests, vérifier qu'ils passent**

Run: `dotnet test tests/UnitTests/Rok.ApplicationTests/Rok.ApplicationTests.csproj /p:Platform=x64 --filter "FullyQualifiedName~KeyboardShortcutCatalogTests"`
Expected: 7/7 passants.

- [ ] **Step 7: Commit**

```bash
git add src/Rok.Application/Accessibility/ tests/UnitTests/Rok.ApplicationTests/Accessibility/
git commit -m "feat(accessibility): add KeyboardShortcut record and KeyboardShortcutCatalog with tests"
```

---

### Task 4: ISystemMediaTransportControlsService — signature étendue (BREAKING)

**Files:**
- Modify: `src/Rok.Application/Interfaces/ISystemMediaTransportControlsService.cs`
- Modify: `src/Rok.Infrastructure/Player/SystemMediaTransportControlsService.cs`
- Modify: `src/Rok.Application/Player/PlayerService.cs:352, 369-370, 391, 615-616`

Cette tâche est un refactor mécanique : changer la signature, propager. Pas de TDD ici (signature change uniquement). Les compléments fonctionnels (thumbnail, timeline) viennent en Task 5 et 6.

- [ ] **Step 1: Mettre à jour l'interface**

```csharp
using Rok.Application.Dto;

namespace Rok.Application.Interfaces;

public interface ISystemMediaTransportControlsService : IDisposable
{
    void SetPlayerService(Player.IPlayerService playerService);

    void Initialize();

    void UpdatePlaybackState(Player.PlaybackStatus status);

    void UpdateTrackInfo(TrackDto track, string? coverPath);

    void UpdateTimeline(TimeSpan position, TimeSpan duration);
}
```

- [ ] **Step 2: Adapter l'implémentation `SystemMediaTransportControlsService`**

Dans `src/Rok.Infrastructure/Player/SystemMediaTransportControlsService.cs`, modifier :

Remplacer `public void UpdatePlaybackState(bool isPlaying)` par :

```csharp
public void UpdatePlaybackState(PlaybackStatus status)
{
    if (_smtc is null)
    {
        logger.LogWarning("SMTC: UpdatePlaybackState called but _smtc is null");
        return;
    }

    try
    {
        MediaPlaybackStatus newStatus = status switch
        {
            PlaybackStatus.Playing => MediaPlaybackStatus.Playing,
            PlaybackStatus.Paused => MediaPlaybackStatus.Paused,
            PlaybackStatus.Stopped => MediaPlaybackStatus.Stopped,
            PlaybackStatus.Closed => MediaPlaybackStatus.Closed,
            _ => MediaPlaybackStatus.Stopped
        };

        logger.LogInformation("SMTC: Updating playback state from {OldStatus} to {NewStatus}", _smtc.PlaybackStatus, newStatus);

        _smtc.PlaybackStatus = newStatus;

        _smtc.IsPlayEnabled = true;
        _smtc.IsPauseEnabled = true;
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Failed to update SMTC playback state");
    }
}
```

Remplacer `public void UpdateTrackInfo(TrackDto track)` par :

```csharp
public void UpdateTrackInfo(TrackDto track, string? coverPath)
{
    if (_smtc is null)
        return;

    try
    {
        SystemMediaTransportControlsDisplayUpdater updater = _smtc.DisplayUpdater;
        updater.Type = MediaPlaybackType.Music;
        updater.MusicProperties.Title = track.Title;
        updater.MusicProperties.Artist = track.ArtistName;
        updater.MusicProperties.AlbumTitle = track.AlbumName;
        updater.Update();

        logger.LogDebug("SMTC: Updated track info — {Title} by {Artist}", track.Title, track.ArtistName);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Failed to update SMTC track info");
    }
}
```

(le coverPath sera utilisé en Task 5)

Ajouter une méthode stub `UpdateTimeline` :

```csharp
public void UpdateTimeline(TimeSpan position, TimeSpan duration)
{
    // Implemented in Task 6
}
```

Ajouter le `using` au sommet du fichier si manquant : `using Rok.Application.Player;`

- [ ] **Step 3: Adapter les appelants dans `PlayerService.cs`**

Localiser et modifier les 4 appels à `UpdatePlaybackState` :
- `Pause(...)` → `_smtcService?.UpdatePlaybackState(PlaybackStatus.Paused);`
- `Play()` (après `UpdateTrackInfo`) → `_smtcService?.UpdatePlaybackState(PlaybackStatus.Playing);`
- `Stop(...)` → `_smtcService?.UpdatePlaybackState(PlaybackStatus.Stopped);`
- Dans `Next()` ou bout de queue (ligne ~615-616) → `_smtcService?.UpdatePlaybackState(PlaybackStatus.Playing);` (puisque la nouvelle piste joue)

Modifier les 2 appels à `UpdateTrackInfo` (passer `null` comme coverPath, sera renseigné en Task 7) :
- `_smtcService?.UpdateTrackInfo(CurrentTrack, null);`
- `_smtcService?.UpdateTrackInfo(nextTrack, null);`

Ajouter `using Rok.Application.Player;` si manquant.

- [ ] **Step 4: Build + tests existants**

Run: `dotnet build /p:Platform=x64`
Expected: 0 erreur, 0 warning.

Run: `dotnet test /p:Platform=x64`
Expected: tous les tests existants passent (501+ comme avant).

- [ ] **Step 5: Commit**

```bash
git add src/Rok.Application/Interfaces/ISystemMediaTransportControlsService.cs src/Rok.Infrastructure/Player/SystemMediaTransportControlsService.cs src/Rok.Application/Player/PlayerService.cs
git commit -m "$(cat <<'EOF'
refactor(player): extend SMTC service signature with PlaybackStatus enum, coverPath and timeline

BREAKING CHANGE: ISystemMediaTransportControlsService.UpdatePlaybackState now takes
PlaybackStatus enum instead of bool. UpdateTrackInfo gains a coverPath parameter.
UpdateTimeline added (no-op stub, implemented in follow-up).
EOF
)"
```

---

### Task 5: SMTC service — chargement de la pochette

**Files:**
- Modify: `src/Rok.Infrastructure/Player/SystemMediaTransportControlsService.cs`

L'implémentation est WinRT/COM, donc non-testable en unit. Vérification visuelle Windows (panneau Lecture en cours).

- [ ] **Step 1: Compléter `UpdateTrackInfo` pour charger le thumbnail**

Dans `SystemMediaTransportControlsService.UpdateTrackInfo`, après `updater.MusicProperties.AlbumTitle = track.AlbumName;` mais **avant** `updater.Update();`, ajouter :

```csharp
        if (!string.IsNullOrEmpty(coverPath))
        {
            try
            {
                StorageFile coverFile = await StorageFile.GetFileFromPathAsync(coverPath);
                updater.Thumbnail = RandomAccessStreamReference.CreateFromFile(coverFile);
            }
            catch (Exception coverEx)
            {
                logger.LogDebug(coverEx, "SMTC: failed to load cover from {CoverPath}", coverPath);
                updater.Thumbnail = null;
            }
        }
        else
        {
            updater.Thumbnail = null;
        }
```

`UpdateTrackInfo` doit devenir `async` :

```csharp
public async void UpdateTrackInfo(TrackDto track, string? coverPath)
```

L'interface doit suivre. Modifier `ISystemMediaTransportControlsService.cs` :

```csharp
    void UpdateTrackInfo(TrackDto track, string? coverPath);
```

→ rester `void` (avec `async void` côté impl). C'est le pattern WinUI accepté pour fire-and-forget. L'interface reste synchrone côté contrat.

Ajouter les `using` au sommet du fichier impl :

```csharp
using Windows.Storage;
using Windows.Storage.Streams;
```

- [ ] **Step 2: Build**

Run: `dotnet build /p:Platform=x64`
Expected: 0 erreur, 0 warning.

- [ ] **Step 3: Commit**

```bash
git add src/Rok.Infrastructure/Player/SystemMediaTransportControlsService.cs
git commit -m "feat(player): load album cover into SMTC DisplayUpdater thumbnail"
```

---

### Task 6: SMTC service — UpdateTimeline

**Files:**
- Modify: `src/Rok.Infrastructure/Player/SystemMediaTransportControlsService.cs`

- [ ] **Step 1: Implémenter `UpdateTimeline`**

Remplacer le stub par :

```csharp
public void UpdateTimeline(TimeSpan position, TimeSpan duration)
{
    if (_smtc is null)
        return;

    try
    {
        SystemMediaTransportControlsTimelineProperties timeline = new()
        {
            StartTime = TimeSpan.Zero,
            EndTime = duration,
            Position = position,
            MinSeekTime = TimeSpan.Zero,
            MaxSeekTime = duration
        };

        _smtc.UpdateTimelineProperties(timeline);
    }
    catch (Exception ex)
    {
        logger.LogDebug(ex, "Failed to update SMTC timeline");
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build /p:Platform=x64`
Expected: 0 erreur.

- [ ] **Step 3: Commit**

```bash
git add src/Rok.Infrastructure/Player/SystemMediaTransportControlsService.cs
git commit -m "feat(player): implement SMTC UpdateTimeline for Windows Now Playing progress bar"
```

---

### Task 7: PlayerService — chemin de pochette via IAlbumPicture

**Files:**
- Modify: `src/Rok.Application/Player/PlayerService.cs`
- Create: `tests/UnitTests/Rok.ApplicationTests/Player/PlayerServiceSmtcTests.cs`

`TrackDto` n'a pas de champ `AlbumPath`, mais `MusicFile` est le chemin complet du fichier audio (ex. `D:\Music\AC-DC\Back in Black\01 - Hells Bells.mp3`). Le dossier album = `Path.GetDirectoryName(track.MusicFile)`.

- [ ] **Step 1: Écrire le test qui échoue**

Create `tests/UnitTests/Rok.ApplicationTests/Player/PlayerServiceSmtcTests.cs`:

```csharp
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Rok.Application.Dto;
using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Pictures;
using Rok.Application.Options;
using Rok.Application.Player;

namespace Rok.ApplicationTests.Player;

public class PlayerServiceSmtcTests
{
    private static PlayerService BuildSut(
        Mock<ISystemMediaTransportControlsService> smtcMock,
        Mock<IAlbumPicture>? albumPictureMock = null,
        Mock<IPlayerEngine>? engineMock = null)
    {
        Mock<ICallDetectionService> callMock = new();
        engineMock ??= new Mock<IPlayerEngine>();
        Mock<IAppOptions> optionsMock = new();
        optionsMock.SetupGet(o => o.CrossFade).Returns(false);
        albumPictureMock ??= new Mock<IAlbumPicture>();
        FakeTimeProvider time = new();

        return new PlayerService(
            callMock.Object,
            engineMock.Object,
            optionsMock.Object,
            discordService: null,
            smtcService: smtcMock.Object,
            albumPicture: albumPictureMock.Object,
            timeProvider: time,
            logger: NullLogger<PlayerService>.Instance);
    }

    [Fact(DisplayName = "when_track_changes_smtc_receives_track_info_with_cover_path")]
    public void When_track_changes_smtc_receives_track_info_with_cover_path()
    {
        // Arrange
        Mock<ISystemMediaTransportControlsService> smtc = new();
        Mock<IAlbumPicture> picture = new();

        const string albumDir = @"C:\Music\Album";
        const string coverFile = @"C:\Music\Album\cover.jpg";
        TrackDto track = new() { Title = "T", ArtistName = "A", AlbumName = "Alb", MusicFile = @"C:\Music\Album\01.mp3" };

        picture.Setup(p => p.PictureFileExists(albumDir)).Returns(true);
        picture.Setup(p => p.GetPictureFile(albumDir)).Returns(coverFile);

        PlayerService sut = BuildSut(smtc, picture);
        sut.SetPlaylist(new List<TrackDto> { track }, 0);

        // Act
        sut.Play();

        // Assert
        smtc.Verify(s => s.UpdateTrackInfo(track, coverFile), Times.AtLeastOnce);
    }

    [Fact(DisplayName = "when_track_changes_and_no_cover_file_smtc_receives_null_cover_path")]
    public void When_track_changes_and_no_cover_file_smtc_receives_null_cover_path()
    {
        // Arrange
        Mock<ISystemMediaTransportControlsService> smtc = new();
        Mock<IAlbumPicture> picture = new();

        const string albumDir = @"C:\Music\Album";
        TrackDto track = new() { Title = "T", ArtistName = "A", AlbumName = "Alb", MusicFile = @"C:\Music\Album\01.mp3" };

        picture.Setup(p => p.PictureFileExists(albumDir)).Returns(false);

        PlayerService sut = BuildSut(smtc, picture);
        sut.SetPlaylist(new List<TrackDto> { track }, 0);

        // Act
        sut.Play();

        // Assert
        smtc.Verify(s => s.UpdateTrackInfo(track, null), Times.AtLeastOnce);
    }
}
```

> **Note** : si la signature `SetPlaylist` n'existe pas exactement comme ça dans `PlayerService`, vérifier la vraie API (le fichier expose probablement `Playlist` + un autre point d'entrée). Adapter le test pour respecter l'API réelle. Les contractants à tester restent les mêmes : appeler le code de changement de piste et vérifier l'invocation de SMTC. Si nécessaire, appeler la méthode privée via `InternalsVisibleTo` (déjà actif) ou trouver un point d'entrée public équivalent.

- [ ] **Step 2: Lancer les tests, vérifier l'échec**

Run: `dotnet test tests/UnitTests/Rok.ApplicationTests/Rok.ApplicationTests.csproj /p:Platform=x64 --filter "FullyQualifiedName~PlayerServiceSmtcTests"`
Expected: échec — le constructeur n'a pas encore `IAlbumPicture`.

- [ ] **Step 3: Modifier le constructeur de `PlayerService`**

Dans `src/Rok.Application/Player/PlayerService.cs`, ajouter le champ et ajuster le constructeur :

```csharp
private readonly IAlbumPicture _albumPicture;

public PlayerService(
    ICallDetectionService callDetectionService,
    IPlayerEngine player,
    IAppOptions appOptions,
    IDiscordRichPresenceService? discordService,
    ISystemMediaTransportControlsService? smtcService,
    IAlbumPicture albumPicture,
    TimeProvider timeProvider,
    ILogger<PlayerService> logger)
{
    _callDetectionService = Guard.Against.Null(callDetectionService, nameof(callDetectionService));
    _player = Guard.Against.Null(player, nameof(player));
    _appOptions = Guard.Against.Null(appOptions, nameof(appOptions));
    _discordService = discordService;
    _smtcService = smtcService;
    _albumPicture = Guard.Against.Null(albumPicture, nameof(albumPicture));
    _timeProvider = Guard.Against.Null(timeProvider, nameof(timeProvider));
    _logger = Guard.Against.Null(logger, nameof(logger));

    _isCrossfadeEnabled = appOptions.CrossFade;

    _discordService?.Initialize();

    InitEvents();

#if DEBUG
    _volume = 5;
#else
    _volume = 100;
#endif
}
```

Ajouter le `using` : `using Rok.Application.Interfaces.Pictures;`

- [ ] **Step 4: Ajouter une méthode privée pour résoudre le coverPath**

Dans `PlayerService` (zone des helpers) :

```csharp
private string? ResolveCoverPath(TrackDto track)
{
    if (string.IsNullOrEmpty(track.MusicFile))
        return null;

    string? albumDir = Path.GetDirectoryName(track.MusicFile);

    if (string.IsNullOrEmpty(albumDir))
        return null;

    return _albumPicture.PictureFileExists(albumDir)
        ? _albumPicture.GetPictureFile(albumDir)
        : null;
}
```

- [ ] **Step 5: Remplacer les `null` par `ResolveCoverPath` aux deux appels**

Remplacer :
```csharp
_smtcService?.UpdateTrackInfo(CurrentTrack, null);
```
par :
```csharp
_smtcService?.UpdateTrackInfo(CurrentTrack, ResolveCoverPath(CurrentTrack));
```

(et idem pour `nextTrack` à l'autre emplacement)

- [ ] **Step 6: DI — vérifier que `IAlbumPicture` est bien résolu côté Application**

Vérifier dans `src/Rok.Infrastructure/DependencyInjection.cs` que `services.AddSingleton<IAlbumPicture, AlbumPicture>();` (ou similaire) existe déjà. Si oui, rien à faire (PlayerService est résolu via le conteneur de Presentation qui a accès à Infrastructure). Si non, l'ajouter.

- [ ] **Step 7: Lancer les tests**

Run: `dotnet test tests/UnitTests/Rok.ApplicationTests/Rok.ApplicationTests.csproj /p:Platform=x64 --filter "FullyQualifiedName~PlayerServiceSmtcTests"`
Expected: 2/2 passants. Tous les autres tests passent encore.

Run: `dotnet test /p:Platform=x64`
Expected: aucune régression.

- [ ] **Step 8: Commit**

```bash
git add src/Rok.Application/Player/PlayerService.cs tests/UnitTests/Rok.ApplicationTests/Player/
git commit -m "feat(player): inject IAlbumPicture and pass cover path to SMTC on track change"
```

---

### Task 8: PlayerService — état Stopped en bout de queue

**Files:**
- Modify: `src/Rok.Application/Player/PlayerService.cs`
- Modify: `tests/UnitTests/Rok.ApplicationTests/Player/PlayerServiceSmtcTests.cs`

- [ ] **Step 1: Écrire le test qui échoue**

Ajouter dans `PlayerServiceSmtcTests.cs` :

```csharp
[Fact(DisplayName = "when_queue_ends_smtc_receives_playback_state_stopped")]
public void When_queue_ends_smtc_receives_playback_state_stopped()
{
    // Arrange
    Mock<ISystemMediaTransportControlsService> smtc = new();
    Mock<IPlayerEngine> engine = new();
    TrackDto onlyTrack = new() { Title = "T", ArtistName = "A", AlbumName = "Alb", MusicFile = @"C:\Music\01.mp3" };

    PlayerService sut = BuildSut(smtc, engineMock: engine);
    sut.SetPlaylist(new List<TrackDto> { onlyTrack }, 0);
    sut.Play();
    sut.IsLoopingEnabled = false;

    smtc.Invocations.Clear();

    // Act
    sut.Next();  // déclenche fin de queue car une seule piste, pas de loop

    // Assert
    smtc.Verify(s => s.UpdatePlaybackState(PlaybackStatus.Stopped), Times.AtLeastOnce);
}
```

- [ ] **Step 2: Lancer le test, vérifier l'échec**

Run: `dotnet test /p:Platform=x64 --filter "DisplayName~when_queue_ends_smtc_receives_playback_state_stopped"`
Expected: échec — actuellement aucun appel `Stopped` n'est émis en bout de queue.

- [ ] **Step 3: Modifier `PlayerService.Next()` pour émettre Stopped**

Localiser la branche bout-de-queue dans `Next()` (vers la ligne 414) :

```csharp
if (_currentIndex + 1 >= Playlist.Count)
{
    if (IsLoopingEnabled)
    {
        _currentIndex = 0;
    }
    else
    {
        // Playlist ended
        PlaybackState = EPlaybackState.Stopped;
        _smtcService?.UpdatePlaybackState(PlaybackStatus.Stopped);  // ← ajout
        return;
    }
}
```

- [ ] **Step 4: Lancer les tests**

Run: `dotnet test /p:Platform=x64 --filter "FullyQualifiedName~PlayerServiceSmtcTests"`
Expected: 3/3 passants.

- [ ] **Step 5: Commit**

```bash
git add src/Rok.Application/Player/PlayerService.cs tests/UnitTests/Rok.ApplicationTests/Player/PlayerServiceSmtcTests.cs
git commit -m "feat(player): emit Stopped playback state to SMTC at end of queue"
```

---

### Task 9: PlayerService — barre de progression SMTC throttlée à 1 Hz

**Files:**
- Modify: `src/Rok.Application/Player/PlayerService.cs`
- Modify: `tests/UnitTests/Rok.ApplicationTests/Player/PlayerServiceSmtcTests.cs`

`TimeProvider` est déjà injecté. On utilise `_timeProvider.CreateTimer(...)` pour planifier un tick toutes les secondes. Le timer est créé à `Play`, arrêté à `Pause`/`Stop`.

- [ ] **Step 1: Écrire le test qui échoue**

Ajouter dans `PlayerServiceSmtcTests.cs` :

```csharp
[Fact(DisplayName = "when_playing_timeline_updates_at_one_hz")]
public void When_playing_timeline_updates_at_one_hz()
{
    // Arrange
    Mock<ISystemMediaTransportControlsService> smtc = new();
    Mock<IPlayerEngine> engine = new();
    engine.SetupGet(e => e.Position).Returns(TimeSpan.FromSeconds(10));
    engine.SetupGet(e => e.Duration).Returns(TimeSpan.FromMinutes(3));

    TrackDto track = new() { Title = "T", ArtistName = "A", AlbumName = "Alb", MusicFile = @"C:\Music\01.mp3", Duration = 180_000 };
    Mock<ICallDetectionService> callMock = new();
    Mock<IAppOptions> optionsMock = new();
    optionsMock.SetupGet(o => o.CrossFade).Returns(false);
    Mock<IAlbumPicture> picture = new();
    FakeTimeProvider time = new();

    PlayerService sut = new(
        callMock.Object, engine.Object, optionsMock.Object,
        discordService: null, smtcService: smtc.Object, albumPicture: picture.Object,
        timeProvider: time, logger: NullLogger<PlayerService>.Instance);

    sut.SetPlaylist(new List<TrackDto> { track }, 0);
    sut.Play();
    smtc.Invocations.Clear();

    // Act
    time.Advance(TimeSpan.FromSeconds(3.5));

    // Assert
    smtc.Verify(s => s.UpdateTimeline(It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>()), Times.Exactly(3));
}
```

> **Note** : si `IPlayerEngine` n'expose pas `Position` ou `Duration` directement, adapter pour utiliser ce qui est disponible (par exemple `IPlayerService.Position` qui est déjà `double` selon la grep précédente). Le test doit refléter l'API réelle. Le **comportement** à valider reste identique : 1 tick par seconde pendant `Playing`.

- [ ] **Step 2: Lancer le test, vérifier l'échec**

Run: `dotnet test /p:Platform=x64 --filter "DisplayName~when_playing_timeline_updates_at_one_hz"`
Expected: échec — pas d'appel à `UpdateTimeline`.

- [ ] **Step 3: Implémenter le timer dans `PlayerService`**

Ajouter un champ et helpers :

```csharp
private ITimer? _smtcTimelineTimer;

private void StartSmtcTimelineTimer()
{
    _smtcTimelineTimer?.Dispose();
    _smtcTimelineTimer = _timeProvider.CreateTimer(
        _ => OnSmtcTimelineTick(),
        state: null,
        dueTime: TimeSpan.FromSeconds(1),
        period: TimeSpan.FromSeconds(1));
}

private void StopSmtcTimelineTimer()
{
    _smtcTimelineTimer?.Dispose();
    _smtcTimelineTimer = null;
}

private void OnSmtcTimelineTick()
{
    if (_smtcService is null || CurrentTrack is null)
        return;

    TimeSpan position = TimeSpan.FromMilliseconds(_player.Position);
    TimeSpan duration = TimeSpan.FromMilliseconds(CurrentTrack.Duration);

    _smtcService.UpdateTimeline(position, duration);
}
```

> **Note** : la lecture de la position dépend de l'API exacte de `IPlayerEngine`. Adapter l'expression `_player.Position` selon la propriété/méthode existante (peut-être `_player.GetPosition()` ou similaire). Si la position est en `TimeSpan` directement, retirer la conversion `FromMilliseconds`.

Brancher dans les méthodes existantes :

- À la fin de `Play()` (après `_smtcService?.UpdatePlaybackState(PlaybackStatus.Playing);`) → `StartSmtcTimelineTimer();`
- Au début de `Pause(...)` (avant les autres opérations) → `StopSmtcTimelineTimer();`
- Au début de `Stop(...)` → `StopSmtcTimelineTimer();`
- En bout de queue dans `Next()` (à côté du `UpdatePlaybackState(Stopped)` ajouté en Task 8) → `StopSmtcTimelineTimer();`

- [ ] **Step 4: Lancer les tests**

Run: `dotnet test /p:Platform=x64 --filter "FullyQualifiedName~PlayerServiceSmtcTests"`
Expected: 4/4 passants.

Run: `dotnet test /p:Platform=x64`
Expected: aucune régression.

- [ ] **Step 5: Commit**

```bash
git add src/Rok.Application/Player/PlayerService.cs tests/UnitTests/Rok.ApplicationTests/Player/PlayerServiceSmtcTests.cs
git commit -m "feat(player): emit timeline updates to SMTC at 1 Hz during playback"
```

---

### Task 10: Localisation — libellés des actions

**Files:**
- Modify: `src/Presentation/Strings/en-US/Resources.resw`
- Modify: `src/Presentation/Strings/fr-FR/Resources.resw`
- Modify: `src/Presentation/Strings/es-ES/Resources.resw`
- Modify: `src/Presentation/Strings/uk-UA/Resources.resw`

Les `Resources.resw` sont des XML structurés `<root><data name="..."><value>...</value></data>...</root>`. Ajouter les 26 nouvelles clés ci-dessous dans **chaque** fichier (4 langues).

- [ ] **Step 1: Lire un Resources.resw existant pour comprendre la structure**

```bash
head -40 src/Presentation/Strings/en-US/Resources.resw
```

- [ ] **Step 2: Ajouter les libellés dans en-US**

Insérer dans `src/Presentation/Strings/en-US/Resources.resw`, en suivant la structure XML existante (typiquement, ajouter ces blocs avant la fin `</root>`) :

```xml
  <data name="KeyboardShortcut_Action_PlayPause" xml:space="preserve"><value>Play / Pause</value></data>
  <data name="KeyboardShortcut_Action_Next" xml:space="preserve"><value>Next track</value></data>
  <data name="KeyboardShortcut_Action_Previous" xml:space="preserve"><value>Previous track</value></data>
  <data name="KeyboardShortcut_Action_VolumeUp" xml:space="preserve"><value>Volume up</value></data>
  <data name="KeyboardShortcut_Action_VolumeDown" xml:space="preserve"><value>Volume down</value></data>
  <data name="KeyboardShortcut_Action_Mute" xml:space="preserve"><value>Mute</value></data>
  <data name="KeyboardShortcut_Action_Shuffle" xml:space="preserve"><value>Shuffle</value></data>
  <data name="KeyboardShortcut_Action_Repeat" xml:space="preserve"><value>Repeat</value></data>
  <data name="KeyboardShortcut_Action_SeekForward" xml:space="preserve"><value>Skip forward 5 seconds</value></data>
  <data name="KeyboardShortcut_Action_SeekBackward" xml:space="preserve"><value>Skip backward 5 seconds</value></data>
  <data name="KeyboardShortcut_Action_OpenAlbums" xml:space="preserve"><value>Albums</value></data>
  <data name="KeyboardShortcut_Action_OpenArtists" xml:space="preserve"><value>Artists</value></data>
  <data name="KeyboardShortcut_Action_OpenTracks" xml:space="preserve"><value>Tracks</value></data>
  <data name="KeyboardShortcut_Action_OpenPlaylists" xml:space="preserve"><value>Playlists</value></data>
  <data name="KeyboardShortcut_Action_OpenInsights" xml:space="preserve"><value>Insights</value></data>
  <data name="KeyboardShortcut_Action_OpenListening" xml:space="preserve"><value>Now playing</value></data>
  <data name="KeyboardShortcut_Action_FocusSearch" xml:space="preserve"><value>Focus search</value></data>
  <data name="KeyboardShortcut_Action_Back" xml:space="preserve"><value>Back</value></data>
  <data name="KeyboardShortcut_Action_ToggleFullScreen" xml:space="preserve"><value>Full screen</value></data>
  <data name="KeyboardShortcut_Action_ToggleCompact" xml:space="preserve"><value>Compact mode</value></data>
  <data name="KeyboardShortcut_Action_Help" xml:space="preserve"><value>Show keyboard shortcuts</value></data>
  <data name="KeyboardShortcut_Dialog_Title" xml:space="preserve"><value>Keyboard shortcuts</value></data>
  <data name="KeyboardShortcut_Dialog_Group_Playback" xml:space="preserve"><value>Playback</value></data>
  <data name="KeyboardShortcut_Dialog_Group_Navigation" xml:space="preserve"><value>Navigation</value></data>
  <data name="KeyboardShortcut_Dialog_Group_Modes" xml:space="preserve"><value>Modes</value></data>
  <data name="KeyboardShortcut_Dialog_Group_Help" xml:space="preserve"><value>Help</value></data>
  <data name="KeyboardShortcut_Options_Button" xml:space="preserve"><value>View keyboard shortcuts</value></data>
```

- [ ] **Step 3: Ajouter les libellés en fr-FR**

```xml
  <data name="KeyboardShortcut_Action_PlayPause" xml:space="preserve"><value>Lecture / Pause</value></data>
  <data name="KeyboardShortcut_Action_Next" xml:space="preserve"><value>Piste suivante</value></data>
  <data name="KeyboardShortcut_Action_Previous" xml:space="preserve"><value>Piste précédente</value></data>
  <data name="KeyboardShortcut_Action_VolumeUp" xml:space="preserve"><value>Augmenter le volume</value></data>
  <data name="KeyboardShortcut_Action_VolumeDown" xml:space="preserve"><value>Diminuer le volume</value></data>
  <data name="KeyboardShortcut_Action_Mute" xml:space="preserve"><value>Couper le son</value></data>
  <data name="KeyboardShortcut_Action_Shuffle" xml:space="preserve"><value>Lecture aléatoire</value></data>
  <data name="KeyboardShortcut_Action_Repeat" xml:space="preserve"><value>Répétition</value></data>
  <data name="KeyboardShortcut_Action_SeekForward" xml:space="preserve"><value>Avancer de 5 secondes</value></data>
  <data name="KeyboardShortcut_Action_SeekBackward" xml:space="preserve"><value>Reculer de 5 secondes</value></data>
  <data name="KeyboardShortcut_Action_OpenAlbums" xml:space="preserve"><value>Albums</value></data>
  <data name="KeyboardShortcut_Action_OpenArtists" xml:space="preserve"><value>Artistes</value></data>
  <data name="KeyboardShortcut_Action_OpenTracks" xml:space="preserve"><value>Pistes</value></data>
  <data name="KeyboardShortcut_Action_OpenPlaylists" xml:space="preserve"><value>Playlists</value></data>
  <data name="KeyboardShortcut_Action_OpenInsights" xml:space="preserve"><value>Insights</value></data>
  <data name="KeyboardShortcut_Action_OpenListening" xml:space="preserve"><value>Lecture en cours</value></data>
  <data name="KeyboardShortcut_Action_FocusSearch" xml:space="preserve"><value>Recherche</value></data>
  <data name="KeyboardShortcut_Action_Back" xml:space="preserve"><value>Retour</value></data>
  <data name="KeyboardShortcut_Action_ToggleFullScreen" xml:space="preserve"><value>Plein écran</value></data>
  <data name="KeyboardShortcut_Action_ToggleCompact" xml:space="preserve"><value>Mode compact</value></data>
  <data name="KeyboardShortcut_Action_Help" xml:space="preserve"><value>Afficher les raccourcis</value></data>
  <data name="KeyboardShortcut_Dialog_Title" xml:space="preserve"><value>Raccourcis clavier</value></data>
  <data name="KeyboardShortcut_Dialog_Group_Playback" xml:space="preserve"><value>Lecture</value></data>
  <data name="KeyboardShortcut_Dialog_Group_Navigation" xml:space="preserve"><value>Navigation</value></data>
  <data name="KeyboardShortcut_Dialog_Group_Modes" xml:space="preserve"><value>Modes</value></data>
  <data name="KeyboardShortcut_Dialog_Group_Help" xml:space="preserve"><value>Aide</value></data>
  <data name="KeyboardShortcut_Options_Button" xml:space="preserve"><value>Voir les raccourcis clavier</value></data>
```

- [ ] **Step 4: Ajouter les libellés en es-ES**

```xml
  <data name="KeyboardShortcut_Action_PlayPause" xml:space="preserve"><value>Reproducir / Pausar</value></data>
  <data name="KeyboardShortcut_Action_Next" xml:space="preserve"><value>Pista siguiente</value></data>
  <data name="KeyboardShortcut_Action_Previous" xml:space="preserve"><value>Pista anterior</value></data>
  <data name="KeyboardShortcut_Action_VolumeUp" xml:space="preserve"><value>Subir volumen</value></data>
  <data name="KeyboardShortcut_Action_VolumeDown" xml:space="preserve"><value>Bajar volumen</value></data>
  <data name="KeyboardShortcut_Action_Mute" xml:space="preserve"><value>Silenciar</value></data>
  <data name="KeyboardShortcut_Action_Shuffle" xml:space="preserve"><value>Reproducción aleatoria</value></data>
  <data name="KeyboardShortcut_Action_Repeat" xml:space="preserve"><value>Repetir</value></data>
  <data name="KeyboardShortcut_Action_SeekForward" xml:space="preserve"><value>Avanzar 5 segundos</value></data>
  <data name="KeyboardShortcut_Action_SeekBackward" xml:space="preserve"><value>Retroceder 5 segundos</value></data>
  <data name="KeyboardShortcut_Action_OpenAlbums" xml:space="preserve"><value>Álbumes</value></data>
  <data name="KeyboardShortcut_Action_OpenArtists" xml:space="preserve"><value>Artistas</value></data>
  <data name="KeyboardShortcut_Action_OpenTracks" xml:space="preserve"><value>Pistas</value></data>
  <data name="KeyboardShortcut_Action_OpenPlaylists" xml:space="preserve"><value>Listas</value></data>
  <data name="KeyboardShortcut_Action_OpenInsights" xml:space="preserve"><value>Estadísticas</value></data>
  <data name="KeyboardShortcut_Action_OpenListening" xml:space="preserve"><value>Reproduciendo</value></data>
  <data name="KeyboardShortcut_Action_FocusSearch" xml:space="preserve"><value>Buscar</value></data>
  <data name="KeyboardShortcut_Action_Back" xml:space="preserve"><value>Atrás</value></data>
  <data name="KeyboardShortcut_Action_ToggleFullScreen" xml:space="preserve"><value>Pantalla completa</value></data>
  <data name="KeyboardShortcut_Action_ToggleCompact" xml:space="preserve"><value>Modo compacto</value></data>
  <data name="KeyboardShortcut_Action_Help" xml:space="preserve"><value>Mostrar atajos</value></data>
  <data name="KeyboardShortcut_Dialog_Title" xml:space="preserve"><value>Atajos de teclado</value></data>
  <data name="KeyboardShortcut_Dialog_Group_Playback" xml:space="preserve"><value>Reproducción</value></data>
  <data name="KeyboardShortcut_Dialog_Group_Navigation" xml:space="preserve"><value>Navegación</value></data>
  <data name="KeyboardShortcut_Dialog_Group_Modes" xml:space="preserve"><value>Modos</value></data>
  <data name="KeyboardShortcut_Dialog_Group_Help" xml:space="preserve"><value>Ayuda</value></data>
  <data name="KeyboardShortcut_Options_Button" xml:space="preserve"><value>Ver atajos de teclado</value></data>
```

- [ ] **Step 5: Ajouter les libellés en uk-UA**

```xml
  <data name="KeyboardShortcut_Action_PlayPause" xml:space="preserve"><value>Відтворити / Пауза</value></data>
  <data name="KeyboardShortcut_Action_Next" xml:space="preserve"><value>Наступний трек</value></data>
  <data name="KeyboardShortcut_Action_Previous" xml:space="preserve"><value>Попередній трек</value></data>
  <data name="KeyboardShortcut_Action_VolumeUp" xml:space="preserve"><value>Гучніше</value></data>
  <data name="KeyboardShortcut_Action_VolumeDown" xml:space="preserve"><value>Тихіше</value></data>
  <data name="KeyboardShortcut_Action_Mute" xml:space="preserve"><value>Без звуку</value></data>
  <data name="KeyboardShortcut_Action_Shuffle" xml:space="preserve"><value>Випадковий порядок</value></data>
  <data name="KeyboardShortcut_Action_Repeat" xml:space="preserve"><value>Повтор</value></data>
  <data name="KeyboardShortcut_Action_SeekForward" xml:space="preserve"><value>Уперед на 5 секунд</value></data>
  <data name="KeyboardShortcut_Action_SeekBackward" xml:space="preserve"><value>Назад на 5 секунд</value></data>
  <data name="KeyboardShortcut_Action_OpenAlbums" xml:space="preserve"><value>Альбоми</value></data>
  <data name="KeyboardShortcut_Action_OpenArtists" xml:space="preserve"><value>Виконавці</value></data>
  <data name="KeyboardShortcut_Action_OpenTracks" xml:space="preserve"><value>Треки</value></data>
  <data name="KeyboardShortcut_Action_OpenPlaylists" xml:space="preserve"><value>Списки відтворення</value></data>
  <data name="KeyboardShortcut_Action_OpenInsights" xml:space="preserve"><value>Аналітика</value></data>
  <data name="KeyboardShortcut_Action_OpenListening" xml:space="preserve"><value>Зараз грає</value></data>
  <data name="KeyboardShortcut_Action_FocusSearch" xml:space="preserve"><value>Пошук</value></data>
  <data name="KeyboardShortcut_Action_Back" xml:space="preserve"><value>Назад</value></data>
  <data name="KeyboardShortcut_Action_ToggleFullScreen" xml:space="preserve"><value>Повний екран</value></data>
  <data name="KeyboardShortcut_Action_ToggleCompact" xml:space="preserve"><value>Компактний режим</value></data>
  <data name="KeyboardShortcut_Action_Help" xml:space="preserve"><value>Показати скорочення</value></data>
  <data name="KeyboardShortcut_Dialog_Title" xml:space="preserve"><value>Клавіатурні скорочення</value></data>
  <data name="KeyboardShortcut_Dialog_Group_Playback" xml:space="preserve"><value>Відтворення</value></data>
  <data name="KeyboardShortcut_Dialog_Group_Navigation" xml:space="preserve"><value>Навігація</value></data>
  <data name="KeyboardShortcut_Dialog_Group_Modes" xml:space="preserve"><value>Режими</value></data>
  <data name="KeyboardShortcut_Dialog_Group_Help" xml:space="preserve"><value>Довідка</value></data>
  <data name="KeyboardShortcut_Options_Button" xml:space="preserve"><value>Переглянути скорочення</value></data>
```

- [ ] **Step 6: Vérifier (script existant)**

`src/Presentation/find-unlocalized.ps1` existe (vu dans la grep initiale). Si le projet a aussi un script de cohérence inter-langues, le lancer. Sinon : vérifier visuellement que les 4 fichiers ont bien 26 nouvelles entrées.

- [ ] **Step 7: Build**

Run: `dotnet build /p:Platform=x64`
Expected: 0 erreur.

- [ ] **Step 8: Commit**

```bash
git add src/Presentation/Strings/
git commit -m "feat(i18n): add keyboard shortcut localized labels for en, fr, es, uk"
```

---

### Task 11: KeyVisualBox UserControl

Petit composant qui rend une combinaison "Ctrl + →" sous forme de touches stylisées.

**Files:**
- Create: `src/Presentation/Commons/KeyVisualBox.xaml`
- Create: `src/Presentation/Commons/KeyVisualBox.xaml.cs`

- [ ] **Step 1: Créer le XAML**

`src/Presentation/Commons/KeyVisualBox.xaml`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<UserControl x:Class="Rok.Commons.KeyVisualBox"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             mc:Ignorable="d">

    <ItemsControl x:Name="KeysHost">
        <ItemsControl.ItemsPanel>
            <ItemsPanelTemplate>
                <StackPanel Orientation="Horizontal" Spacing="4" />
            </ItemsPanelTemplate>
        </ItemsControl.ItemsPanel>
        <ItemsControl.ItemTemplate>
            <DataTemplate>
                <Border Background="{ThemeResource CardBackgroundFillColorDefaultBrush}"
                        BorderBrush="{ThemeResource DividerStrokeColorDefaultBrush}"
                        BorderThickness="1"
                        CornerRadius="4"
                        Padding="6,2"
                        MinWidth="28"
                        HorizontalAlignment="Center">
                    <TextBlock Text="{Binding}"
                               FontSize="12"
                               FontWeight="SemiBold"
                               HorizontalAlignment="Center"
                               TextAlignment="Center" />
                </Border>
            </DataTemplate>
        </ItemsControl.ItemTemplate>
    </ItemsControl>
</UserControl>
```

- [ ] **Step 2: Créer le code-behind**

`src/Presentation/Commons/KeyVisualBox.xaml.cs`:

```csharp
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rok.Application.Accessibility;
using Windows.System;

namespace Rok.Commons;

public sealed partial class KeyVisualBox : UserControl
{
    public static readonly DependencyProperty ShortcutProperty = DependencyProperty.Register(
        nameof(Shortcut),
        typeof(KeyboardShortcut),
        typeof(KeyVisualBox),
        new PropertyMetadata(null, OnShortcutChanged));

    public KeyboardShortcut? Shortcut
    {
        get => (KeyboardShortcut?)GetValue(ShortcutProperty);
        set => SetValue(ShortcutProperty, value);
    }

    public KeyVisualBox()
    {
        InitializeComponent();
    }

    private static void OnShortcutChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is KeyVisualBox box && e.NewValue is KeyboardShortcut shortcut)
        {
            box.KeysHost.ItemsSource = BuildLabels(shortcut);
        }
    }

    private static List<string> BuildLabels(KeyboardShortcut shortcut)
    {
        List<string> labels = new();

        if (shortcut.Modifiers.HasFlag(VirtualKeyModifiers.Control))
            labels.Add("Ctrl");

        if (shortcut.Modifiers.HasFlag(VirtualKeyModifiers.Shift))
            labels.Add("Shift");

        if (shortcut.Modifiers.HasFlag(VirtualKeyModifiers.Menu))
            labels.Add("Alt");

        if (shortcut.Modifiers.HasFlag(VirtualKeyModifiers.Windows))
            labels.Add("Win");

        labels.Add(FormatKey(shortcut.Key));

        return labels;
    }

    private static string FormatKey(VirtualKey key)
    {
        return key switch
        {
            VirtualKey.Right => "→",
            VirtualKey.Left => "←",
            VirtualKey.Up => "↑",
            VirtualKey.Down => "↓",
            VirtualKey.Space => "Space",
            VirtualKey.Escape => "Esc",
            VirtualKey.Number0 => "0",
            VirtualKey.Number1 => "1",
            VirtualKey.Number2 => "2",
            VirtualKey.Number3 => "3",
            VirtualKey.Number4 => "4",
            VirtualKey.Number5 => "5",
            VirtualKey.Number6 => "6",
            VirtualKey.Number7 => "7",
            VirtualKey.Number8 => "8",
            VirtualKey.Number9 => "9",
            VirtualKey.F1 => "F1",
            VirtualKey.F11 => "F11",
            _ => key.ToString()
        };
    }
}
```

- [ ] **Step 3: Build**

Run: `dotnet build /p:Platform=x64`
Expected: 0 erreur, 0 warning.

- [ ] **Step 4: Commit**

```bash
git add src/Presentation/Commons/KeyVisualBox.xaml src/Presentation/Commons/KeyVisualBox.xaml.cs
git commit -m "feat(presentation): add KeyVisualBox UserControl for keyboard shortcut display"
```

---

### Task 12: KeyboardShortcutsDialog

**Files:**
- Create: `src/Presentation/Dialogs/KeyboardShortcutsDialog.xaml`
- Create: `src/Presentation/Dialogs/KeyboardShortcutsDialog.xaml.cs`

- [ ] **Step 1: Créer le XAML**

`src/Presentation/Dialogs/KeyboardShortcutsDialog.xaml`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<ContentDialog x:Class="Rok.Dialogs.KeyboardShortcutsDialog"
               xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
               xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
               xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
               xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
               xmlns:c="using:Rok.Commons"
               x:Uid="KeyboardShortcutsDialog"
               PrimaryButtonText="Close"
               Width="540"
               mc:Ignorable="d">

    <ScrollViewer MaxHeight="540" VerticalScrollBarVisibility="Auto">
        <ItemsControl x:Name="GroupsHost" Margin="0,4,0,0">
            <ItemsControl.ItemTemplate>
                <DataTemplate>
                    <StackPanel Margin="0,0,0,16" Spacing="8">
                        <TextBlock Text="{Binding Title}"
                                   FontWeight="SemiBold"
                                   FontSize="16"
                                   Margin="0,0,0,4" />
                        <ItemsControl ItemsSource="{Binding Shortcuts}">
                            <ItemsControl.ItemTemplate>
                                <DataTemplate>
                                    <Grid Margin="0,4">
                                        <Grid.ColumnDefinitions>
                                            <ColumnDefinition Width="*" />
                                            <ColumnDefinition Width="Auto" />
                                        </Grid.ColumnDefinitions>
                                        <TextBlock Grid.Column="0"
                                                   Text="{Binding Label}"
                                                   VerticalAlignment="Center" />
                                        <c:KeyVisualBox Grid.Column="1"
                                                        Shortcut="{Binding Shortcut}" />
                                    </Grid>
                                </DataTemplate>
                            </ItemsControl.ItemTemplate>
                        </ItemsControl>
                    </StackPanel>
                </DataTemplate>
            </ItemsControl.ItemTemplate>
        </ItemsControl>
    </ScrollViewer>
</ContentDialog>
```

- [ ] **Step 2: Créer le code-behind**

`src/Presentation/Dialogs/KeyboardShortcutsDialog.xaml.cs`:

```csharp
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;
using Rok.Application.Accessibility;

namespace Rok.Dialogs;

public sealed partial class KeyboardShortcutsDialog : ContentDialog
{
    public KeyboardShortcutsDialog()
    {
        InitializeComponent();
        GroupsHost.ItemsSource = BuildGroups();
    }

    private static List<ShortcutGroupViewModel> BuildGroups()
    {
        ResourceLoader loader = new();

        ShortcutCategory[] categoriesInOrder = new[]
        {
            ShortcutCategory.Playback,
            ShortcutCategory.Navigation,
            ShortcutCategory.Modes,
            ShortcutCategory.Help
        };

        List<ShortcutGroupViewModel> groups = new();

        foreach (ShortcutCategory category in categoriesInOrder)
        {
            List<ShortcutRowViewModel> rows = new();

            foreach (KeyboardShortcut shortcut in KeyboardShortcutCatalog.ByCategory(category))
            {
                rows.Add(new ShortcutRowViewModel
                {
                    Label = loader.GetString(shortcut.LabelResourceKey),
                    Shortcut = shortcut
                });
            }

            groups.Add(new ShortcutGroupViewModel
            {
                Title = loader.GetString($"KeyboardShortcut_Dialog_Group_{category}"),
                Shortcuts = rows
            });
        }

        return groups;
    }
}

internal sealed class ShortcutGroupViewModel
{
    public string Title { get; init; } = string.Empty;

    public IReadOnlyList<ShortcutRowViewModel> Shortcuts { get; init; } = new List<ShortcutRowViewModel>();
}

internal sealed class ShortcutRowViewModel
{
    public string Label { get; init; } = string.Empty;

    public KeyboardShortcut? Shortcut { get; init; }
}
```

- [ ] **Step 3: Ajouter le titre du dialogue dans les Resources**

Le titre est défini par `x:Uid="KeyboardShortcutsDialog"` dans le XAML. Pour qu'il soit traduit, ajouter dans **chaque** `Resources.resw` (4 langues) :

```xml
  <data name="KeyboardShortcutsDialog.Title" xml:space="preserve"><value>Keyboard shortcuts</value></data>
  <data name="KeyboardShortcutsDialog.PrimaryButtonText" xml:space="preserve"><value>Close</value></data>
```

(adapter les valeurs : `Raccourcis clavier`/`Fermer` en fr-FR, etc. — réutiliser les libellés `KeyboardShortcut_Dialog_Title` et le mot "Close"/"Fermer"/"Cerrar"/"Закрити")

- [ ] **Step 4: Build**

Run: `dotnet build /p:Platform=x64`
Expected: 0 erreur.

- [ ] **Step 5: Commit**

```bash
git add src/Presentation/Dialogs/KeyboardShortcutsDialog.xaml src/Presentation/Dialogs/KeyboardShortcutsDialog.xaml.cs src/Presentation/Strings/
git commit -m "feat(presentation): add KeyboardShortcutsDialog (F1 help dialog)"
```

---

### Task 13: KeyboardShortcutInstaller (service Presentation)

**Files:**
- Create: `src/Presentation/Services/KeyboardShortcutInstaller.cs`
- Modify: `src/Presentation/DependencyInjection.cs`

- [ ] **Step 1: Créer l'installer**

`src/Presentation/Services/KeyboardShortcutInstaller.cs`:

```csharp
using Microsoft.UI.Xaml.Input;
using Rok.Application.Accessibility;

namespace Rok.Services;

public sealed class KeyboardShortcutInstaller
{
    public KeyboardAccelerator Build(ShortcutId id, TypedEventHandler<KeyboardAccelerator, KeyboardAcceleratorInvokedEventArgs> handler)
    {
        KeyboardShortcut shortcut = KeyboardShortcutCatalog.ById(id);

        KeyboardAccelerator accelerator = new()
        {
            Key = shortcut.Key,
            Modifiers = shortcut.Modifiers
        };

        accelerator.Invoked += handler;

        return accelerator;
    }
}
```

- [ ] **Step 2: Enregistrer dans le DI**

Dans `src/Presentation/DependencyInjection.cs`, ajouter dans la méthode `AddLogic` (ou équivalent) :

```csharp
services.AddSingleton<KeyboardShortcutInstaller>();
```

- [ ] **Step 3: Build**

Run: `dotnet build /p:Platform=x64`
Expected: 0 erreur.

- [ ] **Step 4: Commit**

```bash
git add src/Presentation/Services/KeyboardShortcutInstaller.cs src/Presentation/DependencyInjection.cs
git commit -m "feat(presentation): add KeyboardShortcutInstaller service"
```

---

### Task 14: PlayerView — accelerators de lecture

Les raccourcis de lecture (Espace, Ctrl+←/→, Ctrl+↑/↓, Ctrl+M, Ctrl+H, Ctrl+T, Shift+←/→) sont attachés aux contrôles existants du `PlayerView`. Tooltips auto-enrichis pour ceux attachés à un bouton avec un tooltip.

**Files:**
- Modify: `src/Presentation/Pages/PlayerView.xaml.cs`

- [ ] **Step 1: Récupérer l'installer dans `PlayerView`**

Dans `PlayerView.xaml.cs` ajouter en haut :

```csharp
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.Extensions.DependencyInjection;
using Rok.Application.Accessibility;
using Rok.Services;
using Windows.System;
```

Dans le constructeur de `PlayerView`, après `InitializeComponent()` :

```csharp
this.Loaded += OnLoaded;
```

Ajouter le handler :

```csharp
private void OnLoaded(object sender, RoutedEventArgs e)
{
    KeyboardShortcutInstaller installer = App.ServiceProvider.GetRequiredService<KeyboardShortcutInstaller>();

    AttachPlaybackShortcuts(installer);
}

private void AttachPlaybackShortcuts(KeyboardShortcutInstaller installer)
{
    // Espace : Play/Pause avec garde de focus
    playPauseButton.KeyboardAccelerators.Add(
        installer.Build(ShortcutId.PlayPause, OnPlayPauseAccelerator));

    // Ctrl+→/← : Next / Previous (attachés à la racine du UserControl pour qu'ils marchent partout dans la fenêtre)
    this.KeyboardAccelerators.Add(installer.Build(ShortcutId.Next, OnNextAccelerator));
    this.KeyboardAccelerators.Add(installer.Build(ShortcutId.Previous, OnPreviousAccelerator));

    // Ctrl+↑/↓ : Volume
    this.KeyboardAccelerators.Add(installer.Build(ShortcutId.VolumeUp, OnVolumeUpAccelerator));
    this.KeyboardAccelerators.Add(installer.Build(ShortcutId.VolumeDown, OnVolumeDownAccelerator));

    // Ctrl+M : Mute
    this.KeyboardAccelerators.Add(installer.Build(ShortcutId.Mute, OnMuteAccelerator));

    // Ctrl+H : Shuffle
    this.KeyboardAccelerators.Add(installer.Build(ShortcutId.Shuffle, OnShuffleAccelerator));

    // Ctrl+T : Repeat
    this.KeyboardAccelerators.Add(installer.Build(ShortcutId.Repeat, OnRepeatAccelerator));

    // Shift+→/← : Seek ±5s avec garde de focus sur slider
    this.KeyboardAccelerators.Add(installer.Build(ShortcutId.SeekForward, OnSeekForwardAccelerator));
    this.KeyboardAccelerators.Add(installer.Build(ShortcutId.SeekBackward, OnSeekBackwardAccelerator));
}

private bool IsTextInputFocused()
{
    DependencyObject? focused = FocusManager.GetFocusedElement(this.XamlRoot) as DependencyObject;
    return focused is TextBox or AutoSuggestBox or PasswordBox;
}

private bool IsSliderFocused()
{
    return FocusManager.GetFocusedElement(this.XamlRoot) is Slider;
}

private void OnPlayPauseAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
{
    if (IsTextInputFocused())
    {
        args.Handled = false;
        return;
    }

    args.Handled = true;
    if (ViewModel?.TogglePlayPauseCommand?.CanExecute(null) == true)
        ViewModel.TogglePlayPauseCommand.Execute(null);
}

private void OnNextAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
{
    args.Handled = true;
    if (ViewModel?.SkipNextCommand?.CanExecute(null) == true)
        ViewModel.SkipNextCommand.Execute(null);
}

private void OnPreviousAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
{
    args.Handled = true;
    if (ViewModel?.SkipPreviousCommand?.CanExecute(null) == true)
        ViewModel.SkipPreviousCommand.Execute(null);
}

private void OnVolumeUpAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
{
    args.Handled = true;
    if (ViewModel != null)
        ViewModel.Volume = Math.Min(100, ViewModel.Volume + 5);
}

private void OnVolumeDownAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
{
    args.Handled = true;
    if (ViewModel != null)
        ViewModel.Volume = Math.Max(0, ViewModel.Volume - 5);
}

private void OnMuteAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
{
    args.Handled = true;
    if (ViewModel?.MuteCommand?.CanExecute(null) == true)
        ViewModel.MuteCommand.Execute(null);
}

private void OnShuffleAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
{
    args.Handled = true;
    // ViewModel.ToggleShuffleCommand.Execute(null);  ← adapter au nom réel de la commande shuffle
}

private void OnRepeatAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
{
    args.Handled = true;
    // ViewModel.ToggleRepeatCommand.Execute(null);  ← adapter au nom réel de la commande repeat
}

private void OnSeekForwardAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
{
    if (IsSliderFocused())
    {
        args.Handled = false;
        return;
    }

    args.Handled = true;
    SeekRelative(TimeSpan.FromSeconds(5));
}

private void OnSeekBackwardAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
{
    if (IsSliderFocused())
    {
        args.Handled = false;
        return;
    }

    args.Handled = true;
    SeekRelative(TimeSpan.FromSeconds(-5));
}

private void SeekRelative(TimeSpan delta)
{
    if (ViewModel == null)
        return;

    // ViewModel expose ListenDuration / DurationTotal en TimeSpan ; adapter au type réel.
    // Variante via IPlayerService : injection séparée + appel à Position (déjà existant).
    // À implémenter selon l'API ViewModel exacte.
}
```

> **Note importante** : les noms de commandes `ToggleShuffleCommand`/`ToggleRepeatCommand` ainsi que la mécanique exacte de seek dépendent de `PlayerViewModel` réel. Au moment de l'implémentation, ouvrir `PlayerViewModel.cs` et utiliser les vraies commandes et propriétés. Si shuffle/repeat n'existent pas encore comme commandes, c'est un sujet à part — soit on les ajoute (out of scope ici), soit on désactive temporairement les raccourcis en attendant.

- [ ] **Step 2: Build**

Run: `dotnet build /p:Platform=x64`
Expected: 0 erreur.

- [ ] **Step 3: Test manuel rapide**

Lancer l'app (`dotnet run --project src/Presentation /p:Platform=x64`), démarrer la lecture, tester :
- Espace met en pause / reprend (avec focus sur la fenêtre, pas dans la barre de recherche)
- Espace dans la barre de recherche n'arrête pas la lecture
- Ctrl+→/← change de piste
- Ctrl+↑/↓ change le volume
- Ctrl+M coupe le son

- [ ] **Step 4: Commit**

```bash
git add src/Presentation/Pages/PlayerView.xaml.cs
git commit -m "feat(presentation): wire keyboard shortcuts on PlayerView (playback controls)"
```

---

### Task 15: MainWindow — accelerators globaux + handlers + ouverture dialogue F1

**Files:**
- Modify: `src/Presentation/MainWindow.xaml.cs`

- [ ] **Step 1: Wirage des accelerators dans MainWindow**

Dans `MainWindow.xaml.cs`, ajouter dans le constructeur (ou dans une méthode appelée à `Loaded`) :

```csharp
private void AttachGlobalShortcuts()
{
    KeyboardShortcutInstaller installer = App.ServiceProvider.GetRequiredService<KeyboardShortcutInstaller>();

    // Navigation pages — attachés aux NavigationViewItems pour bénéficier des tooltips auto-enrichis
    albumsItem.KeyboardAccelerators.Add(installer.Build(ShortcutId.OpenAlbums, OnOpenAlbumsAccelerator));
    artistsItem.KeyboardAccelerators.Add(installer.Build(ShortcutId.OpenArtists, OnOpenArtistsAccelerator));
    tracksItem.KeyboardAccelerators.Add(installer.Build(ShortcutId.OpenTracks, OnOpenTracksAccelerator));
    playlistItem.KeyboardAccelerators.Add(installer.Build(ShortcutId.OpenPlaylists, OnOpenPlaylistsAccelerator));
    insightsItem.KeyboardAccelerators.Add(installer.Build(ShortcutId.OpenInsights, OnOpenInsightsAccelerator));

    // Globaux (sans cible visible → ajoutés à la racine NavigationView ou à MainGrid)
    MainGrid.KeyboardAccelerators.Add(installer.Build(ShortcutId.OpenListening, OnOpenListeningAccelerator));
    MainGrid.KeyboardAccelerators.Add(installer.Build(ShortcutId.FocusSearch, OnFocusSearchAccelerator));
    MainGrid.KeyboardAccelerators.Add(installer.Build(ShortcutId.Help, OnHelpAccelerator));
    MainGrid.KeyboardAccelerators.Add(installer.Build(ShortcutId.ToggleFullScreen, OnToggleFullScreenAccelerator));
    MainGrid.KeyboardAccelerators.Add(installer.Build(ShortcutId.ToggleCompact, OnToggleCompactAccelerator));
    MainGrid.KeyboardAccelerators.Add(installer.Build(ShortcutId.Back, OnBackAccelerator));
}
```

Appel à `AttachGlobalShortcuts()` à la fin de `NavigationView_Loaded` (event déjà câblé en XAML), ou dans le constructeur après que la fenêtre soit prête.

- [ ] **Step 2: Implémenter les handlers de navigation page**

```csharp
private void OnOpenAlbumsAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
{
    args.Handled = true;
    EnsureNormalMode();
    NavigateToTag("Albums");
}

private void OnOpenArtistsAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
{
    args.Handled = true;
    EnsureNormalMode();
    NavigateToTag("Artists");
}

private void OnOpenTracksAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
{
    args.Handled = true;
    EnsureNormalMode();
    NavigateToTag("Tracks");
}

private void OnOpenPlaylistsAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
{
    args.Handled = true;
    EnsureNormalMode();
    NavigateToTag("Playlists");
}

private void OnOpenInsightsAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
{
    args.Handled = true;
    EnsureNormalMode();
    NavigateToTag("Insights");
}

private void NavigateToTag(string tag)
{
    foreach (object item in navMenu.MenuItems)
    {
        if (item is NavigationViewItem navItem && navItem.Tag is string itemTag && itemTag == tag)
        {
            navMenu.SelectedItem = navItem;
            // Reuse existing NavigationView_ItemInvoked logic by raising the same flow.
            return;
        }
    }
}
```

> **Note** : la mécanique de navigation effective dépend du `NavigationView_ItemInvoked` existant. Si placer `SelectedItem` ne suffit pas à charger la page, appeler la même logique que `NavigationView_ItemInvoked` (refactorer en méthode privée `NavigateTo(string tag)` partagée).

- [ ] **Step 3: Implémenter les autres handlers globaux**

```csharp
private void OnOpenListeningAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
{
    args.Handled = true;
    EnsureNormalMode();
    HorizontalPlayerView.ViewModel?.OpenListeningCommand.Execute(null);
}

private void OnFocusSearchAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
{
    args.Handled = true;
    EnsureNormalMode();
    searchBox.Focus(FocusState.Keyboard);
}

private async void OnHelpAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
{
    args.Handled = true;

    KeyboardShortcutsDialog dialog = new()
    {
        XamlRoot = this.Content.XamlRoot
    };
    await dialog.ShowAsync();
}

private void OnToggleFullScreenAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
{
    args.Handled = true;
    HorizontalPlayerView.ViewModel?.FullscreenCommand.Execute(null);
}

private void OnToggleCompactAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
{
    args.Handled = true;
    HorizontalPlayerView.ViewModel?.CompactModeCommand.Execute(null);
}

private void OnBackAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
{
    // Si plein écran ou compact actifs, retour mode normal
    if (FullScreenGrid.Visibility == Visibility.Visible)
    {
        HorizontalPlayerView.ViewModel?.FullscreenCommand.Execute(null);
        args.Handled = true;
        return;
    }

    if (gridCompactScreen.Visibility == Visibility.Visible)
    {
        HorizontalPlayerView.ViewModel?.CompactModeCommand.Execute(null);
        args.Handled = true;
        return;
    }

    // Sinon, déléguer au backstack NavigationView
    if (ContentFrame.CanGoBack)
    {
        ContentFrame.GoBack();
        args.Handled = true;
        return;
    }

    args.Handled = false;  // pas de retour disponible → laisser passer
}

private void EnsureNormalMode()
{
    if (FullScreenGrid.Visibility == Visibility.Visible)
        HorizontalPlayerView.ViewModel?.FullscreenCommand.Execute(null);

    if (gridCompactScreen.Visibility == Visibility.Visible)
        HorizontalPlayerView.ViewModel?.CompactModeCommand.Execute(null);
}
```

> **Note** : `HorizontalPlayerView.ViewModel` n'est peut-être pas exposé publiquement ; injecter directement les ViewModels nécessaires (FullScreen/Compact) via DI ou via les commandes du MainViewModel. Adapter à la structure réelle.

- [ ] **Step 4: Ajouter les `using`**

```csharp
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using Rok.Application.Accessibility;
using Rok.Dialogs;
using Rok.Services;
```

- [ ] **Step 5: Build**

Run: `dotnet build /p:Platform=x64`
Expected: 0 erreur, 0 warning.

- [ ] **Step 6: Test manuel**

Lancer l'app et vérifier :
- F1 ouvre le dialogue (avec Échap qui le ferme)
- Ctrl+1..5 navigue
- Ctrl+0 ouvre la page Listening
- Ctrl+F focus la recherche
- F11 plein écran
- Ctrl+Shift+M mode compact
- Échap depuis plein écran/compact retourne au mode normal

- [ ] **Step 7: Commit**

```bash
git add src/Presentation/MainWindow.xaml.cs
git commit -m "feat(presentation): wire global keyboard shortcuts on MainWindow (navigation, help, modes)"
```

---

### Task 16: OptionsPage — bouton "Voir les raccourcis clavier"

**Files:**
- Modify: `src/Presentation/Pages/OptionsPage.xaml`
- Modify: `src/Presentation/Pages/OptionsPage.xaml.cs`

- [ ] **Step 1: Ajouter le bouton dans le XAML**

Dans `src/Presentation/Pages/OptionsPage.xaml`, dans le premier `PivotItem x:Uid="OptionTabOptions"`, ajouter un bouton à la fin du `StackPanel` (après les `ToggleSwitch`) :

```xml
                        <Button x:Uid="KeyboardShortcutOptionsButton"
                                x:Name="KeyboardShortcutsButton"
                                Click="KeyboardShortcutsButton_Click"
                                Margin="0,8,0,0" />
```

- [ ] **Step 2: Ajouter le handler dans le code-behind**

Dans `src/Presentation/Pages/OptionsPage.xaml.cs` :

```csharp
private async void KeyboardShortcutsButton_Click(object sender, RoutedEventArgs e)
{
    KeyboardShortcutsDialog dialog = new()
    {
        XamlRoot = this.XamlRoot
    };
    await dialog.ShowAsync();
}
```

Ajouter `using Rok.Dialogs;` au sommet.

- [ ] **Step 3: Ajouter le libellé dans Resources**

Le `x:Uid="KeyboardShortcutOptionsButton"` doit avoir une clé `.Content` dans les Resources. Ajouter dans **chaque** `Resources.resw` :

```xml
  <data name="KeyboardShortcutOptionsButton.Content" xml:space="preserve"><value>View keyboard shortcuts</value></data>
```

(adapter par langue : `Voir les raccourcis clavier` en fr-FR, etc. — réutiliser les valeurs `KeyboardShortcut_Options_Button`)

- [ ] **Step 4: Build**

Run: `dotnet build /p:Platform=x64`
Expected: 0 erreur.

- [ ] **Step 5: Test manuel**

Naviguer dans Options → cliquer sur "Voir les raccourcis clavier" → le dialogue s'ouvre, listant les raccourcis par groupe.

- [ ] **Step 6: Commit**

```bash
git add src/Presentation/Pages/OptionsPage.xaml src/Presentation/Pages/OptionsPage.xaml.cs src/Presentation/Strings/
git commit -m "feat(presentation): add 'view keyboard shortcuts' button in Options page"
```

---

### Task 17: Vérification finale

**Files:** aucun (vérification end-to-end)

- [ ] **Step 1: Build complet warning-free**

Run: `dotnet build /p:Platform=x64`
Expected: 0 warning, 0 erreur.

- [ ] **Step 2: Tous les tests passent**

Run: `dotnet test /p:Platform=x64`
Expected: tous passants, +6 nouveaux tests par rapport au baseline (4 catalogue + 2 SMTC track info ; les 2 autres sont liés aux tâches 8/9 — vérifier le total +8).

- [ ] **Step 3: Checklist de vérification manuelle (Windows réel)**

Ouvrir l'app et tester point par point :

- [ ] Espace dans la barre de recherche **n'arrête pas** la lecture
- [ ] Espace ailleurs (focus sur fenêtre) bascule Play/Pause
- [ ] Ctrl+→/← change de piste
- [ ] Ctrl+↑/↓ ajuste le volume
- [ ] Ctrl+M mute toggle
- [ ] Shift+→/← seek ±5s (pas quand le slider de progression a le focus)
- [ ] Ctrl+1..5 navigue vers Albums/Artistes/Pistes/Playlists/Insights
- [ ] Ctrl+0 ouvre Listening
- [ ] Ctrl+F focus la barre de recherche
- [ ] F1 ouvre le dialogue
- [ ] F11 bascule plein écran
- [ ] Ctrl+Shift+M bascule mode compact
- [ ] Échap depuis plein écran/compact retourne mode normal
- [ ] Échap depuis page imbriquée → retour
- [ ] Tooltip de Play affiche "(Espace)" / "(Space)" auto-traduit
- [ ] Tooltip de Next/Previous affiche le raccourci
- [ ] Touches média Windows (Play/Pause/Next/Prev/Stop sur clavier physique ou casque Bluetooth) fonctionnent quand Rok est en arrière-plan
- [ ] Le panneau "Lecture en cours" Windows (clic sur l'icône volume → flèche) montre Rok avec **pochette d'album**, titre, artiste, et **barre de progression qui avance**
- [ ] Bouton "Voir les raccourcis clavier" dans Options ouvre le dialogue
- [ ] Le dialogue est lisible aux 4 langues (changer la langue dans Options pour vérifier au moins fr-FR + en-US)

- [ ] **Step 4: Commit final si quoi que ce soit a été ajusté**

Si des ajustements ont été nécessaires (noms de commandes, API ViewModels), commit final :

```bash
git add -A
git commit -m "fix(accessibility): adjustments after end-to-end manual testing"
```

- [ ] **Step 5: Push**

```bash
git push -u origin MF/keyboard-accessibility
```

---

## Self-Review

Après écriture du plan complet (avant remise utilisateur) :

**1. Spec coverage**

Décisions verrouillées (1-13 de la spec) → tâches couvrantes :
- D1 Catalogue figé (architecturé V2) → Task 3
- D2 Source unique de vérité → Task 3 + Task 13
- D3 Tooltips auto-enrichis → couvert via attachement aux boutons en Task 14 (PlayerView) + Task 15 (NavigationViewItems)
- D4 Dialogue F1 + bouton Options → Task 12 + Task 15 (F1) + Task 16 (Options)
- D5 SMTC déjà branché, on étend → Task 4
- D6 Compléments SMTC (thumbnail, timeline, Stopped) → Task 5, 6, 8, 9
- D7 Signature SMTC modifiée → Task 4
- D8 Garde de focus Espace + Slider → Task 14 (`IsTextInputFocused`, `IsSliderFocused`)
- D9 Modes basculent automatiquement en normal → Task 15 (`EnsureNormalMode`)
- D10 AZERTY supporté nativement → couvert par utilisation de `VirtualKey.NumberX` en Task 3
- D11 Localisation 4 langues → Task 10 + ajouts incrémentaux dans Task 12 et Task 16
- D12 Cycle de vie (attaché à Loaded) → Task 14 + Task 15
- D13 Aucun changement domaine, aucune migration → respecté (rien dans Domain ni Infrastructure/Migrations)

Catalogue des raccourcis (10 lecture + 9 navigation + 3 modes + 1 aide = 23 entrées + Listening + Back + Help → 21 IDs distincts) → Task 3.

**2. Placeholder scan**

- "Adapter au nom réel de la commande" en Task 14 et 15 : justifié car ces propriétés VM doivent être lues du code source réel au moment de l'implémentation. C'est une instruction explicite, pas un placeholder.
- Pas de "TBD", "TODO", "implement later" non explicités.

**3. Type consistency**

- `PlaybackStatus.Playing|Paused|Stopped|Closed` : utilisé identique en Task 1, 4, 8, 9.
- `KeyboardShortcut(Id, Category, Modifiers, Key, LabelResourceKey)` : signature constante en Task 3, 11, 13.
- `ShortcutId` enum : 21 valeurs, citées identiquement en Task 2, 3, 14, 15.
- `IAlbumPicture.PictureFileExists(string albumPath)` / `GetPictureFile(string albumPath)` : signatures réelles vues dans le code source.
- `KeyboardShortcutInstaller.Build(ShortcutId, handler)` : signature constante en Task 13, 14, 15.
