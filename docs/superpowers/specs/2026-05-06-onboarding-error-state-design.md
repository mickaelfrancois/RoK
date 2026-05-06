# Onboarding — Error State & Folder Picker Robustification

**Date:** 2026-05-06  
**Scope:** Failure path only — happy path (auto-scan → 100 albums → navigate) is unchanged.  
**Goal:** Reduce abandonment when no music is found on first start by (1) clearly identifying Rok as a local music player and (2) making the folder picker reliable with proper error feedback.

---

## Context

27 installs, 5 regular users. Short sessions (1–24 s) suggest users either don't understand Rok is a local player (not streaming) or get stuck at the folder selection step. The happy path is already satisfactory; this spec targets only the failure path.

**Two root causes:**
1. The error card gives no indication that Rok is a local music player — users who expected a streaming service abandon immediately.
2. The folder picker has an untested silent `catch {}` — access denied and empty folder errors produce no feedback, leaving users stranded.

---

## Flow

```
App launch
  └─ IsFirstStart = true
       └─ Auto-scan Windows Music Library
            ├─ ≥1 track found → import runs, albums appear, navigate at 100 albums  (UNCHANGED)
            └─ 0 tracks found → show Error Card
                  └─ User clicks "Choisir un dossier"
                       ├─ Cancelled (null) → nothing, card stays
                       ├─ Access denied → show error banner "Accès refusé…", card stays
                       ├─ 0 compatible files → show error banner "Aucun fichier audio…", card stays
                       └─ ≥1 track found → import runs → normal happy path continues
```

Every failure re-shows the error card with a contextual banner. The loop runs until a valid folder is found or the user quits.

---

## Section 1 — Error Card Redesign

### Current state

```
📁
Aucun album n'a pu être trouvé dans votre dossier musique.
[ ＋ Sélectionner un dossier ]
```

### Proposed state

```
🎵  ROK
Lecteur de musique locale

┌────────────────────────────────────────┐
│ Rok joue votre musique stockée         │
│ localement sur ce PC.                  │
│ Aucun fichier audio n'a été trouvé     │
│ dans votre dossier.                    │
│                                        │
│ MP3 · FLAC · WAV · AAC · OGG · M4A    │
└────────────────────────────────────────┘

[ 📂 Choisir un dossier de musique ]

┌────────────────────────────────────────┐  ← visible only on sub-error
│ ⚠ Accès refusé à ce dossier.          │    (red tint, low opacity border)
│   Essayez un autre dossier ou          │
│   vérifiez les permissions.            │
└────────────────────────────────────────┘
```

**Changes from current:**
- App name "ROK" + subtitle "Lecteur de musique locale" → addresses streaming misexpectation
- Explanation box with supported formats → user knows what files to look for
- Error banner is hidden by default; only shown when a sub-error occurs

### XAML impact

`WelcomePage.xaml` — error card section:
- Add `TextBlock` for "ROK" heading and "Lecteur de musique locale" subtitle
- Add explanation `Border` with formats list
- Add `ErrorBannerBorder` (collapsed by default, red tint) bound to `ErrorBannerMessage`
- Update button text to "Choisir un dossier de musique"

### Localization

New/updated keys in `Resources.resw` (fr-FR, en-US, es-ES, uk-UA):

| Key | fr-FR value |
|---|---|
| `startViewTagline` | `Lecteur de musique locale` |
| `startViewExplanation` | `Rok joue votre musique stockée localement sur ce PC.\nAucun fichier audio n'a été trouvé dans votre dossier.` |
| `startViewFormats` | `MP3 · FLAC · WAV · AAC · OGG · M4A` |
| `startViewAddFolder` | `Choisir un dossier de musique` |
| `startViewErrorAccessDenied` | `Accès refusé à ce dossier. Essayez un autre dossier ou vérifiez les permissions.` |
| `startViewErrorNoAudio` | `Ce dossier ne contient aucun fichier audio compatible (MP3, FLAC, WAV…).` |

---

## Section 2 — Folder Picker Robustification

### Current bug

`WelcomePage.xaml.cs` `Button_Click` has a silent `catch {}`. Any exception (access denied, COM error) produces no user feedback. `AddLibraryFolderAsync` also does not validate whether the folder contains any audio files before triggering a scan.

### Fix — ViewModel (`StartViewModel.cs`)

Add observable property:

```csharp
[ObservableProperty]
private string? _errorBannerMessage;
```

`AddLibraryFolderAsync` changes:
1. After picking the folder, attempt to enumerate files. If `UnauthorizedAccessException` → set `ErrorBannerMessage` to the access-denied string and return (do not clear tokens, do not start import).
2. After scan completes, if `TracksCount == 0` → set `ErrorBannerMessage` to the no-audio string and return to error card state (`ErrorOccurred = true`, `IsImporting = false`).
3. On success → clear `ErrorBannerMessage`, proceed as today.

### Fix — Code-behind (`WelcomePage.xaml.cs`)

Replace `catch {}` with:

```csharp
catch (UnauthorizedAccessException)
{
    ViewModel.ErrorBannerMessage = ...; // resource string
}
catch (Exception ex)
{
    // log + show generic message
}
```

Or propagate via `ErrorBannerMessage` entirely through the VM — preferred to keep code-behind minimal.

### Error cases

| Case | Trigger | Message key |
|---|---|---|
| User cancelled (folder = null) | `PickSingleFolderAsync` returns null | — (silent, banner hidden) |
| Access denied | `UnauthorizedAccessException` on folder enum | `startViewErrorAccessDenied` |
| No compatible files | Scan completes, `TracksCount == 0` | `startViewErrorNoAudio` |

---

## Out of scope

- Happy path changes (album clickability during import, "library ready" prompt, CTA on Albums page)
- Telemetry `first_start` event (tracked separately by the user)
- Any changes to the NavigationView / MainWindow onboarding flag

---

## Files affected

| File | Change |
|---|---|
| `src/Presentation/Pages/WelcomePage.xaml` | Error card redesign (heading, explanation, formats, error banner) |
| `src/Presentation/Pages/WelcomePage.xaml.cs` | Replace silent catch, remove direct error handling |
| `src/Presentation/ViewModels/Start/StartViewModel.cs` | Add `ErrorBannerMessage`, fix `AddLibraryFolderAsync` validation |
| `src/Presentation/Strings/*/Resources.resw` (×4) | New/updated localization keys |
