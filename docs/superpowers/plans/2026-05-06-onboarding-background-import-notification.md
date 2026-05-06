# Onboarding Background Import Notification — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Pendant l'onboarding, dès que 100 albums sont découverts, naviguer automatiquement vers l'app et afficher une notification informant l'utilisateur que l'import continue en tâche de fond.

**Architecture:** Une constante dédiée `KMinAlbumsToUnlockApp = 100` remplace la référence directe à `ImportMessageThrottler.MaxMessagesBeforeThrottle` dans `StartViewModel`. Les deux valeurs sont actuellement égales mais doivent rester indépendantes : le throttle contrôle le débit de messages UI, l'unlock contrôle l'UX. Invariant à respecter : `MaxMessagesBeforeThrottle >= KMinAlbumsToUnlockApp`. Après navigation, `Messenger.Send(ShowNotificationMessage)` déclenche le `NotificationControl` existant (InfoBar, auto-masquage 5 s).

**Tech Stack:** C# 13, WinUI 3, CommunityToolkit.Mvvm, MiF.SimpleMessenger, `.resw` pour la localisation (en-US · fr-FR · es-ES · uk-UA)

---

## Contexte — état actuel

### `StartViewModel` (src/Presentation/ViewModels/Start/StartViewModel.cs)

- Ligne 15 : `private const int KDisplayIntervalMs = 300;`
- Ligne 81 : `ImportProgress = Math.Min(AlbumsImported.Count * 100.0 / ImportMessageThrottler.MaxMessagesBeforeThrottle, 100);`
- Ligne 83 : `if (AlbumsImported.Count >= ImportMessageThrottler.MaxMessagesBeforeThrottle)` — navigue à **100 albums** (référence directe au throttle, à découpler)
- Ligne 53 : `_albumsImportedMessage = resourceService.GetString("AlbumsImported");` — modèle à suivre pour charger les nouvelles chaînes

### Système de notification existant
- `ShowNotificationMessage` (`src/Rok.Application/Messages/ShowNotificationMessage.cs`) : propriétés `Title`, `Message`, `Type (NotificationType)`
- `NotificationControl` : abonné globalement dans `MainWindow`, affiche une InfoBar qui se masque après 5 s
- Exemple d'usage : `Messenger.Send(new ShowNotificationMessage { Message = ..., Type = NotificationType.Success });`

### Seuil du throttler
- `ImportMessageThrottler.MaxMessagesBeforeThrottle = 100` : arrête l'envoi de `AlbumImportedMessage` après 100 messages.
- **Invariant :** `MaxMessagesBeforeThrottle >= KMinAlbumsToUnlockApp`. Si le throttle coupe les messages avant que le seuil d'unlock soit atteint, la navigation ne se déclencherait jamais depuis le timer — elle tomberait en fallback sur le handler `Stop`. Les deux constantes sont actuellement égales (100), ce qui est valide.

---

## Fichiers touchés

| Fichier | Action |
|---|---|
| `src/Presentation/Strings/en-US/Resources.resw` | Modifier — 2 nouvelles clés |
| `src/Presentation/Strings/fr-FR/Resources.resw` | Modifier — 2 nouvelles clés |
| `src/Presentation/Strings/es-ES/Resources.resw` | Modifier — 2 nouvelles clés |
| `src/Presentation/Strings/uk-UA/Resources.resw` | Modifier — 2 nouvelles clés |
| `src/Presentation/ViewModels/Start/StartViewModel.cs` | Modifier — constante + assert + champs + logique timer |

> **Pas de tests unitaires** : `StartViewModel` dépend de `DispatcherQueue` (thread UI WinUI 3) et de `Messenger` statique global — non testable en headless sans refactoring majeur hors scope. Vérification manuelle décrite en fin de plan.

---

## Task 1 : Chaînes de localisation

