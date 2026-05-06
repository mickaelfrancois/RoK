# Onboarding Error State & Folder Picker Robustification — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Improve the failure path of the WelcomePage so that users who have no music in their default folder see a clear "local music player" identity, can pick a custom folder, and get contextual error feedback on access denied or empty folder.

**Architecture:** Add an `IFolderValidator` service (tested in isolation) to pre-validate the picked folder before starting the import pipeline. `StartViewModel` gains an `ErrorBannerMessage` observable property that drives a new collapsible error banner in the XAML error card. Happy path (auto-scan → 100 albums → navigate) is entirely unchanged.

**Tech Stack:** C# 13 / .NET 10, CommunityToolkit.Mvvm (`[ObservableProperty]`, `[RelayCommand]`), WinUI 3 (XAML x:Bind), xUnit + Moq, .resw localization files.

---

## File Map

| File | Action | Responsibility |
|---|---|---|
| `src/Presentation/ViewModels/Start/FolderValidator.cs` | **Create** | `IFolderValidator` interface + `FolderValidator` implementation — pure folder validation logic |
| `src/Presentation/ViewModels/Start/StartViewModel.cs` | **Modify** | Add `ErrorBannerMessage` property, inject `IFolderValidator`, fix `AddLibraryFolderAsync` and `LibraryRefreshChangeAsync` |
| `src/Presentation/Strings/fr-FR/Resources.resw` | **Modify** | Add 5 new string keys (fr) |
| `src/Presentation/Strings/en-US/Resources.resw` | **Modify** | Add 5 new string keys (en) |
| `src/Presentation/Strings/es-ES/Resources.resw` | **Modify** | Add 5 new string keys (es) |
| `src/Presentation/Strings/uk-UA/Resources.resw` | **Modify** | Add 5 new string keys (uk) |
| `src/Presentation/Pages/WelcomePage.xaml` | **Modify** | Error card redesign — identity heading, explanation box, error banner |
| `src/Presentation/Pages/WelcomePage.xaml.cs` | **Modify** | Remove silent `catch {}` |
| `tests/UnitTests/Rok.PresentationTests/ViewModels/Start/FolderValidatorTests.cs` | **Create** | Unit tests for `FolderValidator` |

> **Note on formats:** `FileSystemService.ValidExtensions` only contains `.mp3` and `.flac`. The spec listed a broader set of formats, but the strings below reflect only the actually supported formats to avoid misleading users.

---

## Task 1: `IFolderValidator` + `FolderValidator` (TDD)

**Files:**
- Create: `src/Presentation/ViewModels/Start/FolderValidator.cs`
- Create: `tests/UnitTests/Rok.PresentationTests/ViewModels/Start/FolderValidatorTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/UnitTests/Rok.PresentationTests/ViewModels/Start/FolderValidatorTests.cs`:

