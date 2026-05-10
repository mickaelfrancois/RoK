# Lyrics Badge UI — Design Spec

**Date:** 2026-05-10  
**Branch:** feat/lyrics-badge-ui (à créer)

## Contexte

Les pages de listes de tracks (AlbumPage, ArtistPage, ListeningPage, TracksPage, PlaylistPage, SearchPage) affichent un badge cliquable `"Lyrics"` lorsque des paroles sont disponibles pour un track. Ce badge est un `HyperlinkButton` avec un style minimal (texte gris, 9px). Il ne différencie pas les paroles synchronisées (fichier `.lrc`) des paroles brutes (fichier `.txt`).

L'objectif est d'améliorer ce badge visuellement et d'introduire une distinction visuelle entre les deux types de paroles.

## Design visuel validé

Deux badges pill distincts, positionnés immédiatement après le titre du track dans le groupe de titre (sans margin supplémentaire — le gap du StackPanel parent suffit) :

| Type | Texte | Couleur texte | Fond | Bordure |
|------|-------|---------------|------|---------|
| Synchronisé (`.lrc`) | `sync lyrics` | `#3A86FF` | `rgba(58,134,255,0.15)` | `rgba(58,134,255,0.35)` |
| Brut (`.txt`) | `lyrics` | `#999999` | `rgba(255,255,255,0.06)` | `rgba(255,255,255,0.12)` |

Style commun : `font-size 9px`, `font-weight 600` (sync) / `500` (plain), `border-radius 4px`, `padding 1px 5px`. Les deux restent cliquables et déclenchent `LyricsOpenCommand` (comportement actuel inchangé).

## Architecture

### 1. `TrackLyricsService` — nouvelle méthode

Ajouter dans `Rok.Application/Features/Tracks/Services/TrackLyricsService.cs` :

```csharp
public ELyricsType CheckLyricsType(string musicFile)
    => _lyricsService.CheckLyricsFileExists(musicFile);
```

`ILyricsService.CheckLyricsFileExists` retourne déjà `ELyricsType` — il s'agit d'une simple délégation.

### 2. `TrackViewModel` — propriété `LyricsType`

Dans `Presentation/ViewModels/Track/TrackViewModel.cs` :

- Ajouter un champ privé `ELyricsType? _lyricsType` (nullable pour lazy init).
- Ajouter une propriété publique :

```csharp
public ELyricsType LyricsType
{
    get
    {
        _lyricsType ??= TrackLyricsService.CheckLyricsType(MusicFile);
        return _lyricsType.Value;
    }
}
```

- Modifier `LyricsExists` pour dériver de `LyricsType` :

```csharp
public bool LyricsExists => LyricsType != ELyricsType.None;
```

Cela évite un double hit file system (un seul appel `CheckLyricsType` par instance de VM, résultat mis en cache).

**Important :** Lors de l'appel async `GetLyricsFromAPIAsync()` (téléchargement depuis l'API), réinitialiser `_lyricsType = null` puis notifier `LyricsType` et `LyricsExists` pour que l'UI se mette à jour après téléchargement.

### 3. `LyricsTypeVisibilityConverter` — nouveau converter

Fichier : `Presentation/Converters/LyricsTypeVisibilityConverter.cs`

```csharp
public sealed class LyricsTypeVisibilityConverter : IValueConverter
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

Enregistrer dans `App.xaml` (ou le ResourceDictionary approprié) :

```xml
<converters:LyricsTypeVisibilityConverter x:Key="LyricsTypeVisibilityConverter"/>
```

### 4. Styles — `ControlsStyles.xaml`

Remplacer `GridTracksLyricsHyperlinkButtonStyle` par deux nouveaux styles. Chaque style définit un `ControlTemplate` minimal (un `Border` wrappant un `ContentPresenter`) pour supporter le fond et la bordure pill. Le comportement de survol/pression reste géré via `VisualStateManager`.

**`GridTracksSyncLyricsBadgeStyle`** (pour paroles synchronisées) :
- `Content` = `"sync lyrics"`
- Foreground `#3A86FF`, Background `#263A86FF` (approximation ARGB), BorderBrush `#5C3A86FF`, BorderThickness `1`, CornerRadius `4`, Padding `5,1`

**`GridTracksPlainLyricsBadgeStyle`** (pour paroles brutes) :
- `Content` = `"lyrics"`
- Foreground `#999999`, Background `#0FFFFFFF`, BorderBrush `#1FFFFFFF`, BorderThickness `1`, CornerRadius `4`, Padding `5,1`

Les deux styles partagent : `FontSize 9`, `FontWeight SemiBold`, `Margin 0`, `Cursor Hand`.

### 5. XAML — 6 pages de listes

Dans chaque page concernée, remplacer :

```xml
<HyperlinkButton x:Uid="lyricsTag"
                 Visibility="{x:Bind ViewModel.LyricsExists, Converter={StaticResource BoolToVisibilityConverter}}"
                 Style="{StaticResource GridTracksLyricsHyperlinkButtonStyle}"
                 Command="{x:Bind ViewModel.LyricsOpenCommand}" />
```

par :

```xml
<HyperlinkButton
    Visibility="{x:Bind LyricsType, Converter={StaticResource LyricsTypeVisibilityConverter}, ConverterParameter=Synchronized, Mode=OneWay}"
    Style="{StaticResource GridTracksSyncLyricsBadgeStyle}"
    Command="{x:Bind LyricsOpenCommand}" />
<HyperlinkButton
    Visibility="{x:Bind LyricsType, Converter={StaticResource LyricsTypeVisibilityConverter}, ConverterParameter=Plain, Mode=OneWay}"
    Style="{StaticResource GridTracksPlainLyricsBadgeStyle}"
    Command="{x:Bind LyricsOpenCommand}" />
```

Note : le binding `x:Bind` s'applique directement sur `TrackViewModel` (élément de la liste), pas sur le ViewModel de la page.

Pages concernées : `AlbumPage.xaml`, `ArtistPage.xaml`, `ListeningPage.xaml`, `TracksPage.xaml`, `PlaylistPage.xaml`, `SearchPage.xaml`.

### 6. `.gitignore`

Ajouter `.superpowers/` si absent.

## Hors scope

- Modifier l'affichage des paroles dans `FullScreenControl` (lecteur plein écran) — déjà différencié via `IsSynchronizedLyrics`.
- Changer le comportement de `LyricsOpenCommand` (ouvre toujours la dialog avec les paroles brutes).
- Ajouter un tooltip ou une info supplémentaire au survol.

## Tests

Aucun test unitaire nouveau requis : `TrackViewModel.LyricsType` est un simple délégué vers un service déjà testé. Le converter `LyricsTypeVisibilityConverter` est suffisamment simple pour ne pas nécessiter de test dédié. La validation se fait visuellement sur les 6 pages.