**Fichiers :**
- Modifier : `src/Presentation/Strings/en-US/Resources.resw`
- Modifier : `src/Presentation/Strings/fr-FR/Resources.resw`
- Modifier : `src/Presentation/Strings/es-ES/Resources.resw`
- Modifier : `src/Presentation/Strings/uk-UA/Resources.resw`

Ajouter les deux clés **juste après** le bloc `AlbumsImported` (ligne ~1574 dans chaque fichier).

- [ ] **Step 1 : Ajouter les clés en-US**

Dans `src/Presentation/Strings/en-US/Resources.resw`, après la ligne `</data>` qui ferme `AlbumsImported` :

```xml
  <data name="notification_import_background_title" xml:space="preserve">
    <value>Import in progress</value>
  </data>
  <data name="notification_import_background_message" xml:space="preserve">
    <value>Your first albums are ready. The import continues in the background.</value>
  </data>
```

- [ ] **Step 2 : Ajouter les clés fr-FR**

Dans `src/Presentation/Strings/fr-FR/Resources.resw`, au même emplacement :

```xml
  <data name="notification_import_background_title" xml:space="preserve">
    <value>Import en cours</value>
  </data>
  <data name="notification_import_background_message" xml:space="preserve">
    <value>Vos premiers albums sont prêts. L'import continue en tâche de fond.</value>
  </data>
```

- [ ] **Step 3 : Ajouter les clés es-ES**

Dans `src/Presentation/Strings/es-ES/Resources.resw`, au même emplacement :

```xml
  <data name="notification_import_background_title" xml:space="preserve">
    <value>Importación en curso</value>
  </data>
  <data name="notification_import_background_message" xml:space="preserve">
    <value>Tus primeros álbumes están listos. La importación continúa en segundo plano.</value>
  </data>
```

- [ ] **Step 4 : Ajouter les clés uk-UA**

Dans `src/Presentation/Strings/uk-UA/Resources.resw`, au même emplacement :

```xml
  <data name="notification_import_background_title" xml:space="preserve">
    <value>Імпорт триває</value>
  </data>
  <data name="notification_import_background_message" xml:space="preserve">
    <value>Ваші перші альбоми готові. Імпорт продовжується у фоновому режимі.</value>
  </data>
```

- [ ] **Step 5 : Build pour valider les ressources**

```bash
dotnet build /p:Platform=x64 -v quiet
```

Attendu : `0 Erreur(s)  0 Avertissement(s)`

- [ ] **Step 6 : Commit**

```bash
git add src/Presentation/Strings/en-US/Resources.resw \
        src/Presentation/Strings/fr-FR/Resources.resw \
        src/Presentation/Strings/es-ES/Resources.resw \
        src/Presentation/Strings/uk-UA/Resources.resw
git commit -m "feat(onboarding): add localization strings for background import notification"
```

---

## Task 2 : Logique StartViewModel — constante dédiée + invariant + notification

**Fichiers :**
- Modifier : `src/Presentation/ViewModels/Start/StartViewModel.cs`

- [ ] **Step 1 : Ajouter la constante et les champs pour les chaînes**

Dans le bloc des constantes (après `private const int KDisplayIntervalMs = 300;` ligne 15), ajouter :

```csharp
// Must stay <= ImportMessageThrottler.MaxMessagesBeforeThrottle (currently 100).
// If unlock > throttle, AlbumImportedMessage stops arriving before the threshold is reached
// and navigation falls back to the LibraryRefreshMessage Stop handler without notification.
private const int KMinAlbumsToUnlockApp = 100;
```

Dans le bloc des champs privés (après `private readonly string _albumsImportedMessage` ligne 27), ajouter :

```csharp
private readonly string _importBackgroundTitle;
private readonly string _importBackgroundMessage;
```

- [ ] **Step 2 : Ajouter un Debug.Assert sur l'invariant dans le constructeur**

Au tout début du constructeur (avant les assignments), ajouter :