```csharp
using Rok.ViewModels.Start;

namespace Rok.PresentationTests.ViewModels.Start;

public class FolderValidatorTests : IDisposable
{
    private readonly DirectoryInfo _tempDir = Directory.CreateTempSubdirectory("FolderValidatorTests_");

    public void Dispose() => _tempDir.Delete(recursive: true);


    [Fact(DisplayName = "when_folder_has_mp3_files_returns_valid")]
    public async Task ValidateAsync_ReturnsValid_WhenFolderContainsMp3()
    {
        File.WriteAllText(Path.Combine(_tempDir.FullName, "track.mp3"), string.Empty);

        FolderValidationResult result = await FolderValidator.ValidateAsync(_tempDir.FullName);

        Assert.Equal(FolderValidationResult.Valid, result);
    }

    [Fact(DisplayName = "when_folder_has_flac_files_returns_valid")]
    public async Task ValidateAsync_ReturnsValid_WhenFolderContainsFlac()
    {
        File.WriteAllText(Path.Combine(_tempDir.FullName, "track.flac"), string.Empty);

        FolderValidationResult result = await FolderValidator.ValidateAsync(_tempDir.FullName);

        Assert.Equal(FolderValidationResult.Valid, result);
    }

    [Fact(DisplayName = "when_folder_has_only_unsupported_files_returns_no_audio_files")]
    public async Task ValidateAsync_ReturnsNoAudioFiles_WhenFolderContainsOnlyUnsupportedFiles()
    {
        File.WriteAllText(Path.Combine(_tempDir.FullName, "image.jpg"), string.Empty);
        File.WriteAllText(Path.Combine(_tempDir.FullName, "doc.pdf"), string.Empty);

        FolderValidationResult result = await FolderValidator.ValidateAsync(_tempDir.FullName);

        Assert.Equal(FolderValidationResult.NoAudioFiles, result);
    }

    [Fact(DisplayName = "when_folder_is_empty_returns_no_audio_files")]
    public async Task ValidateAsync_ReturnsNoAudioFiles_WhenFolderIsEmpty()
    {
        FolderValidationResult result = await FolderValidator.ValidateAsync(_tempDir.FullName);

        Assert.Equal(FolderValidationResult.NoAudioFiles, result);
    }

    [Fact(DisplayName = "when_audio_file_is_in_subdirectory_returns_valid")]
    public async Task ValidateAsync_ReturnsValid_WhenAudioFileIsInSubdirectory()
    {
        string sub = Path.Combine(_tempDir.FullName, "Artist", "Album");
        Directory.CreateDirectory(sub);
        File.WriteAllText(Path.Combine(sub, "track.mp3"), string.Empty);

        FolderValidationResult result = await FolderValidator.ValidateAsync(_tempDir.FullName);

        Assert.Equal(FolderValidationResult.Valid, result);
    }
}
```

- [ ] **Step 2: Run tests to confirm they fail**

```bash
rtk dotnet test tests/UnitTests/Rok.PresentationTests/Rok.PresentationTests.csproj /p:Platform=x64 --filter "FullyQualifiedName~FolderValidatorTests"
```

Expected: build error — `FolderValidator` and `FolderValidationResult` do not exist yet.

- [ ] **Step 3: Create `FolderValidator.cs`**

Create `src/Presentation/ViewModels/Start/FolderValidator.cs`:

```csharp
namespace Rok.ViewModels.Start;

public enum FolderValidationResult
{
    Valid,
    AccessDenied,
    NoAudioFiles
}

public static class FolderValidator
{
    private static readonly HashSet<string> ValidExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3",
        ".flac"
    };

    public static Task<FolderValidationResult> ValidateAsync(string folderPath) =>
        Task.Run(() =>
        {
            try
            {
                EnumerationOptions options = new()
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = false
                };

                bool hasAudio = Directory.EnumerateFiles(folderPath, "*.*", options)
                    .Any(f => ValidExtensions.Contains(Path.GetExtension(f)));

                return hasAudio ? FolderValidationResult.Valid : FolderValidationResult.NoAudioFiles;
            }
            catch (UnauthorizedAccessException)
            {
                return FolderValidationResult.AccessDenied;
            }
        });
}
```

- [ ] **Step 4: Run tests to confirm they pass**

```bash
rtk dotnet test tests/UnitTests/Rok.PresentationTests/Rok.PresentationTests.csproj /p:Platform=x64 --filter "FullyQualifiedName~FolderValidatorTests"
```

Expected: 5 tests pass.

- [ ] **Step 5: Commit**

```bash
rtk git add src/Presentation/ViewModels/Start/FolderValidator.cs tests/UnitTests/Rok.PresentationTests/ViewModels/Start/FolderValidatorTests.cs && rtk git commit -m "feat(onboarding): add FolderValidator for pre-import folder validation"
```

---

## Task 2: Update `StartViewModel`

**Files:**
- Modify: `src/Presentation/ViewModels/Start/StartViewModel.cs`

- [ ] **Step 1: Add `ErrorBannerMessage` property and new constructor fields**

In `StartViewModel.cs`, add the observable property after the existing `ImportProgress` property (line ~49):

```csharp
[ObservableProperty]
public partial string? ErrorBannerMessage { get; set; }
```

Add two private string fields after the existing string fields (after `_importBackgroundMessage` around line ~35):

```csharp
private readonly string _errorAccessDenied;
private readonly string _errorNoAudioFiles;
```

- [ ] **Step 2: Update the constructor signature and body**

Change the constructor signature to inject the folder validator:

```csharp
public StartViewModel(IAlbumPicture albumPicture, ISettingsFile settingsFile, NavigationService navigationService, IResourceService resourceService, IMediator mediator, IImport importService, IAppOptions appOptions)
```

stays the same — `FolderValidator` is a static class, no injection needed. Just load the two new strings at the end of the constructor body, after the existing `_importBackgroundMessage` line:

```csharp
_errorAccessDenied = resourceService.GetString("startViewErrorAccessDenied");
_errorNoAudioFiles = resourceService.GetString("startViewErrorNoAudio");
```

- [ ] **Step 3: Fix `AddLibraryFolderAsync`**

Replace the entire `AddLibraryFolderAsync` method:

```csharp
[RelayCommand]
private async Task AddLibraryFolderAsync(StorageFolder folder)
{
    FolderValidationResult validationResult = await FolderValidator.ValidateAsync(folder.Path);

    if (validationResult == FolderValidationResult.AccessDenied)
    {
        _dispatcherQueue.TryEnqueue(() => ErrorBannerMessage = _errorAccessDenied);
        return;
    }

    if (validationResult == FolderValidationResult.NoAudioFiles)
    {
        _dispatcherQueue.TryEnqueue(() => ErrorBannerMessage = _errorNoAudioFiles);
        return;
    }

    string token = Guid.NewGuid().ToString();
    StorageApplicationPermissions.FutureAccessList.AddOrReplace(token, folder);

    _appOptions.LibraryTokens.Clear();
    _appOptions.LibraryTokens.Add(token);
    await _settingsFile.SaveAsync(_appOptions);

    _dispatcherQueue.TryEnqueue(() =>
    {
        ErrorBannerMessage = null;
        ErrorOccurred = false;
        LibraryRefreshRunning = true;
    });

    _importService.StartAsync(0);
}
```

- [ ] **Step 4: Fix `LibraryRefreshChangeAsync` to set `ErrorBannerMessage` on 0 tracks**

In `LibraryRefreshChangeAsync`, update the `trackCount == 0` branch (currently around line ~129):

```csharp
if (trackCount == 0)
{
    ErrorOccurred = true;
    ErrorBannerMessage = _errorNoAudioFiles;
}
```

- [ ] **Step 5: Build to confirm compilation**

```bash
rtk dotnet build src/Presentation/Rok.csproj /p:Platform=x64 -v quiet
```

Expected: Build succeeded, 0 warnings, 0 errors.

- [ ] **Step 6: Commit**

```bash
rtk git add src/Presentation/ViewModels/Start/StartViewModel.cs && rtk git commit -m "feat(onboarding): add ErrorBannerMessage and folder pre-validation to StartViewModel"
```

---

## Task 3: Add localization strings

**Files:**
- Modify: `src/Presentation/Strings/fr-FR/Resources.resw`
- Modify: `src/Presentation/Strings/en-US/Resources.resw`
- Modify: `src/Presentation/Strings/es-ES/Resources.resw`
- Modify: `src/Presentation/Strings/uk-UA/Resources.resw`

For each file, locate the `startViewAddFolder.Text` entry and insert the new keys immediately after it (the new keys go in alphabetical order relative to the existing `startView*` block).

- [ ] **Step 1: Add strings to `fr-FR/Resources.resw`**

Two kinds of keys exist in .resw:
- Keys with `.Text` suffix → used by XAML `x:Uid` (e.g. `x:Uid="startViewTagline"` → looks up `startViewTagline.Text`)
- Keys without suffix → used by `resourceService.GetString("key")` in C# code

Find the `startViewAddFolder.Text` entry (currently value = "Sélectionner un dossier") and:
1. Update its value in place.
2. Insert the new keys immediately after it.

```xml
  <data name="startViewAddFolder.Text" xml:space="preserve">
    <value>Choisir un dossier de musique</value>
  </data>
  <data name="startViewErrorAccessDenied" xml:space="preserve">
    <value>Accès refusé à ce dossier. Essayez un autre dossier ou vérifiez les permissions.</value>
  </data>
  <data name="startViewErrorNoAudio" xml:space="preserve">
    <value>Ce dossier ne contient aucun fichier audio compatible (MP3, FLAC).</value>
  </data>
  <data name="startViewExplanation.Text" xml:space="preserve">
    <value>Rok joue votre musique stockée localement sur ce PC. Aucun fichier audio n'a été trouvé dans votre dossier.</value>
  </data>
  <data name="startViewFormats.Text" xml:space="preserve">
    <value>MP3 · FLAC</value>
  </data>
  <data name="startViewTagline.Text" xml:space="preserve">
    <value>Lecteur de musique locale</value>
  </data>
```

