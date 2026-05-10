# Lyrics Badge UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remplacer le badge "Lyrics" unique et générique par deux badges pill distincts — "sync lyrics" (bleu) pour les paroles synchronisées `.lrc` et "lyrics" (gris) pour les paroles brutes `.txt` — sur les 7 pages de listes de tracks.

**Architecture:** `TrackLyricsService` expose `CheckLyricsType` qui retourne `ELyricsType` (déjà calculé par `ILyricsService`). `TrackViewModel` expose une propriété `LyricsType` (lazy-cached) et dérive `LyricsExists` de celle-ci. Un nouveau `LyricsTypeVisibilityConverter` paramétrable par le type cible permet d'afficher l'un ou l'autre badge en XAML.

**Tech Stack:** C# 13 / .NET 10, WinUI 3 (Windows App SDK 1.8), CommunityToolkit.Mvvm, `x:Bind` one-way.

---

## Fichiers touchés

| Action | Fichier |
|--------|---------|
| Modify | `src/Rok.Application/Features/Tracks/Services/TrackLyricsService.cs` |
| Modify | `src/Presentation/ViewModels/Track/TrackViewModel.cs` |
| **Create** | `src/Presentation/Converters/LyricsTypeVisibilityConverter.cs` |
| Modify | `src/Presentation/App.xaml` |
| Modify | `src/Presentation/Styles/Components/ControlsStyles.xaml` |
| Modify | `src/Presentation/Pages/AlbumPage.xaml` |
| Modify | `src/Presentation/Pages/ArtistPage.xaml` |
| Modify | `src/Presentation/Pages/ListeningPage.xaml` |
| Modify | `src/Presentation/Pages/TracksPage.xaml` |
| Modify | `src/Presentation/Pages/PlaylistPage.xaml` |
| Modify | `src/Presentation/Pages/SearchPage.xaml` |
| Modify | `src/Presentation/Pages/SmartPlaylistPage.xaml` |
| Modify | `.gitignore` |

---

## Task 1 — Couche Application : exposer `ELyricsType` depuis `TrackLyricsService`

**Files:**
- Modify: `src/Rok.Application/Features/Tracks/Services/TrackLyricsService.cs:11-14`

- [ ] **Step 1 — Ajouter `CheckLyricsType` dans `TrackLyricsService`**

  Après la méthode `CheckLyricsExists` (ligne 14), ajouter :

  ```csharp
  public ELyricsType CheckLyricsType(string musicFile)
  {
      return lyricsService.CheckLyricsFileExists(musicFile);
  }
  ```

  Le fichier complet après modification (méthode `CheckLyricsExists` existante + nouvelle méthode) :

  ```csharp
  public bool CheckLyricsExists(string musicFile)
  {
      return lyricsService.CheckLyricsFileExists(musicFile) != ELyricsType.None;
  }

  public ELyricsType CheckLyricsType(string musicFile)
  {
      return lyricsService.CheckLyricsFileExists(musicFile);
  }
  ```

---

## Task 2 — ViewModel : `LyricsType` lazy-cached + `LyricsExists` dérivée

**Files:**
- Modify: `src/Presentation/ViewModels/Track/TrackViewModel.cs:26,137-151,253-271`

- [ ] **Step 1 — Ajouter le champ cache `_lyricsType`**

  À la ligne 26 (après `private LyricsModel? _lyrics;`), ajouter :

  ```csharp
  private LyricsModel? _lyrics;
  private ELyricsType? _lyricsType;
  ```

- [ ] **Step 2 — Remplacer la propriété `LyricsExists` par `LyricsType` + `LyricsExists` dérivée**

  Remplacer les lignes 137-151 (propriété `LyricsExists` actuelle) par :

  ```csharp
  public ELyricsType LyricsType
  {
      get
      {
          if (_lyricsType is null)
          {
              _lyricsType = _lyricsService.CheckLyricsType(Track.MusicFile);
              if (_lyricsType == ELyricsType.None)
              {
  #pragma warning disable 4014
                  _ = GetLyricsFromAPIAsync();
  #pragma warning restore 4014
              }
          }

          return _lyricsType.Value;
      }
  }

  public bool LyricsExists => LyricsType != ELyricsType.None;
  ```