```csharp
Debug.Assert(
    ImportMessageThrottler.MaxMessagesBeforeThrottle >= KMinAlbumsToUnlockApp,
    $"Throttle ({ImportMessageThrottler.MaxMessagesBeforeThrottle}) must be >= unlock threshold ({KMinAlbumsToUnlockApp}).");
```

`Debug` est dans `System.Diagnostics` — vérifier que ce namespace est couvert par les global usings du projet (très probable) ou ajouter `using System.Diagnostics;` si absent.

- [ ] **Step 3 : Charger les chaînes dans le constructeur**

Après la ligne existante :
```csharp
_albumsImportedMessage = resourceService.GetString("AlbumsImported");
```

Ajouter :
```csharp
_importBackgroundTitle = resourceService.GetString("notification_import_background_title");
_importBackgroundMessage = resourceService.GetString("notification_import_background_message");
```

- [ ] **Step 4 : Mettre à jour OnDisplayTimerTick**

Remplacer les lignes 81–87 :

```csharp
// AVANT
ImportProgress = Math.Min(AlbumsImported.Count * 100.0 / ImportMessageThrottler.MaxMessagesBeforeThrottle, 100);

if (AlbumsImported.Count >= ImportMessageThrottler.MaxMessagesBeforeThrottle)
{
    UnregisterEvents();
    _navigationService.NavigateToAlbums();
}
```

Par :

```csharp
// APRÈS
ImportProgress = Math.Min(AlbumsImported.Count * 100.0 / KMinAlbumsToUnlockApp, 100);

if (AlbumsImported.Count >= KMinAlbumsToUnlockApp)
{
    UnregisterEvents();
    _navigationService.NavigateToAlbums();
    Messenger.Send(new ShowNotificationMessage
    {
        Title = _importBackgroundTitle,
        Message = _importBackgroundMessage,
        Type = NotificationType.Informational
    });
}
```

> **Pourquoi `Messenger.Send` après `NavigateToAlbums()` ?** `NavigateToAlbums()` navigue de façon synchrone. La notification est envoyée une fois la page Albums chargée — le `NotificationControl` est dans la `MainWindow`, pas dans la `WelcomePage`, donc disponible dès que le `ContentFrame` a navigué.

- [ ] **Step 5 : Vérifier les usings**

`ShowNotificationMessage` est dans `Rok.Application.Messages`, `NotificationType` dans `Rok.Shared.Enums`. Ces namespaces sont probablement déjà couverts par les global usings (les autres messages sont utilisés sans using explicite dans ce fichier). Ajouter explicitement si le build échoue :

```csharp
using Rok.Application.Messages;
using Rok.Shared.Enums;
```

- [ ] **Step 6 : Build**

```bash
dotnet build /p:Platform=x64 -v quiet
```

Attendu : `0 Erreur(s)  0 Avertissement(s)`

- [ ] **Step 7 : Commit**

```bash
git add src/Presentation/ViewModels/Start/StartViewModel.cs
git commit -m "feat(onboarding): navigate to app after 100 albums and notify background import"
```

---

## Vérification manuelle

1. Supprimer la base de données SQLite pour simuler un premier démarrage :
   - Chemin : `%LOCALAPPDATA%\Rok\` → supprimer le fichier `.db`
2. Lancer l'app.
3. Observer la WelcomePage : les albums s'affichent, la barre de progression monte vers 100 %.
4. Au 100ᵉ album, l'app navigue automatiquement vers AlbumsPage.
5. Une InfoBar "Import in progress / Your first albums are ready..." apparaît et se masque après 5 s.
6. L'import continue en tâche de fond (la barre de rafraîchissement dans la toolbar tourne).

**Cas limites à vérifier :**
- Bibliothèque < 100 albums : l'import se termine, `LibraryRefreshChangeAsync` reçoit `Stop` → navigation normale sans notification (import déjà terminé, pas besoin de prévenir).
- Bibliothèque exactement 0 track : `ErrorOccurred = true` → carte d'erreur "Add Folder" (comportement inchangé).
