# Implementation plan — Step 3 : `MiF.SimpleMessenger` → `CleanArch.DevKit.Messaging`

> Spec : `2026-05-16-cleanarch-messaging-migration-design.md`. Branche stacked : `refactor/migrate-to-cleanarch-messaging` (sur `refactor/migrate-to-cleanarch-results`).

## T1. Foundation — package + GlobalUsings + DI

**Fichiers :**
- `src/Rok.Application/Rok.Application.csproj` : retirer `MiF.Messenger`, ajouter `CleanArch.DevKit.Messaging` 1.0.0
- `src/Presentation/Rok.csproj` : si `MiF.Messenger` est listé, idem
- `src/Rok.Infrastructure/Rok.Infrastructure.csproj` : idem si présent
- `src/Rok.Import/Rok.Import.csproj` : idem si présent
- `src/Rok.Application/GlobalUsings.cs` : `MiF.SimpleMessenger` → `CleanArch.DevKit.Messaging`
- `src/Presentation/GlobalUsings.cs` : `MiF.SimpleMessenger` → `CleanArch.DevKit.Messaging`
- `tests/UnitTests/Rok.PresentationTests/GlobalUsings.cs` : ajouter `CleanArch.DevKit.Messaging`
- `tests/UnitTests/Rok.ApplicationTests/GlobalUsings.cs` : ajouter `CleanArch.DevKit.Messaging` (pour les tests Subscribe)
- `tests/UnitTests/Rok.ImportTests/GlobalUsings.cs` : ajouter `CleanArch.DevKit.Messaging`
- `src/Rok.Application/DependencyInjection.cs` : ajouter `services.AddMessenger()`

**Commit** : `refactor(app): swap MiF.SimpleMessenger for CleanArch.DevKit.Messaging foundation`

## T2. Migrate `Rok.Application` Send sites

Fichiers :
- `Player/PlayerService.cs` — inject `IMessenger _messenger` via constructor primary (déjà a un primary constructor probable) ; remplacer 10 `Messenger.Send(...)` par `_messenger.Send(...)`
- `Features/Playlists/Requests/ImportPlaylistRequestHandler.cs` — inject `IMessenger` dans le primary constructor records-handler ; remplacer 1 Send

Vérifier : registration du `IPlayerService` dans Application/DI déjà compatible (singleton).

**Commit** : `refactor(app): inject IMessenger into PlayerService and ImportPlaylistRequestHandler`

## T3. Migrate `Rok.Infrastructure` + `Rok.Import` Send sites

Fichiers :
- `Rok.Infrastructure/Repositories/TagRepository.cs` — inject `IMessenger` ; 1 Send
- `Rok.Import/Services/ImportProgressService.cs` — inject `IMessenger` ; 5 Send
- `Rok.Import/Services/FolderImportProcessor.cs` — inject `IMessenger` ; 2 Send

**Commit** : `refactor: inject IMessenger into Infrastructure + Import services`

## T4. Migrate Presentation Library Monitors (Subscribe with IDisposable)

Fichiers (3 fichiers, pattern symétrique) :
- `ViewModels/Tracks/Services/TrackLibraryMonitor.cs`
- `ViewModels/Albums/Services/AlbumLibraryMonitor.cs`
- `ViewModels/Artists/Services/ArtistLibraryMonitor.cs`

Pattern :
```csharp
// Avant
public class XxxLibraryMonitor : IXxxLibraryMonitor, IDisposable
{
    public XxxLibraryMonitor(...) {
        Messenger.Subscribe<...>(_handler.Handle);
        ...
    }
    public void Dispose() {
        Messenger.Unsubscribe<...>(_handler.Handle);
        ...
    }
}

// Après
public class XxxLibraryMonitor : IXxxLibraryMonitor, IDisposable
{
    private readonly IMessenger _messenger;
    private readonly List<IDisposable> _subscriptions = new();

    public XxxLibraryMonitor(IMessenger messenger, ...) {
        _messenger = messenger;
        _subscriptions.Add(_messenger.Subscribe<...>(_handler.Handle));
        ...
    }
    public void Dispose() {
        foreach (var sub in _subscriptions) sub.Dispose();
        _subscriptions.Clear();
        GC.SuppressFinalize(this);
    }
}
```

**Commit** : `refactor(presentation): migrate Library Monitors to IMessenger with IDisposable`

## T5. Migrate Presentation Services (`PlaylistMenuService`, `TagsProvider`)

- `PlaylistMenuService.cs` : déjà `IDisposable` (a `_cacheSemaphore`). Ajouter `List<IDisposable> _subscriptions` + dispose dans `Dispose()`.
- `TagsProvider.cs` : `Messenger.Subscribe<TagUpdatedMessage>` au constructor sans dispose actuel. Ajouter `IDisposable`.