- [ ] **Step 3 — Mettre à jour `GetLyricsFromAPIAsync` pour invalider le cache**

  Remplacer les lignes 253-271 (méthode `GetLyricsFromAPIAsync`) par :

  ```csharp
  private async Task GetLyricsFromAPIAsync()
  {
      bool success = await _lyricsService.GetAndSaveLyricsFromApiAsync(Track);

      if (success)
      {
          Track.GetLyricsLastAttempt = DateTime.UtcNow;

          DispatcherQueue dispatcher = DispatcherQueue.GetForCurrentThread();
          if (dispatcher != null)
          {
              dispatcher.TryEnqueue(() =>
              {
                  _lyricsType = null;
                  OnPropertyChanged(nameof(LyricsType));
                  OnPropertyChanged(nameof(LyricsExists));
              });
          }
          else
          {
              _lyricsType = null;
              OnPropertyChanged(nameof(LyricsType));
              OnPropertyChanged(nameof(LyricsExists));
          }
      }
  }
  ```

---

## Task 3 — Converter `LyricsTypeVisibilityConverter`

**Files:**
- Create: `src/Presentation/Converters/LyricsTypeVisibilityConverter.cs`

- [ ] **Step 1 — Créer le fichier converter**

  ```csharp
  using Rok.Application.Dto.Lyrics;

  namespace Rok.Converters;

  public partial class LyricsTypeVisibilityConverter : IValueConverter
  {
      public object Convert(object value, Type targetType, object parameter, string language)
      {
          if (value is not ELyricsType lyricsType || parameter is not string target)
              return Visibility.Collapsed;

          return target switch
          {
              "Synchronized" => lyricsType == ELyricsType.Synchronized ? Visibility.Visible : Visibility.Collapsed,
              "Plain"        => lyricsType == ELyricsType.Plain        ? Visibility.Visible : Visibility.Collapsed,
              _              => Visibility.Collapsed
          };
      }

      public object ConvertBack(object value, Type targetType, object parameter, string language)
          => throw new NotImplementedException();
  }
  ```

- [ ] **Step 2 — Enregistrer le converter dans `App.xaml`**

  Dans `src/Presentation/App.xaml`, après la ligne contenant `LyricColorConverter` (ligne ~42) :

  ```xml
  <converters:LyricColorConverter x:Key="LyricColorConverter" />
  <converters:LyricsTypeVisibilityConverter x:Key="LyricsTypeVisibilityConverter" />
  ```

---

## Task 4 — Styles badge dans `ControlsStyles.xaml`

**Files:**
- Modify: `src/Presentation/Styles/Components/ControlsStyles.xaml:1049-1054`

- [ ] **Step 1 — Remplacer `GridTracksLyricsHyperlinkButtonStyle` par deux styles pill**

  Remplacer les lignes 1049-1054 (style `GridTracksLyricsHyperlinkButtonStyle`) par :

  ```xml
  <Style x:Key="GridTracksSyncLyricsBadgeStyle" TargetType="HyperlinkButton">
      <Setter Property="Content" Value="sync lyrics"/>
      <Setter Property="FontSize" Value="9"/>
      <Setter Property="FontWeight" Value="SemiBold"/>
      <Setter Property="Foreground" Value="#FF3A86FF"/>
      <Setter Property="Padding" Value="5,1"/>
      <Setter Property="Margin" Value="4,0,0,0"/>
      <Setter Property="MinHeight" Value="0"/>
      <Setter Property="MinWidth" Value="0"/>
      <Setter Property="Template">
          <Setter.Value>
              <ControlTemplate TargetType="HyperlinkButton">
                  <Border Background="#263A86FF"
                          BorderBrush="#593A86FF"
                          BorderThickness="1"
                          CornerRadius="4"
                          Padding="{TemplateBinding Padding}">
                      <ContentPresenter
                          Content="{TemplateBinding Content}"
                          FontSize="{TemplateBinding FontSize}"
                          FontWeight="{TemplateBinding FontWeight}"
                          Foreground="{TemplateBinding Foreground}"
                          HorizontalAlignment="Center"
                          VerticalAlignment="Center"/>
                  </Border>
              </ControlTemplate>
          </Setter.Value>
      </Setter>
  </Style>

  <Style x:Key="GridTracksPlainLyricsBadgeStyle" TargetType="HyperlinkButton">
      <Setter Property="Content" Value="lyrics"/>
      <Setter Property="FontSize" Value="9"/>
      <Setter Property="FontWeight" Value="Normal"/>
      <Setter Property="Foreground" Value="#FF999999"/>
      <Setter Property="Padding" Value="5,1"/>
      <Setter Property="Margin" Value="4,0,0,0"/>
      <Setter Property="MinHeight" Value="0"/>
      <Setter Property="MinWidth" Value="0"/>
      <Setter Property="Template">
          <Setter.Value>
              <ControlTemplate TargetType="HyperlinkButton">
                  <Border Background="#0FFFFFFF"
                          BorderBrush="#1FFFFFFF"
                          BorderThickness="1"
                          CornerRadius="4"
                          Padding="{TemplateBinding Padding}">
                      <ContentPresenter
                          Content="{TemplateBinding Content}"
                          FontSize="{TemplateBinding FontSize}"
                          FontWeight="{TemplateBinding FontWeight}"
                          Foreground="{TemplateBinding Foreground}"
                          HorizontalAlignment="Center"
                          VerticalAlignment="Center"/>
                  </Border>
              </ControlTemplate>
          </Setter.Value>
      </Setter>
  </Style>
  ```

