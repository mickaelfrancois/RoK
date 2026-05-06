# Plan : bloquer la navigation pendant l'onboarding premier démarrage

## Contexte

Au premier démarrage (`_dbContext.IsFirstStart == true`), `NavigationView_Loaded` navigue vers `WelcomePage` mais **le `NavigationView` reste actif** — l'utilisateur peut cliquer sur Albums, Artists, etc. et quitter l'onboarding sans avoir sélectionné de dossier. Il se retrouve alors sur une page vide sans aucun moyen visible de revenir ajouter de la musique.

`StartViewModel` gère déjà le cas nominal :
- Import OK (> 0 tracks) → appelle `_navigationService.NavigateToAlbums()` (lignes 86 et 117)
- Import KO (0 tracks) → `ErrorOccurred = true` → carte d'erreur avec bouton "Add Folder" visible

Le seul problème est que rien n'empêche l'utilisateur de quitter `WelcomePage` avant la fin.

## Fichier à modifier : `src/Presentation/MainWindow.xaml.cs`

Seul fichier touché. Trois modifications.

---

### Modification 1 — Ajouter un champ (après ligne 38)

```csharp
private bool _isOnboardingActive = false;
```

---

### Modification 2 — `NavigationView_Loaded` (lignes 145–152)

Remplacer :
```csharp
if (_dbContext.IsFirstStart)
{
    PlaylistsSeed playlistsSeed = App.ServiceProvider.GetRequiredService<PlaylistsSeed>();
    playlistsSeed.SeedAsync().GetAwaiter().GetResult();

    ContentFrame.Navigate(typeof(Pages.WelcomePage), null, new EntranceNavigationTransitionInfo());
    return;
}
```

Par :
```csharp
if (_dbContext.IsFirstStart)
{
    PlaylistsSeed playlistsSeed = App.ServiceProvider.GetRequiredService<PlaylistsSeed>();
    playlistsSeed.SeedAsync().GetAwaiter().GetResult();

    _isOnboardingActive = true;
    navMenu.IsPaneVisible = false;
    ContentFrame.Navigate(typeof(Pages.WelcomePage), null, new EntranceNavigationTransitionInfo());
    return;
}
```

---

### Modification 3 — `ContentFrame_Navigated` (ligne 364)

Ajouter au début de la méthode, avant le bloc `if (e.Content is Pages.OptionsPage)` :

```csharp
if (_isOnboardingActive && e.Content is not Pages.WelcomePage)
{
    _isOnboardingActive = false;
    navMenu.IsPaneVisible = true;
    AttachGlobalShortcuts();
}
```

> **Pourquoi `AttachGlobalShortcuts()` ici ?** Le `return` prématuré dans `NavigationView_Loaded` empêche son appel pour les premiers démarrages. Les raccourcis clavier (Albums, Artists, etc.) ne fonctionneraient jamais pour ces utilisateurs sans ce correctif.

---

## Comportement résultant

| Scénario | Avant | Après |
|---|---|---|
| Premier démarrage | WelcomePage visible, nav cliquable | WelcomePage plein écran, nav masquée |
| Import OK (> 0 tracks) | Navigate vers Albums, nav visible | Navigate vers Albums, nav restaurée + shortcuts |
| Import KO (0 tracks) | Carte "Add Folder" + nav cliquable | Carte "Add Folder", nav toujours masquée |
| Redémarrage normal | Inchangé | Inchangé |

---

## Vérification

```bash
dotnet build /p:Platform=x64
```

Tester manuellement en supprimant la DB SQLite (`%LOCALAPPDATA%\Rok\`) pour simuler un premier démarrage.