**Commit** : `refactor(presentation): migrate PlaylistMenuService + TagsProvider to IMessenger`

## T6. Migrate Presentation ViewModels with Subscribe

Fichiers :
- `ViewModels/Player/PlayerViewModel.cs` (6 Subscribe)
- `ViewModels/Main/MainViewModel.cs` (1)
- `ViewModels/Main/SearchSuggestionsViewModel.cs` (1+1 unsub)
- `ViewModels/Start/StartViewModel.cs` (2+2 unsub)
- `ViewModels/Playlists/PlaylistsViewModel.cs` (2)
- `ViewModels/Listening/ListeningViewModel.cs` (2)
- `ViewModels/Track/TrackViewModel.cs` (1+1 unsub)

Pour chaque : inject `IMessenger`, `List<IDisposable> _subscriptions`, `Dispose()` méthode. Les VMs héritant de `ObservableObject` doivent implémenter `IDisposable` explicitement.

**Commit** : `refactor(presentation): migrate ViewModels to IMessenger with IDisposable`

## T7. Migrate Presentation `.xaml.cs` files

- `MainWindow.xaml.cs` : déjà DI-injected, ajouter `IMessenger messenger` au constructor, remplacer 5 Subscribe + 2 Send
- `Pages/PlayerView.xaml.cs` : default ctor → `App.ServiceProvider.GetRequiredService<IMessenger>()`, remplacer 1 Subscribe
- `Commons/NotificationControl.xaml.cs` : default ctor → idem, 1 Subscribe + 1 Send

**Commit** : `refactor(presentation): migrate WinUI .xaml.cs to IMessenger`

## T8. Migrate Send-only Presentation files

Fichiers (Send seulement, pas Subscribe — facile) :
- `ViewModels/Album/AlbumViewModel.cs` (6 Send)
- `ViewModels/Artist/ArtistViewModel.cs` (5 Send)
- `ViewModels/Track/TrackViewModel.cs` (déjà T6, mais aussi Send sites)
- `ViewModels/Track/Services/TrackScoreService.cs` (1 Send)
- `Services/PlaylistMenuService.cs` (déjà T5)
- `ViewModels/Playlist/Services/PlaylistUpdateService.cs` (5 Send)
- `ViewModels/Playlist/Services/PlaylistExportService.cs` (2 Send)
- `ViewModels/Playlists/Services/PlaylistImportService.cs` (1 Send)
- `ViewModels/Playlists/Services/PlaylistCreationService.cs` (2 Send)

Tous les sites où on n'a que du Send : inject `IMessenger`, remplacer `Messenger.Send(...)` par `_messenger.Send(...)`.

**Commit** : `refactor(presentation): inject IMessenger into Send-only sites`

## T9. Migrate tests

**Application tests :**
- `Features/Playlists/Requests/ImportPlaylistCommandHandlerTests.cs` : utilise `Messenger.Subscribe<PlaylistImportedMessage>` pour intercepter. Remplacer par `IMessenger messenger = new Messenger();` partagé entre test et handler, capture des sends via `messenger.Subscribe(received.Add)`.

**Import tests :**
- `Services/ImportProgressServiceTests.cs` : idem pattern.

**Presentation tests :**
- `Services/PlaylistMenuServiceTests.cs` : 7 paires sub/unsub. Remplacer par `Messenger` local + Subscribe + Dispose (try/finally déjà en place).
- `ViewModels/Playlists/Services/PlaylistImportServiceTests.cs` : 6 paires sub/unsub. Idem.

**Commit** : `test: migrate test setups to IMessenger instance`

## T10. Build + tests + audit grep

```bash
dotnet build Rok.slnx /p:Platform=x64       # expect 0 errors/warnings
dotnet test Rok.slnx /p:Platform=x64 --no-build  # expect 1284 green
grep -r "MiF.SimpleMessenger" src/ tests/    # expect 0
grep -r "Messenger\.Send\b" src/ tests/      # expect 0 (statique)
grep -r "Messenger\.Subscribe" src/ tests/   # expect 0 (statique)
grep -r "Messenger\.Unsubscribe" src/ tests/ # expect 0
```

## T11. Smoke test WinUI manuel (utilisateur)

- Lancement, notifications (créer une playlist → notification "Playlist créée")
- Navigation entre Albums/Artists/Playlists (library refresh msg)
- Playback : démarrer un track, observer MediaChangedMessage propage à PlayerView (animations)
- Import library
- Tag edit (TagUpdatedMessage)
- Plein écran toggle / compact mode toggle (FullScreenMessage / CompactModeMessage)

## T12. Squash + PR

`git reset --soft refactor/migrate-to-cleanarch-results` + commit unique avec message Conventional Commits descriptif. Push + PR (stacked sur step 2).