`startViewErrorAccessDenied` and `startViewErrorNoAudio` have **no `.Text` suffix** because they are loaded via `resourceService.GetString()` in C#, not via XAML `x:Uid`.
The XAML x:Uid keys (`startViewTagline`, `startViewExplanation`, `startViewFormats`, `startViewAddFolder`) all keep `.Text`.

- [ ] **Step 2: Add strings to `en-US/Resources.resw`**

Same location (update `startViewAddFolder.Text` value, insert new keys after it):

```xml
  <data name="startViewAddFolder.Text" xml:space="preserve">
    <value>Choose a music folder</value>
  </data>
  <data name="startViewErrorAccessDenied" xml:space="preserve">
    <value>Access denied to this folder. Try a different folder or check your permissions.</value>
  </data>
  <data name="startViewErrorNoAudio" xml:space="preserve">
    <value>This folder contains no compatible audio files (MP3, FLAC).</value>
  </data>
  <data name="startViewExplanation.Text" xml:space="preserve">
    <value>Rok plays music stored locally on your PC. No audio files were found in your folder.</value>
  </data>
  <data name="startViewFormats.Text" xml:space="preserve">
    <value>MP3 · FLAC</value>
  </data>
  <data name="startViewTagline.Text" xml:space="preserve">
    <value>Local music player</value>
  </data>
```

- [ ] **Step 3: Add strings to `es-ES/Resources.resw`**

```xml
  <data name="startViewAddFolder.Text" xml:space="preserve">
    <value>Elegir una carpeta de música</value>
  </data>
  <data name="startViewErrorAccessDenied" xml:space="preserve">
    <value>Acceso denegado a esta carpeta. Intenta con otra carpeta o verifica los permisos.</value>
  </data>
  <data name="startViewErrorNoAudio" xml:space="preserve">
    <value>Esta carpeta no contiene archivos de audio compatibles (MP3, FLAC).</value>
  </data>
  <data name="startViewExplanation.Text" xml:space="preserve">
    <value>Rok reproduce música almacenada localmente en tu PC. No se encontraron archivos de audio en tu carpeta.</value>
  </data>
  <data name="startViewFormats.Text" xml:space="preserve">
    <value>MP3 · FLAC</value>
  </data>
  <data name="startViewTagline.Text" xml:space="preserve">
    <value>Reproductor de música local</value>
  </data>
```

- [ ] **Step 4: Add strings to `uk-UA/Resources.resw`**

```xml
  <data name="startViewAddFolder.Text" xml:space="preserve">
    <value>Вибрати папку з музикою</value>
  </data>
  <data name="startViewErrorAccessDenied" xml:space="preserve">
    <value>Доступ до цієї папки заборонено. Спробуйте іншу папку або перевірте права доступу.</value>
  </data>
  <data name="startViewErrorNoAudio" xml:space="preserve">
    <value>Ця папка не містить сумісних аудіофайлів (MP3, FLAC).</value>
  </data>
  <data name="startViewExplanation.Text" xml:space="preserve">
    <value>Rok відтворює музику, збережену локально на вашому ПК. У вашій папці не знайдено аудіофайлів.</value>
  </data>
  <data name="startViewFormats.Text" xml:space="preserve">
    <value>MP3 · FLAC</value>
  </data>
  <data name="startViewTagline.Text" xml:space="preserve">
    <value>Локальний музичний плеєр</value>
  </data>
```

- [ ] **Step 5: Build to confirm no resource key errors**

```bash
rtk dotnet build src/Presentation/Rok.csproj /p:Platform=x64 -v quiet
```

Expected: Build succeeded, 0 warnings, 0 errors.

- [ ] **Step 6: Commit**

```bash
rtk git add src/Presentation/Strings/ && rtk git commit -m "feat(onboarding): add localization strings for error card redesign"
```

---

