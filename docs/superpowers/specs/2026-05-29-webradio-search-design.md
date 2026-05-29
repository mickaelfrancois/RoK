# Webradio Search — Design

**Date** : 2026-05-29
**Branche cible** : `feat/webradio-phase-1` (ou nouvelle `feat/webradio-search`)
**Auteur** : Mickaël FRANCOIS (avec assistant IA)
**Scope** : phase 2 de la feature webradio — recherche de stations via Radio-Browser API depuis la page "Mes radios".

## Contexte

La phase 1 (`2026-05-28-webradio-phase-1-design.md`) a livré :

- `RadioStationEntity` + table SQLite + Migration11.
- Dialogues d'ajout manuel (`AddRadioStationDialog`) et de lecture URL ad-hoc (`PlayRadioUrlDialog`).
- `RadiosPage` avec grille des favoris, lecture, suppression confirmée, i18n FR/EN.
- Moteur de lecture radio (NAudio + résolveur d'URL `.pls`/`.m3u`).

**Limite actuelle** : pour ajouter une webradio, l'utilisateur doit connaître et saisir manuellement l'URL du flux. C'est inutilisable pour un usage grand public — l'utilisateur ne sait pas par cœur l'URL de `de1.radio-paris.example/jazz`.

**Objectif de cette phase** : ajouter une **recherche de webradios** dans l'application, alimentée par l'API publique [Radio-Browser](https://www.radio-browser.info/), afin que l'utilisateur puisse trouver une station par nom et l'ajouter aux favoris (ou la tester immédiatement) sans connaître l'URL.

## Décisions de design

Récapitulatif des arbitrages effectués lors du brainstorming :

| # | Question | Décision |
|---|---|---|
| 1 | Source des stations | **Radio-Browser API** (gratuit, ~50 000 stations indexées, sans clé) |
| 2 | Point d'entrée UX | Bouton "Rechercher" dans la `CommandBar` de `RadiosPage` qui ouvre un dialogue |
| 3 | Action sur un résultat | Clic carte = lecture immédiate ; clic ★ = ajout aux favoris ; clic droit = menu (Play, Ajouter, Site web, Copier URL) |
| 4 | Filtres | **Texte libre uniquement** (recherche par nom). Pas de filtres pays/genre/codec en v1 |
| 5 | Disposition | Dialogue/flyout par-dessus la page Radios (`ContentDialog`) |
| 6 | Lecture pendant le dialogue | Le dialogue **reste ouvert**, la lecture démarre en arrière-plan, l'utilisateur peut tester plusieurs stations à la suite |
| 7 | Métadonnées persistées sur un favori | Ajouter `StationUuid`, `FaviconUrl`, `CountryCode`, `Codec`, `Bitrate` à l'entité (Migration12) |
| 8 | Déclenchement de la recherche | **Sur Enter ou bouton loupe** (pas de debounce sur la saisie) |
| 9 | Failover / miroirs Radio-Browser | **Endpoint unique** par défaut (`de1.api.radio-browser.info`), surchargeable via `appsettings.json` |

## Architecture globale

Stratification existante respectée (`Domain → Application → Infrastructure / Presentation`).

```
Presentation
  Dialogs/SearchRadioStationsDialog.xaml(.cs)        ← nouveau
  ViewModels/Radio/SearchRadioStationsViewModel.cs   ← nouveau
  Pages/RadiosPage.xaml                              ← +1 AppBarButton "Rechercher"
  Pages/RadiosPage.xaml.cs                           ← +1 handler clic
  DependencyInjection.cs                             ← +1 enregistrement VM (Transient)
                  │
                  ▼ (via IMediator)
Application
  Features/Radios/Requests/
    SearchRadioStationsRequest.cs                    ← nouveau (+ Validator + Handler)
    AddRadioStationRequest.cs                        ← étendu (5 champs)
  Features/Radios/Services/
    IRadioBrowserClient.cs                           ← nouveau seam
  Dto/
    RadioSearchResultDto.cs                          ← nouveau
    RadioStationDto.cs                               ← étendu (5 champs)
  Options/
    RadioBrowserOptions.cs                           ← nouveau
                  │
                  ▼
Infrastructure
  RadioBrowser/
    RadioBrowserClient.cs                            ← impl IRadioBrowserClient
    RadioBrowserStationResponse.cs                   ← contrat JSON interne
    Mapping/RadioBrowserStationMapping.cs            ← mapping JSON → DTO
  Repositories/RadioStationRepository.cs             ← +5 colonnes (SELECT/INSERT)
  Migration/Migration12.cs                           ← nouveau (ALTER TABLE)
  DependencyInjection.cs                             ← +AddHttpClient typé + Migration12

Domain
  Entities/RadioStationEntity.cs                     ← +5 propriétés
```

### Flux d'une recherche

1. L'utilisateur ouvre le dialogue depuis la `CommandBar` de `RadiosPage` (icône loupe).
2. Il saisit du texte (≥ 2 caractères), presse `Enter` ou clique le bouton loupe interne.
3. `SearchRadioStationsViewModel.SearchAsync()` envoie `SearchRadioStationsRequest` via `IMediator`.
4. Le handler appelle `IRadioBrowserClient.SearchByNameAsync(query, limit, ct)`.
5. `RadioBrowserClient` interroge `GET /json/stations/byname/{encoded}?limit=N&hidebroken=true&order=votes&reverse=true`, mappe la réponse JSON vers `RadioSearchResultDto`.
6. Le `ListView` du dialogue se met à jour. Clic carte ou bouton ▶ = `PlayRadioUrlRequest` (le dialogue reste ouvert). Clic ★ = `AddRadioStationRequest` enrichi, puis `InfoBar` info ("Ajouté aux favoris") ou ("Cette station est déjà dans tes favoris" en cas de doublon).
7. À la fermeture du dialogue, `RadiosViewModel.LoadAsync()` est rappelé pour recharger la grille des favoris.

## Modèle de données

### Domain — `RadioStationEntity`

```csharp
[Table("RadioStations")]
public class RadioStationEntity : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string StreamUrl { get; set; } = string.Empty;
    public string? HomepageUrl { get; set; }
    public string? StationUuid { get; set; }     // NEW — id stable Radio-Browser (UUID 36 char)
    public string? FaviconUrl { get; set; }      // NEW — URL d'image (favicon/logo)
    public string? CountryCode { get; set; }     // NEW — ISO alpha-2 en lowercase ("fr", "us")
    public string? Codec { get; set; }           // NEW — "MP3" | "AAC" | "AAC+" | "OGG" | ...
    public int? Bitrate { get; set; }            // NEW — kbps (null si inconnu)
    public DateTime AddedAt { get; set; }
    public DateTime? LastListen { get; set; }
}
```

Toutes les nouvelles colonnes sont **nullables** : les favoris créés manuellement (formulaire `AddRadioStationDialog` ou `PlayRadioUrlDialog`) restent valides sans renseigner ces champs.

### Migration12

`Migration11` existe déjà et crée la table `RadioStations`. **Une nouvelle `Migration12` est nécessaire** pour ajouter les colonnes (ne pas modifier `Migration11`, déjà appliquée chez l'utilisateur).

```csharp
namespace Rok.Infrastructure.Migration;

public class Migration12 : IMigration
{
    public int TargetVersion => 12;

    public void Apply(IDbConnection connection)
    {
        connection.Execute("ALTER TABLE RadioStations ADD COLUMN StationUuid TEXT NULL;");
        connection.Execute("ALTER TABLE RadioStations ADD COLUMN FaviconUrl  TEXT NULL;");
        connection.Execute("ALTER TABLE RadioStations ADD COLUMN CountryCode TEXT NULL;");
        connection.Execute("ALTER TABLE RadioStations ADD COLUMN Codec       TEXT NULL;");
        connection.Execute("ALTER TABLE RadioStations ADD COLUMN Bitrate     INTEGER NULL;");
    }
}
```

Pas de back-fill nécessaire (colonnes nullables). Enregistrer `services.AddSingleton<IMigration, Migration12>();` après `Migration11` dans `Rok.Infrastructure/DependencyInjection.cs`.

### DTOs

```csharp
public record RadioStationDto(
    long Id,
    string Name,
    string StreamUrl,
    string? HomepageUrl,
    string? StationUuid,        // NEW
    string? FaviconUrl,         // NEW
    string? CountryCode,        // NEW
    string? Codec,              // NEW
    int? Bitrate,               // NEW
    DateTime AddedAt,
    DateTime? LastListen);

public record RadioSearchResultDto(
    string Name,
    string StreamUrl,           // = url_resolved si présent, sinon url
    string? HomepageUrl,
    string? StationUuid,
    string? FaviconUrl,
    string? CountryCode,        // déjà en lowercase au mapping
    string? Codec,
    int? Bitrate);
```

### Index et dédup

L'index unique existant `UX_RadioStations_StreamUrl` (créé par `Migration11`) suffit en v1 pour la dédup. Le handler convertit la `SqliteException 19` en `ConflictError("radio.duplicate")` (comportement existant inchangé).

## Couche Application

### Seam HTTP — `IRadioBrowserClient`

```csharp
namespace Rok.Application.Features.Radios.Services;

public interface IRadioBrowserClient
{
    Task<IReadOnlyList<RadioSearchResultDto>> SearchByNameAsync(
        string query,
        int limit,
        CancellationToken cancellationToken);
}
```

Une seule méthode v1. Si plus tard on veut filtrer par tag/pays/codec, ajouter des surcharges sans casser l'existant.

### Use case — `SearchRadioStationsRequest`

```csharp
public sealed class SearchRadioStationsRequest : IRequest<Result<IReadOnlyList<RadioSearchResultDto>>>
{
    public string Query { get; set; } = string.Empty;
    public int Limit { get; set; } = 50;
}

public sealed class SearchRadioStationsRequestValidator : Validator<SearchRadioStationsRequest>
{
    public SearchRadioStationsRequestValidator()
    {
        Rule(x => x.Query).Required().MinLength(2).MaxLength(100);
        Rule(x => x.Limit).Must(l => l is > 0 and <= 200);
    }
}

public sealed class SearchRadioStationsRequestHandler(IRadioBrowserClient client)
    : IRequestHandler<SearchRadioStationsRequest, Result<IReadOnlyList<RadioSearchResultDto>>>
{
    public async Task<Result<IReadOnlyList<RadioSearchResultDto>>> Handle(
        SearchRadioStationsRequest message,
        CancellationToken ct)
    {
        try
        {
            IReadOnlyList<RadioSearchResultDto> results =
                await client.SearchByNameAsync(message.Query, message.Limit, ct);

            return Result<IReadOnlyList<RadioSearchResultDto>>.Ok(results);
        }
        catch (HttpRequestException ex)
        {
            return Result<IReadOnlyList<RadioSearchResultDto>>.Fail(
                new OperationError("radio.search_failed", ex.Message));
        }
        catch (TaskCanceledException)
        {
            return Result<IReadOnlyList<RadioSearchResultDto>>.Fail(
                new OperationError("radio.search_timeout", "Radio search timed out."));
        }
    }
}
```

### Extension `AddRadioStationRequest`

```csharp
public class AddRadioStationRequest : IRequest<Result<long>>
{
    public string Name { get; set; } = string.Empty;
    public string StreamUrl { get; set; } = string.Empty;
    public string? HomepageUrl { get; set; }
    public string? StationUuid { get; set; }    // NEW
    public string? FaviconUrl { get; set; }     // NEW
    public string? CountryCode { get; set; }    // NEW
    public string? Codec { get; set; }          // NEW
    public int? Bitrate { get; set; }           // NEW
}

public sealed class AddRadioStationRequestValidator : Validator<AddRadioStationRequest>
{
    public AddRadioStationRequestValidator()
    {
        // Règles existantes inchangées :
        Rule(x => x.Name).Required().MaxLength(200);
        Rule(x => x.StreamUrl).Required().Must(BeAbsoluteHttpUri).Message("Must be an absolute http(s) URL.");

        // NEW — uniquement les règles correspondant aux nouveaux champs :
        Rule(x => x.FaviconUrl).Must(BeAbsoluteHttpUriOrNull);     // tolère null, sinon URL absolue http(s)
        Rule(x => x.StationUuid).MaxLength(64);                    // un UUID = 36 caractères
        Rule(x => x.CountryCode).MaxLength(2);                     // ISO alpha-2
        Rule(x => x.Codec).MaxLength(20);
        Rule(x => x.Bitrate).Must(b => b is null or >= 0);
    }

    // Helper existant — réutilisé tel quel
    private static bool BeAbsoluteHttpUri(string? value) =>
        value is not null &&
        Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    // Helper à ajouter
    private static bool BeAbsoluteHttpUriOrNull(string? value) =>
        string.IsNullOrEmpty(value) || BeAbsoluteHttpUri(value);
}
```

Le handler existant est étendu pour copier les 5 nouveaux champs sur l'entité. La conversion `SqliteException 19 → ConflictError("radio.duplicate")` reste identique.

**Le dialogue manuel `AddRadioStationDialog` n'est pas modifié** : il continue à n'exposer que Nom + StreamUrl + HomepageUrl. Les 5 nouveaux champs restent `null` pour les ajouts manuels, sans friction supplémentaire.

### Options — `RadioBrowserOptions`

```csharp
namespace Rok.Application.Options;

public sealed class RadioBrowserOptions
{
    public string BaseAddress { get; set; } = "https://de1.api.radio-browser.info/";
    public int TimeoutSeconds { get; set; } = 8;
    public string UserAgent { get; set; } = "Rok/1.0";
}
```

Valeurs par défaut inline. Surcharge possible via un bloc `"RadioBrowser": { ... }` dans `appsettings.json`. Pas de clé API → pas de modification de `appsettings.template.json`.

## Couche Infrastructure

### Arborescence

```
Rok.Infrastructure/RadioBrowser/
  RadioBrowserClient.cs
  RadioBrowserStationResponse.cs
  Mapping/RadioBrowserStationMapping.cs
```

### Contrat JSON interne

```csharp
internal sealed class RadioBrowserStationResponse
{
    [JsonPropertyName("stationuuid")]   public string? StationUuid { get; set; }
    [JsonPropertyName("name")]          public string? Name { get; set; }
    [JsonPropertyName("url")]           public string? Url { get; set; }
    [JsonPropertyName("url_resolved")]  public string? UrlResolved { get; set; }
    [JsonPropertyName("homepage")]      public string? Homepage { get; set; }
    [JsonPropertyName("favicon")]       public string? Favicon { get; set; }
    [JsonPropertyName("countrycode")]   public string? CountryCode { get; set; }
    [JsonPropertyName("codec")]         public string? Codec { get; set; }
    [JsonPropertyName("bitrate")]       public int? Bitrate { get; set; }
    [JsonPropertyName("lastcheckok")]   public int? LastCheckOk { get; set; }
}
```

Tous nullables → robuste contre un payload partiel.

### Mapping JSON → DTO

```csharp
internal static class RadioBrowserStationMapping
{
    public static RadioSearchResultDto? ToDto(this RadioBrowserStationResponse r)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) return null;

        string? stream = !string.IsNullOrWhiteSpace(r.UrlResolved) ? r.UrlResolved : r.Url;
        if (string.IsNullOrWhiteSpace(stream)) return null;

        return new RadioSearchResultDto(
            Name:        r.Name.Trim(),
            StreamUrl:   stream.Trim(),
            HomepageUrl: NullIfEmpty(r.Homepage),
            StationUuid: NullIfEmpty(r.StationUuid),
            FaviconUrl:  NullIfEmpty(r.Favicon),
            CountryCode: NullIfEmpty(r.CountryCode)?.ToLowerInvariant(),
            Codec:       NullIfEmpty(r.Codec),
            Bitrate:     r.Bitrate is > 0 ? r.Bitrate : null);
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
```

Règles :
- `url_resolved` préféré à `url` (plus fiable).
- Stations sans nom ou sans URL → ignorées (mapping retourne `null`).
- `CountryCode` mis en lowercase pour s'aligner avec `CountryCodeToImageSourceConverter`.
- `Bitrate = 0` traité comme inconnu (`null`).

### Client

```csharp
internal sealed class RadioBrowserClient(HttpClient http, ILogger<RadioBrowserClient> logger)
    : IRadioBrowserClient
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<RadioSearchResultDto>> SearchByNameAsync(
        string query, int limit, CancellationToken ct)
    {
        string encoded = Uri.EscapeDataString(query);
        string path = $"json/stations/byname/{encoded}?limit={limit}&hidebroken=true&order=votes&reverse=true";

        logger.LogDebug("Radio-Browser search: query='{Query}' limit={Limit}", query, limit);

        using HttpResponseMessage response = await http.GetAsync(path, ct);
        response.EnsureSuccessStatusCode();

        RadioBrowserStationResponse[]? raw =
            await response.Content.ReadFromJsonAsync<RadioBrowserStationResponse[]>(JsonOpts, ct);

        if (raw is null) return [];

        return raw.Select(r => r.ToDto())
                  .Where(d => d is not null)
                  .Select(d => d!)
                  .ToArray();
    }
}
```

Paramètres d'URL :
- `limit` : borné par le validator Application (1..200).
- `hidebroken=true` : Radio-Browser filtre déjà côté serveur les stations cassées.
- `order=votes&reverse=true` : tri par popularité décroissante.

### Enregistrement DI

```csharp
// Rok.Infrastructure/DependencyInjection.cs
services.AddHttpClient<IRadioBrowserClient, RadioBrowserClient>((sp, client) =>
{
    RadioBrowserOptions opts = sp.GetRequiredService<IOptions<RadioBrowserOptions>>().Value;
    client.BaseAddress = new Uri(opts.BaseAddress);
    client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);
    client.DefaultRequestHeaders.UserAgent.ParseAdd(opts.UserAgent);
});

services.AddSingleton<IMigration, Migration12>();   // après Migration11
```

Le `services.Configure<RadioBrowserOptions>(configuration.GetSection("RadioBrowser"))` se fait dans `Presentation/App.xaml.cs` à côté des autres `Configure<>` (`MusicDataApiOptions`, `TelemetryOptions`, etc.).

## Couche Presentation

### `SearchRadioStationsViewModel`

```csharp
public sealed partial class SearchRadioStationsViewModel : ObservableObject
{
    private readonly IMediator _mediator;
    private readonly ResourceLoader _resourceLoader;

    [ObservableProperty]
    private string _query = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoResults))]
    private bool _isSearching;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    [NotifyPropertyChangedFor(nameof(HasNoResults))]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoResults))]
    private bool _hasSearched;

    public ObservableCollection<RadioSearchResultDto> Results { get; } = [];

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool HasNoResults => HasSearched && !IsSearching && Results.Count == 0 && !HasError;

    public SearchRadioStationsViewModel(IMediator mediator, ResourceLoader resourceLoader)
    {
        _mediator = mediator;
        _resourceLoader = resourceLoader;
        Results.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasNoResults));
    }

    [RelayCommand(CanExecute = nameof(CanSearch))]
    private async Task SearchAsync(CancellationToken ct)
    {
        IsSearching = true;
        ErrorMessage = null;
        Results.Clear();
        try
        {
            Result<IReadOnlyList<RadioSearchResultDto>> result =
                await _mediator.Send(
                    new SearchRadioStationsRequest { Query = Query.Trim(), Limit = 50 }, ct);

            HasSearched = true;

            if (result.IsFailure)
            {
                ErrorMessage = ResolveErrorMessage(result.Errors.First());
                return;
            }
            foreach (RadioSearchResultDto r in result.Value)
                Results.Add(r);
        }
        finally
        {
            IsSearching = false;
        }
    }

    private bool CanSearch() => !IsSearching && Query?.Trim().Length >= 2;

    [RelayCommand]
    private Task PlayAsync(RadioSearchResultDto r) =>
        _mediator.Send(new PlayRadioUrlRequest { Url = r.StreamUrl });

    // Le résultat (succès / doublon / erreur) est exposé via FeedbackMessage + FeedbackSeverity
    // (propriétés ObservableProperty) que le dialogue lie à un second InfoBar transitoire.
    [RelayCommand]
    private async Task AddToFavoritesAsync(RadioSearchResultDto r)
    {
        Result<long> result = await _mediator.Send(new AddRadioStationRequest
        {
            Name = r.Name,
            StreamUrl = r.StreamUrl,
            HomepageUrl = r.HomepageUrl,
            StationUuid = r.StationUuid,
            FaviconUrl = r.FaviconUrl,
            CountryCode = r.CountryCode,
            Codec = r.Codec,
            Bitrate = r.Bitrate,
        });

        if (result.IsSuccess)
            SetFeedback(_resourceLoader.GetString("radioFavoriteAdded"), FeedbackKind.Success);
        else if (result.Errors.FirstOrDefault() is ConflictError)
            SetFeedback(_resourceLoader.GetString("radioFavoriteDuplicate"), FeedbackKind.Info);
        else
            SetFeedback(ResolveErrorMessage(result.Errors.First()), FeedbackKind.Error);
    }

    // Détails d'implémentation : FeedbackMessage est rebound côté XAML sur un second InfoBar
    // (Severity dérivée de FeedbackKind via converter). Le code-behind démarre un DispatcherTimer
    // 2,5 s pour Success/Info qui efface FeedbackMessage à expiration.

    // Convertit un code d'erreur (radio.search_failed, radio.search_timeout) vers un libellé localisé.
    // Fallback sur le message brut de l'Error si la clé n'existe pas.
    private string ResolveErrorMessage(Error error)
    {
        string localized = _resourceLoader.GetString($"error.{error.Code}");
        return string.IsNullOrEmpty(localized) ? error.Message : localized;
    }
}
```

### `SearchRadioStationsDialog.xaml`

`ContentDialog` avec :

- `TextBox` (lié à `ViewModel.Query`, `UpdateSourceTrigger=PropertyChanged`) + bouton loupe lié à `SearchCommand`.
- `KeyDown` sur le `TextBox` : déclenche `SearchCommand` si la touche est `Enter`.
- `DefaultButton="None"` pour que `Enter` n'active pas le bouton `Close`.
- `InfoBar` rouge en haut (visible si `HasError`).
- `ProgressRing` centré (visible si `IsSearching`).
- `ListView` des résultats (template : logo 40×40 ou fallback brand-coloured, nom en `SemiBold`, sous-titre `"FR · 128 kbps · MP3"`, drapeau via `CountryCodeToImageSourceConverter`, boutons ▶ Play et ★ Favori).
- `EmptyStateControl` "Aucun résultat" (visible si `HasNoResults`).
- `CloseButtonText="Close"` — seul bouton du dialogue.

Layout en `Grid` 3 rangées (recherche / erreur / résultats), `MinWidth="540" MinHeight="480"`.

### `SearchRadioStationsDialog.xaml.cs`

```csharp
public sealed partial class SearchRadioStationsDialog : ContentDialog
{
    public SearchRadioStationsViewModel ViewModel { get; }

    public SearchRadioStationsDialog(SearchRadioStationsViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    private void OnQueryKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && ViewModel.SearchCommand.CanExecute(null))
        {
            _ = ViewModel.SearchCommand.ExecuteAsync(null);
            e.Handled = true;
        }
    }

    private void OnResultItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is RadioSearchResultDto r)
            _ = ViewModel.PlayCommand.ExecuteAsync(r);
    }
}
```

### `RadiosPage.xaml` — nouveau bouton

À insérer en **première position** de la `CommandBar.Content` (avant "URL" et "Ajouter") :

```xml
<AppBarButton x:Uid="radiosSearch"
              Style="{StaticResource AppBarButtonCompactStyle}"
              Click="OnSearchClick">
    <AppBarButton.Icon>
        <SymbolIcon Symbol="Find"/>
    </AppBarButton.Icon>
</AppBarButton>
```

### `RadiosPage.xaml.cs` — handler

```csharp
private async void OnSearchClick(object sender, RoutedEventArgs e)
{
    SearchRadioStationsDialog dialog = new(
        App.ServiceProvider.GetRequiredService<SearchRadioStationsViewModel>())
    {
        XamlRoot = XamlRoot
    };
    await dialog.ShowAsync();
    await ViewModel.LoadAsync();  // recharge inconditionnellement les favoris
}
```

### `Presentation/DependencyInjection.cs`

```csharp
services.AddTransient<SearchRadioStationsViewModel>();
```

`Transient` pour que chaque ouverture du dialogue parte d'un état neuf (collection vide, query vide).

### i18n — clés à ajouter (`Resources.resw` FR/EN)

| Clé | FR | EN |
|---|---|---|
| `radiosSearch.[Label]` | Rechercher | Search |
| `radiosSearch.[ToolTipService.ToolTip]` | Rechercher des webradios | Search webradios |
| `searchRadioDialog.Title` | Rechercher une webradio | Search webradio |
| `searchRadioDialog.CloseButtonText` | Fermer | Close |
| `searchRadioQuery.PlaceholderText` | Nom de la station... | Station name... |
| `searchRadioSubmit.[ToolTipService.ToolTip]` | Rechercher | Search |
| `searchRadioPlay.[ToolTipService.ToolTip]` | Lire | Play |
| `searchRadioAdd.[ToolTipService.ToolTip]` | Ajouter aux favoris | Add to favorites |
| `searchRadioNoResults.Title` | Aucun résultat | No results |
| `searchRadioNoResults.Subtitle` | Essaie un autre nom. | Try a different name. |
| `error.radio.search_failed` | Impossible de joindre Radio-Browser. Vérifie ta connexion. | Cannot reach Radio-Browser. Check your connection. |
| `error.radio.search_timeout` | La recherche a expiré, réessaie. | Search timed out, try again. |
| `radioFavoriteAdded` | Ajoutée aux favoris. | Added to favorites. |
| `radioFavoriteDuplicate` | Cette station est déjà dans tes favoris. | This station is already in your favorites. |

## Gestion d'erreurs et états

### Recherche

| Situation | Détection | UX |
|---|---|---|
| Saisie < 2 caractères | `CanSearch()` retourne `false` | Bouton "Rechercher" grisé, Enter ignoré |
| Recherche en cours | `IsSearching = true` | `ProgressRing` centré, bouton grisé |
| Aucun résultat | `HasSearched && Results.Count == 0 && !HasError` | `EmptyStateControl` "Aucun résultat" |
| Timeout (8 s) | `TaskCanceledException` → `OperationError("radio.search_timeout")` | `InfoBar` rouge |
| Erreur réseau / DNS | `HttpRequestException` → `OperationError("radio.search_failed")` | `InfoBar` rouge |
| HTTP 4xx/5xx | `EnsureSuccessStatusCode()` → `HttpRequestException` | Idem |
| JSON malformé | Exception lue par `ReadFromJsonAsync` ou `null` → liste vide | Comme "Aucun résultat" |
| Fermeture pendant recherche | `CancellationToken` côté commande, timeout HTTP côté client | Aucune notification, silencieux |

### Lecture depuis le dialogue

| Situation | Détection | UX |
|---|---|---|
| Stream démarre | `PlayRadioUrlRequest` succès | Player du bas se met à jour, dialogue **reste ouvert** |
| Stream échoue (URL morte) | Géré par `IRadioStreamUrlResolver` / `IPlayerService` | Erreur affichée par le player existant (pas dupliquée dans le dialogue) |
| Play d'une 2ᵉ station | `PlayRadioUrlRequest` | Le player s'arrête et bascule (comportement existant) |

### Ajout aux favoris

| Situation | Détection | UX |
|---|---|---|
| Ajout réussi | `Result.Ok` | `InfoBar Severity="Success"` (vert) : "Ajoutée aux favoris." |
| Doublon | `ConflictError("radio.duplicate")` | `InfoBar Severity="Informational"` (bleu) : "Cette station est déjà dans tes favoris." |
| Erreur de validation | `ValidationError` | `InfoBar Severity="Error"` (rouge) avec le premier message |
| Autre échec | `OperationError` | `InfoBar Severity="Error"` (rouge) générique |

**Mécanisme d'auto-close** : `InfoBar` n'a pas de fermeture automatique native. Pour les messages transitoires (succès / doublon), le code-behind utilise un `DispatcherTimer` de 2,5 s qui passe `IsOpen=false` à expiration. Le timer est annulé/redémarré à chaque nouveau message. Les messages d'erreur restent affichés jusqu'à action utilisateur (clic croix ou nouvelle recherche qui appelle `ErrorMessage = null`).

### Edge cases

- **`url_resolved` null mais `url` = playlist `.pls`/`.m3u`** : `IRadioStreamUrlResolver` existant résout au moment du Play. Pas d'action spécifique.
- **Favicon 404 ou SSL invalide** : l'`Image` WinUI échoue silencieusement, le `Border` brand-coloured reste visible (fallback).
- **Double-clic sur "Rechercher"** : `RelayCommand` source-gen empêche la réentrée via `IsRunning` → `CanSearch` retourne false.
- **Saisie > 100 caractères** : `ValidationError` affiché en `InfoBar` (le `TextBox` n'est pas borné côté UI volontairement pour ne pas tronquer en silence).
- **Annulation à la fermeture du dialogue** : pas de `CancellationTokenSource` custom. Le `HttpClient.Timeout` (8 s) termine seul. Choix YAGNI.

### Logging (Serilog)

- `Information` : `"Radio-Browser search: query='{Query}' returned {Count} results"` (en sortie de handler).
- `Warning` : `"Radio-Browser search failed: {ExceptionType} {Message}"` (timeout, HTTP, JSON).
- `Debug` : URL complète (uniquement en mode debug).

## Tests

### `Rok.ApplicationTests`

**`SearchRadioStationsRequestHandlerTests`** (nouveau, avec `Mock<IRadioBrowserClient>`) :

| DisplayName | Vérifie |
|---|---|
| `search_should_return_results_when_client_responds` | 3 DTOs renvoyés → `Result.Ok` contenant 3 éléments |
| `search_should_return_empty_when_no_match` | Client renvoie `[]` → `Result.Ok(empty)` |
| `search_should_fail_with_search_failed_on_http_exception` | `HttpRequestException` → `OperationError("radio.search_failed")` |
| `search_should_fail_with_search_timeout_on_task_canceled` | `TaskCanceledException` → `OperationError("radio.search_timeout")` |
| `search_should_forward_cancellation_token` | CT propagé au client |
| `search_should_be_rejected_when_query_too_short` | Query = "a" → `ValidationError` |
| `search_should_be_rejected_when_limit_out_of_range` | Limit = 0 ou 300 → `ValidationError` |

**`AddRadioStationRequestHandlerTests`** (existant à étendre) :

| DisplayName | Vérifie |
|---|---|
| `add_should_persist_extended_metadata` | Tous les nouveaux champs renseignés → repository reçoit l'entité conforme |
| `add_should_accept_nullable_extended_fields` | Tous les nouveaux champs `null` → succès (cas manuel) |
| `add_should_be_rejected_when_favicon_url_is_relative` | `FaviconUrl = "favicon.ico"` → `ValidationError` |

### `Rok.Infrastructure.UnitTests`

**`RadioBrowserClientTests`** (nouveau, pattern `Mock<HttpMessageHandler>` strict — aligné avec `RadioStreamUrlResolverTests`) :

| DisplayName | Vérifie |
|---|---|
| `search_by_name_should_call_byname_endpoint_with_encoded_query` | URL appelée : `/json/stations/byname/jazz%20fm?...` |
| `search_by_name_should_include_hidebroken_and_order_votes` | URL contient `hidebroken=true&order=votes&reverse=true` |
| `search_by_name_should_attach_user_agent_header` | Header `User-Agent: Rok/1.0` |
| `search_by_name_should_map_url_resolved_when_present` | `url_resolved` non vide → `StreamUrl` = `url_resolved` |
| `search_by_name_should_fallback_to_url_when_resolved_missing` | `url_resolved` vide → `StreamUrl` = `url` |
| `search_by_name_should_lowercase_country_code` | `"countrycode":"FR"` → DTO `"fr"` |
| `search_by_name_should_skip_stations_without_name_or_url` | Stations invalides filtrées |
| `search_by_name_should_treat_bitrate_zero_as_unknown` | `bitrate: 0` → DTO `null` |
| `search_by_name_should_return_empty_list_on_empty_response` | JSON `[]` → liste vide |
| `search_by_name_should_throw_http_request_exception_on_500` | HTTP 500 → exception propagée |
| `search_by_name_should_apply_limit_parameter` | `limit=10` → URL contient `?limit=10` |

**`RadioStationRepositoryTests`** (existant à étendre) :

| DisplayName | Vérifie |
|---|---|
| `add_should_persist_all_extended_columns` | INSERT avec les 5 champs renseignés → SELECT retrouve les mêmes valeurs |
| `add_should_persist_null_extended_columns` | INSERT sans → toutes nullables = null |

**`SqliteDatabaseFixture.cs`** : ajouter `new Migration12()` à la liste des migrations.

### Tests existants à amender (compilation)

- `PlayRadioUrlRequestHandlerTests` : le `RadioStationDto` ad-hoc construit doit recevoir les 5 nouveaux champs (`null` par défaut).
- `GetRadioStationsRequestHandlerTests` : DTOs attendus à ajuster.

### Pas de tests UI

Le dialogue n'est pas testé automatiquement (cohérent avec les autres dialogues du projet : `AddRadioStationDialog`, `PlayRadioUrlDialog`). Validation manuelle au runtime.

### Couverture estimée

- ~17-20 nouveaux tests.
- ~3-5 tests existants à amender pour compiler.

## Hors scope explicite (non-objectifs)

Ces éléments sont volontairement exclus de la phase 2 :

1. **Filtres avancés** (pays, langue, genre/tag, codec, bitrate minimum). Texte libre uniquement en v1.
2. **Debounce sur la saisie**. Recherche déclenchée explicitement par Enter ou bouton.
3. **Cache local des résultats** (mémoire ou SQLite). L'API Radio-Browser est rapide (~200 ms).
4. **Failover multi-miroirs**. Endpoint unique configurable.
5. **Refresh automatique des URLs cassées via `StationUuid`**. Le champ est persisté pour cet usage futur, sans implémentation maintenant.
6. **Indication visuelle "déjà en favori"** dans la liste de résultats (irait à comparer `StationUuid` avec les favoris). À ajouter plus tard si la friction se manifeste.
7. **Recherche full-text dans les favoris**. La recherche actuelle vise Radio-Browser uniquement.
8. **Tests UI** sur le dialogue.

## Risques et mitigations

| Risque | Mitigation |
|---|---|
| Radio-Browser indisponible | `OperationError` affiché dans l'`InfoBar`, l'utilisateur peut toujours utiliser "Ajouter manuellement" et "Jouer URL" |
| URL de stream change côté Radio-Browser après ajout | Acceptable en v1 (l'utilisateur réajoute). `StationUuid` persisté ouvre la voie à un futur job de refresh |
| Volume de résultats (`order=votes`) qui sature l'UI | `limit=50` par défaut, borné à 200 par le validator |
| User-Agent absent → 403 Radio-Browser | Toujours configuré dans `AddHttpClient` via `RadioBrowserOptions.UserAgent` |
| Logo favicon avec contenu trompeur / inapproprié | Limité — Radio-Browser modère sa base. Pas de modération en aval côté Rok |
| Migration12 oubliée sur des bases existantes | `MigrationService` applique automatiquement les migrations manquantes au démarrage |

## Implémentation par ordre logique

À traduire en plan détaillé par `writing-plans`. Esquisse :

1. **Domain + Migration** : étendre `RadioStationEntity`, créer `Migration12`, enregistrer dans DI, étendre `SqliteDatabaseFixture`.
2. **Repository** : adapter `RadioStationRepository` (INSERT/SELECT), tests étendus.
3. **DTOs + AddRadioStationRequest** : étendre `RadioStationDto`, `AddRadioStationRequest` + validator + handler, étendre les tests, ajuster `PlayRadioUrlRequestHandler` et tests dépendants.
4. **Seam + Options** : créer `IRadioBrowserClient`, `RadioSearchResultDto`, `RadioBrowserOptions`.
5. **Infrastructure HTTP** : `RadioBrowserStationResponse`, `RadioBrowserStationMapping`, `RadioBrowserClient`, enregistrement DI typed client. Tests.
6. **Use case Search** : `SearchRadioStationsRequest` + Validator + Handler + tests.
7. **Presentation** : `SearchRadioStationsViewModel`, `SearchRadioStationsDialog.xaml(.cs)`, bouton `radiosSearch` dans `RadiosPage`, enregistrement DI, i18n FR/EN.
8. **Build + tests + smoke manuel** (recherche réelle Radio-Browser, ajout, doublon, lecture, fermeture).

Chaque étape doit laisser le build vert et les tests verts avant de passer à la suivante.

## Références

- Radio-Browser API : <https://api.radio-browser.info/>
- Endpoint utilisé : `GET /json/stations/byname/{searchterm}?limit=N&hidebroken=true&order=votes&reverse=true`
- Spec phase 1 : `docs/superpowers/specs/2026-05-28-webradio-phase-1-design.md`
- Convention de drapeaux Rok : `src/Presentation/Assets/Flags/{code}.png` via `CountryCodeToImageSourceConverter`