---

## Task 5 — Mise à jour des 7 pages XAML

Dans chaque page, le badge actuel est :
```xml
<HyperlinkButton x:Uid="lyricsTag" Visibility="{x:Bind LyricsExists, Converter={StaticResource BoolToVisibilityConverter}}" Style="{StaticResource GridTracksLyricsHyperlinkButtonStyle}" Command="{x:Bind LyricsOpenCommand}" />
```

Le remplacer par ces deux lignes dans chacune des pages ci-dessous.

**Remplacement commun (identique dans les 7 pages) :**
```xml
<HyperlinkButton Visibility="{x:Bind LyricsType, Converter={StaticResource LyricsTypeVisibilityConverter}, ConverterParameter=Synchronized, Mode=OneWay}" Style="{StaticResource GridTracksSyncLyricsBadgeStyle}" Command="{x:Bind LyricsOpenCommand}" />
<HyperlinkButton Visibility="{x:Bind LyricsType, Converter={StaticResource LyricsTypeVisibilityConverter}, ConverterParameter=Plain, Mode=OneWay}" Style="{StaticResource GridTracksPlainLyricsBadgeStyle}" Command="{x:Bind LyricsOpenCommand}" />
```

**Files:**
- Modify: `src/Presentation/Pages/AlbumPage.xaml:133`
- Modify: `src/Presentation/Pages/ArtistPage.xaml:184`
- Modify: `src/Presentation/Pages/ListeningPage.xaml:244`
- Modify: `src/Presentation/Pages/TracksPage.xaml:141`
- Modify: `src/Presentation/Pages/PlaylistPage.xaml:133`
- Modify: `src/Presentation/Pages/SearchPage.xaml:241`
- Modify: `src/Presentation/Pages/SmartPlaylistPage.xaml:167`

- [ ] **Step 1 — `AlbumPage.xaml` ligne 133**
- [ ] **Step 2 — `ArtistPage.xaml` ligne 184**
- [ ] **Step 3 — `ListeningPage.xaml` ligne 244**
- [ ] **Step 4 — `TracksPage.xaml` ligne 141**
- [ ] **Step 5 — `PlaylistPage.xaml` ligne 133**
- [ ] **Step 6 — `SearchPage.xaml` ligne 241**
- [ ] **Step 7 — `SmartPlaylistPage.xaml` ligne 167**

---

## Task 6 — `.gitignore`

**Files:**
- Modify: `.gitignore`

- [ ] **Step 1 — Ajouter `.superpowers/`**

  Ajouter à la fin du fichier `.gitignore` :

  ```
  .superpowers/
  ```

---

## Task 7 — Build de vérification

- [ ] **Step 1 — Builder le projet**

  ```bash
  dotnet build Rok.slnx /p:Platform=x64 -v quiet
  ```

  Attendu : `Build succeeded.` sans erreur ni warning (TreatWarningsAsErrors est actif).

  Si erreur de compilation dans `TrackViewModel`, vérifier que `using Rok.Application.Dto.Lyrics;` est présent (ligne 4 du fichier — déjà là dans le fichier original).

---

## Task 8 — Commit unique

- [ ] **Step 1 — Stager tous les fichiers modifiés**

  ```bash
  git add src/Rok.Application/Features/Tracks/Services/TrackLyricsService.cs
  git add src/Presentation/ViewModels/Track/TrackViewModel.cs
  git add src/Presentation/Converters/LyricsTypeVisibilityConverter.cs
  git add src/Presentation/App.xaml
  git add src/Presentation/Styles/Components/ControlsStyles.xaml
  git add src/Presentation/Pages/AlbumPage.xaml
  git add src/Presentation/Pages/ArtistPage.xaml
  git add src/Presentation/Pages/ListeningPage.xaml
  git add src/Presentation/Pages/TracksPage.xaml
  git add src/Presentation/Pages/PlaylistPage.xaml
  git add src/Presentation/Pages/SearchPage.xaml
  git add src/Presentation/Pages/SmartPlaylistPage.xaml
  git add .gitignore
  ```

- [ ] **Step 2 — Commit**

  ```bash
  git commit -m "feat(ui): differentiate sync and plain lyrics badges in track lists"
  ```