## Task 4: Update `WelcomePage.xaml` error card

**Files:**
- Modify: `src/Presentation/Pages/WelcomePage.xaml`

The existing error card block starts at line ~167 (`<!-- Error state — centered floating card -->`). Replace the entire inner `StackPanel` (lines ~178–198) with the redesigned version below.

- [ ] **Step 1: Replace the error card `StackPanel`**

Find this block:
```xml
                    <StackPanel Spacing="20" HorizontalAlignment="Center">
                        <FontIcon Glyph="&#xE8B7;"
                                  FontSize="56"
                                  Foreground="{ThemeResource AccentTextFillColorPrimaryBrush}"
                                  HorizontalAlignment="Center" />
                        <TextBlock x:Uid="startViewErrorMessage"
                                   FontSize="{StaticResource FontSizeMedium}"
                                   HorizontalAlignment="Center"
                                   TextAlignment="Center"
                                   TextWrapping="Wrap"
                                   Foreground="{ThemeResource TextFillColorPrimaryBrush}" />
                        <Button Click="Button_Click"
                                HorizontalAlignment="Center"
                                Style="{StaticResource AccentButtonStyle}">
                            <StackPanel Orientation="Horizontal" Spacing="8">
                                <SymbolIcon Symbol="Add" />
                                <TextBlock x:Uid="startViewAddFolder" />
                            </StackPanel>
                        </Button>
                    </StackPanel>
```

Replace with:
```xml
                    <StackPanel Spacing="16" MinWidth="340" MaxWidth="420">

                        <!-- Identity header -->
                        <StackPanel HorizontalAlignment="Center" Spacing="2">
                            <FontIcon Glyph="&#xE8D6;"
                                      FontSize="36"
                                      Foreground="{ThemeResource AccentTextFillColorPrimaryBrush}"
                                      HorizontalAlignment="Center" />
                            <TextBlock Text="ROK"
                                       FontFamily="{StaticResource FontFamilyBrand}"
                                       FontSize="20"
                                       FontWeight="Bold"
                                       HorizontalAlignment="Center"
                                       CharacterSpacing="200"
                                       Foreground="{ThemeResource TextFillColorPrimaryBrush}" />
                            <TextBlock x:Uid="startViewTagline"
                                       FontSize="{StaticResource FontSizeSmall}"
                                       HorizontalAlignment="Center"
                                       Opacity="0.6"
                                       Foreground="{ThemeResource TextFillColorPrimaryBrush}" />
                        </StackPanel>

                        <!-- Explanation box -->
                        <Border Background="{ThemeResource SubtleFillColorSecondaryBrush}"
                                CornerRadius="8"
                                Padding="14,12">
                            <StackPanel Spacing="8">
                                <TextBlock x:Uid="startViewExplanation"
                                           FontSize="{StaticResource FontSizeSmall}"
                                           TextWrapping="Wrap"
                                           LineHeight="22"
                                           Foreground="{ThemeResource TextFillColorPrimaryBrush}" />
                                <TextBlock x:Uid="startViewFormats"
                                           FontSize="{StaticResource FontSizeXSmall}"
                                           FontFamily="Consolas"
                                           Opacity="0.5"
                                           Foreground="{ThemeResource TextFillColorPrimaryBrush}" />
                            </StackPanel>
                        </Border>

                        <!-- CTA button -->
                        <Button Click="Button_Click"
                                HorizontalAlignment="Stretch"
                                Style="{StaticResource AccentButtonStyle}">
                            <StackPanel Orientation="Horizontal" Spacing="8">
                                <FontIcon Glyph="&#xE838;" FontSize="14" />
                                <TextBlock x:Uid="startViewAddFolder" />
                            </StackPanel>
                        </Button>

                        <!-- Contextual error banner — visible only when ErrorBannerMessage is set -->
                        <Border Background="{ThemeResource SystemFillColorCriticalBackgroundBrush}"
                                BorderBrush="{ThemeResource SystemFillColorCriticalBrush}"
                                BorderThickness="1"
                                CornerRadius="8"
                                Padding="12,10"
                                Visibility="{x:Bind ViewModel.ErrorBannerMessage, Mode=OneWay, Converter={StaticResource StringToVisibilityConverter}}">
                            <TextBlock Text="{x:Bind ViewModel.ErrorBannerMessage, Mode=OneWay}"
                                       FontSize="{StaticResource FontSizeXSmall}"
                                       TextWrapping="Wrap"
                                       LineHeight="20"
                                       Foreground="{ThemeResource SystemFillColorCriticalBrush}" />
                        </Border>

                    </StackPanel>
```

