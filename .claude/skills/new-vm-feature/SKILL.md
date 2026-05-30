---
name: new-vm-feature
description: Scaffolde un nouveau ViewModel Presentation pour Rok et son cablage DI (incluant la variante keyed "Search" si besoin), plus le slice CQRS Application associe si une nouvelle requete est necessaire. A utiliser pour "ajouter un ViewModel", "nouvelle page/feature UI", "nouvelle vue".
disable-model-invocation: true
---

# Nouveau ViewModel + cablage DI (Rok)

Le piege de ce projet n'est pas d'ecrire le VM mais de **l'enregistrer correctement** dans
`src/Presentation/DependencyInjection.cs` (`AddLogic`) — un VM non enregistre echoue a la
resolution au runtime. Cette skill couvre les deux parties.

## Partie A — ViewModel (Presentation)

1. Cree le VM sous `src/Presentation/ViewModels/<Area>/`, en `CommunityToolkit.Mvvm`
   (`ObservableObject`, commandes source-gen `[RelayCommand]`). **Aucune reference a un controle
   XAML** (regle CLAUDE.md — les VMs ne touchent jamais l'UI).
2. Decoupe selon le pattern existant si la feature est riche : `XViewModel` + `XDataLoader`
   + services (`XSelectionManager`, `XStateManager`, `XPlaybackService`) + message handlers.
   Regarde `ViewModels/Albums/` comme reference complete.

3. **Enregistre dans `Presentation/DependencyInjection.cs`** (methode `AddLogic`), dans la
   section de l'aire concernee :

```csharp
// <Area> ViewModel, services and handlers
services.AddSingleton<XViewModel>();          // Singleton si etat partage app-wide (liste)
                                              // AddTransient si vue de detail (un element)
services.AddTransient<XDataLoader>();
services.AddTransient<IXProvider, XProvider>();
// ... autres services/handlers de l'aire
```

Choix Singleton vs Transient (observe dans le code) : les VMs de **liste** (Albums, Artists,
Tracks, Playlists) sont `Singleton` ; les VMs de **detail** (AlbumViewModel, ArtistViewModel,
TrackViewModel, PlaylistViewModel) sont `Transient`.

4. **Variante keyed "Search"** — si la feature apparait aussi dans la SearchPage avec un
   constructeur reduit, ajoute une fabrique keyed (pattern `SearchAlbums` / `SearchArtists` /
   `SearchTracks`) :

```csharp
services.AddKeyedTransient<XViewModel>("SearchX", (sp, _) =>
    new XViewModel(
        sp.GetRequiredService<...>(),
        // ... uniquement les dependances voulues pour la recherche
        sp.GetRequiredService<ILogger<XViewModel>>()));
```

## Partie B — slice CQRS Application (si une nouvelle requete est necessaire)

Les handlers sont **auto-enregistres** par le mediator source-gen (`AddMediator()`) — aucun
cablage manuel. Deux fichiers seulement, sous
`src/Rok.Application/Features/<Area>/Requests/` :

1. `X{Action}Request.cs` — la requete **ET** son validator colocalises dans le meme fichier
   (pattern du projet, cf. `AddRadioStationRequest.cs`) :

```csharp
namespace Rok.Application.Features.<Area>.Requests;

public class X{Action}Request : IRequest<Result<TResult>>
{
    public string Foo { get; set; } = string.Empty;
}

public sealed class X{Action}RequestValidator : Validator<X{Action}Request>
{
    public X{Action}RequestValidator()
    {
        Rule(x => x.Foo).Required().MaxLength(200);
    }
}
```

2. `X{Action}RequestHandler.cs` — handler en primary constructor, retour `Result<T>` :

```csharp
namespace Rok.Application.Features.<Area>.Requests;

public class X{Action}RequestHandler(IXRepository repository, TimeProvider timeProvider)
    : IRequestHandler<X{Action}Request, Result<TResult>>
{
    public async Task<Result<TResult>> Handle(X{Action}Request message, CancellationToken cancellationToken)
    {
        // ... ; erreurs via Result<T>.Fail(new NotFoundError/ConflictError/...)
        return Result<TResult>.Ok(value);
    }
}
```

## Verifier

```bash
dotnet build /p:Platform=x64
dotnet test /p:Platform=x64
```

Si tu as touche du XAML pour brancher le VM (page, DataTemplate, binding), delegue la relecture
au subagent `winui-xaml-reviewer` et n'oublie pas le **smoke test manuel** de l'app WinUI.
