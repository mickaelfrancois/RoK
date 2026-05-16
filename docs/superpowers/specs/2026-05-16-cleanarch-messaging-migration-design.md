# Migration spec — Step 3 : `MiF.SimpleMessenger` (statique) → `CleanArch.DevKit.Messaging` (instance)

> Étape 3/3 du remplacement de la stack MiF par CleanArch.DevKit. Suit `2026-05-16-cleanarch-results-migration-design.md` (étape 2, Result). Clôt l'initiative.

## Contexte

`MiF.SimpleMessenger` 1.0.0 expose un bus statique `Messenger.Send/Subscribe/Unsubscribe`. Cette approche est :
- Pratique mais non-DI (singleton global implicite)
- Pas d'IDisposable : la responsabilité de tracker les Action handlers et appeler Unsubscribe est sur le caller, fragile
- Pas async : pas de `SendAsync`/`SubscribeAsync`
- Difficile à tester proprement (pas de point d'injection / d'isolation)

`CleanArch.DevKit.Messaging` 1.0.0 expose un bus **instance** via DI :
- `IMessenger` interface
- `Send<T>(msg)` synchrone + `SendAsync<T>(msg, ct)` asynchrone
- `Subscribe<T>(handler)` → `IDisposable` (dispose pour unsubscribe)
- `SubscribeAsync<T>(handler)` → `IDisposable`
- Deux implémentations : `Messenger` (strong refs) et `WeakMessenger` (weak refs, mais piège avec lambdas)

## Écart d'API

| MiF.SimpleMessenger (statique) | CleanArch.DevKit.Messaging (instance via DI) |
|---|---|
| `Messenger.Send<T>(msg)` | `_messenger.Send<T>(msg)` |
| `Messenger.Subscribe<T>(action)` | `IDisposable token = _messenger.Subscribe<T>(action)` |
| `Messenger.Unsubscribe<T>(action)` | `token.Dispose()` |
| `IMessenger` (interface avec `static abstract`) | `IMessenger` (interface instance classique) |

## Décisions (validées par le user)

1. **`AddMessenger`** (strong refs) plutôt que `AddWeakMessenger` :
   - Comportement prévisible
   - Exige `IDisposable` discipline mais évite les bugs latents de subscribers GC'd silencieusement
2. **Injection IMessenger dans constructeurs des `.xaml.cs`** :
   - `MainWindow` : déjà DI, ajouter paramètre
   - `PlayerView`, `NotificationControl` : default ctor instancié par XAML → `App.ServiceProvider.GetRequiredService<IMessenger>()` (pattern déjà utilisé pour `PlayerViewModel`, `KeyboardShortcutInstaller`, etc.)
3. **Cumul des subscriptions** :
   - `List<IDisposable> _subscriptions = new();` par classe avec plusieurs Subscribe
   - Dispose itère et appelle `.Dispose()` sur chaque

## Scope (audit)

**Production source — Send only (~17 occurrences) :**
- `Rok.Application/Player/PlayerService.cs` : 10 Send
- `Rok.Application/Features/Playlists/Requests/ImportPlaylistRequestHandler.cs` : 1 Send
- `Rok.Infrastructure/Repositories/TagRepository.cs` : 1 Send
- `Rok.Import/Services/ImportProgressService.cs` : 5 Send
- `Rok.Import/Services/FolderImportProcessor.cs` : 2 Send

**Production source — Subscribe (need IDisposable management) :**
- Presentation Services : `PlaylistMenuService` (4), `TagsProvider` (1)
- Presentation Library Monitors : `TrackLibraryMonitor` (2 sub + 2 unsub), `AlbumLibraryMonitor` (4+4), `ArtistLibraryMonitor` (4+4)
- Presentation ViewModels : `PlaylistsViewModel` (2), `StartViewModel` (2+2), `ListeningViewModel` (2), `PlayerViewModel` (6), `SearchSuggestionsViewModel` (1+1), `MainViewModel` (1), `TrackViewModel` (1+1)
- Presentation `.xaml.cs` : `MainWindow` (5), `PlayerView` (1), `NotificationControl` (1)

**Production source — mixte Send + Subscribe :**
- Plusieurs ViewModels (Track/Album/Artist) — déjà couverts ci-dessus

**Tests (~32 occurrences) :**
- `Rok.ApplicationTests/Features/Playlists/Requests/ImportPlaylistCommandHandlerTests.cs` : 1 sub + 1 unsub
- `Rok.PresentationTests/Services/PlaylistMenuServiceTests.cs` : 7 sub/unsub pairs
- `Rok.PresentationTests/ViewModels/Playlists/Services/PlaylistImportServiceTests.cs` : 6 sub/unsub pairs
- `Rok.ImportTests/Services/ImportProgressServiceTests.cs` : 1 sub

**Total :** 32 fichiers, 168 occurrences (audit grep)

## Stratégie pour les tests

Trois patterns selon le test :

1. **Tests qui produisent un message et vérifient qu'un service appelle Send** : mocker `Mock<IMessenger>` + `Verify(m => m.Send(...))`
2. **Tests qui testent le routing pub/sub** (Subscribe + déclencheur + assert handler called) : créer un `Messenger` local (`IMessenger messenger = new Messenger();`) et l'injecter via constructeur du service testé
3. **Tests existants qui font `Messenger.Subscribe(...)` pour intercepter les Send d'un service** : remplacer par un `Messenger` partagé entre le test et le service

## Risques

1. **Lifecycle des subscriptions** : oublier de Dispose dans un Transient/Scoped service → leak. Mitigation : audit grep `Subscribe` final pour vérifier qu'il y a un Dispose correspondant.
2. **Pages/Windows WinUI 3** : single-instance par app, leurs subscriptions vivent toute l'app — pas de leak en pratique, mais bonne pratique d'ajouter `IDisposable`.
3. **Tests partagent un état statique** : avec `Messenger.Subscribe` statique, les tests pouvaient interférer si parallélisés. Avec `IMessenger` instance, c'est résolu — bonus de la migration.
4. **Bug latent dans `MainWindow.xaml.cs:89-93`** : 5 Subscribe sans Unsubscribe correspondant. Pas de leak en pratique (Window vit toute l'app) mais on en profite pour ajouter le tracking proprement.

## Critères d'acceptation

- `dotnet build Rok.slnx /p:Platform=x64` → 0 erreur, 0 warning
- `dotnet test Rok.slnx /p:Platform=x64 --no-build` → tous les tests verts (1284 attendus)
- `grep -r "MiF.SimpleMessenger"` → 0 occurrence dans `src/` et `tests/`
- `grep -r "Messenger\\.Send"` / `Messenger\\.Subscribe` / `Messenger\\.Unsubscribe` → 0 occurrence statique (que des `_messenger.Send` etc.)
- Smoke test WinUI manuel : notifications + navigation + playback + import + library refresh fonctionnent