- [ ] **Step 2: Build to confirm XAML compiles**

```bash
rtk dotnet build src/Presentation/Rok.csproj /p:Platform=x64 -v quiet
```

Expected: Build succeeded, 0 warnings, 0 errors.

- [ ] **Step 3: Commit**

```bash
rtk git add src/Presentation/Pages/WelcomePage.xaml && rtk git commit -m "feat(onboarding): redesign error card with identity and contextual error banner"
```

---

## Task 5: Fix `WelcomePage.xaml.cs` silent catch

**Files:**
- Modify: `src/Presentation/Pages/WelcomePage.xaml.cs`

- [ ] **Step 1: Remove the silent `catch {}`**

Replace:
```csharp
    private async void Button_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            FolderPicker folderPicker = new()
            {
                ViewMode = PickerViewMode.List,
                SuggestedStartLocation = PickerLocationId.MusicLibrary
            };

            InitializeWithWindow.Initialize(folderPicker, Rok.App.MainWindowHandle);

            StorageFolder? folder = await folderPicker.PickSingleFolderAsync();

            if (folder is not null)
                ViewModel.AddLibraryFolderCommand.Execute(folder);
        }
        catch
        {
        }
    }
```

With:
```csharp
    private async void Button_Click(object sender, RoutedEventArgs e)
    {
        FolderPicker folderPicker = new()
        {
            ViewMode = PickerViewMode.List,
            SuggestedStartLocation = PickerLocationId.MusicLibrary
        };

        InitializeWithWindow.Initialize(folderPicker, Rok.App.MainWindowHandle);

        StorageFolder? folder = await folderPicker.PickSingleFolderAsync();

        if (folder is not null)
            ViewModel.AddLibraryFolderCommand.Execute(folder);
    }
```

Cancellation returns `null` (no exception), so no try/catch is needed. Any genuine COM failure from `PickSingleFolderAsync` would indicate a missing window handle — a programming error that should surface rather than be swallowed.

- [ ] **Step 2: Final build + all tests**

```bash
rtk dotnet build /p:Platform=x64 -v quiet && rtk dotnet test /p:Platform=x64 --filter "FullyQualifiedName~FolderValidatorTests"
```

Expected: Build succeeded. 5 tests pass.

- [ ] **Step 3: Commit**

```bash
rtk git add src/Presentation/Pages/WelcomePage.xaml.cs && rtk git commit -m "fix(onboarding): remove silent catch in folder picker, surface errors via ErrorBannerMessage"
```

---

## Manual Test Checklist

After all tasks are complete, run the app and verify these scenarios on the WelcomePage (requires `IsFirstStart = true` — reset by clearing the database or temporarily forcing it in code):

- [ ] **Happy path unchanged**: Windows Music Library has MP3/FLAC files → import runs, albums appear, navigates at 100 albums. No error card shown.
- [ ] **Error card identity**: Windows Music Library is empty → error card appears with "ROK" heading, "Lecteur de musique locale" subtitle, explanation text, and "MP3 · FLAC" formats.
- [ ] **No error banner on first show**: Error card appears without the red banner (no sub-error yet).
- [ ] **Pick valid folder**: Click button → select a folder with MP3/FLAC files → import starts, error card disappears, albums begin appearing.
- [ ] **Pick empty folder**: Click button → select a folder with no audio files → error card stays, red banner appears with "Ce dossier ne contient aucun fichier audio compatible (MP3, FLAC)."
- [ ] **Pick folder with access denied**: Simulate by picking a system folder (e.g., `C:\Windows\System32`) → red banner appears with "Accès refusé à ce dossier…"
- [ ] **Re-pick after error**: After a failed pick, click the button again and pick a valid folder → banner disappears, import starts normally.
- [ ] **Cancellation is silent**: Click button, open picker, press Escape → error card stays, no banner shown, no crash.
