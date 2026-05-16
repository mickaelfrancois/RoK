# Migration spec — Step 2 : `MiF.Result` → `CleanArch.DevKit.Mediator.Results`

> Étape 2/3 du remplacement de la stack MiF par CleanArch.DevKit. Suit `2026-05-15-cleanarch-mediator-migration-design.md` (étape 1, Mediator). Précède l'étape 3 (Messenger).

## Contexte

L'étape 1 a migré le mediator (Command/Query → Request, AddValidationBehavior, etc.). Le code applicatif retourne toujours `MiF.Result.Result` / `Result<T>` produit par `MiF.Result` 1.0.x.

Le nouveau package `CleanArch.DevKit.Mediator.Results` 1.0.0 offre un Result plus typé (record-based errors, conversions implicites, méthodes extension fluentes) **et** un `ResultBehavior` qui convertit automatiquement les `ValidationException` levées par le `ValidationBehavior` de l'étape 1 en `Result.Fail(ValidationError)` — supprimant le besoin de try/catch dans les handlers.

## Écart d'API (audit fait)

| MiF.Result | CleanArch.DevKit.Mediator.Results |
|---|---|
| `Result.Success()` | `Result.Ok()` |
| `Result<T>.Success(value)` | `Result<T>.Ok(value)` (ou conversion implicite `return value;`) |
| `Result.Fail("code", "msg")` | `Result.Fail(new SomeError("code", "msg"))` |
| `Result.Fail("msg")` | `Result.Fail(new OperationError("operation.failed", "msg"))` |
| `result.IsSuccess` | `result.IsSuccess` (inchangé) |
| `result.IsError` | `result.IsFailure` |
| `result.Error?.Code` / `.Message` | `result.Errors[0]?.Code` / `.Message` |
| `result.IsErrorType<T>()` / `TryGetError<T>` / `GetError<T>` | **N'existe pas** — utiliser `result.Errors[0] is T` |
| `IError` (interface, mutable) | `Error` (abstract record `(Code, Message)`) |
| `Result<T>` = class | `Result<T>` = readonly struct |

**Erreurs typées fournies par la lib** : `NotFoundError`, `ConflictError`, `ValidationError`, `ForbiddenError`, `UnauthorizedError`. Toutes sont `sealed record … : Error(Code, Message)`.

## Stratégie d'erreur (décidée)

**Mix typed + générique** :

- `NotFoundError.ForEntity("Track", id)` pour les 5 `Fail("NotFound", "X not found")` + 6 `Fail("X not found.")` inline → code généré : `"track.not_found"` etc.
- `ConflictError(code, msg)` pour les "already exists" (1 occurrence dans `AddTrackToPlaylistRequestHandler`)
- `OperationError(code, msg)` (record local dans `Rok.Application/Errors/`) pour les ~30 autres cas génériques — code par défaut `"operation.failed"` quand message-only

`OperationError` à créer :

```csharp
namespace Rok.Application.Errors;

using CleanArch.DevKit.Mediator.Results;

public sealed record OperationError(string Code, string Message) : Error(Code, Message);
```

## Bug pré-existant à corriger

`AddTrackToPlaylistRequestHandler.cs:31` :

```csharp
// Avant (arguments inversés : code="Track already exists in the playlist.", message="DUPLICATE")
return Result<long>.Fail("Track already exists in the playlist.", "DUPLICATE");
```

Décidé : corriger pendant la migration vers :

```csharp
return Result<long>.Fail(new ConflictError("playlist.duplicate_track", "Track already exists in the playlist."));
```

## ResultBehavior — DI

À insérer dans `Rok.Application/DependencyInjection.cs` après `AddValidationBehavior()` :

```csharp
services.AddResultBehavior(); // converts ValidationException → Result.Fail(ValidationError) for Result-returning handlers
```

`AddResultBehavior` insère le pipeline behavior à la position 0 (outermost). L'ordre d'enregistrement avec les autres pipeline behaviors est indifférent.

## Scope (audit)

- **17 fichiers source production** modifiés (handlers Application + 2 consumers Presentation/Import)
- **~12 fichiers tests** modifiés (assertions sur `result.Error.Code` etc.)
- **3 fichiers GlobalUsings** mis à jour
- **1 csproj** : swap package + version
- **1 nouveau fichier** : `Rok.Application/Errors/OperationError.cs`

Pas d'impact sur `Rok.Domain`, `Rok.Infrastructure` (n'utilisent pas Result), `Rok.Shared`.

## Risques

1. **`Result<T>` passe de class à struct** : tout `result == null` cassait. Audit grep : **0 occurrence** `Result.*==.*null` → safe.
2. **Conversions implicites** : `return value;` au lieu de `return Result<T>.Ok(value)` est tentant mais nous gardons explicite pour cohérence visuelle.
3. **Match / Bind / Map** : nouvelle lib offre des combinators async. Pas utilisés à la migration (out of scope), à introduire au besoin dans un PR ultérieur.
4. **Tests** : `result.Error.Code.Should().Be("NotFound")` doit devenir `result.Errors[0].Code.Should().Be("track.not_found")` — codes changent à cause de `ForEntity`. Recalibrer les assertions.

## Critères d'acceptation

- `dotnet build Rok.slnx /p:Platform=x64` → 0 erreur, 0 warning (TreatWarningsAsErrors)
- `dotnet test Rok.slnx /p:Platform=x64 --no-build` → tous les tests passent (~1284 actuels)
- `grep -r "MiF.Result"` → 0 occurrence dans `src/` et `tests/` (sauf docs/specs/plans)
- `grep -r ".IsError"` → 0 occurrence dans `src/` (sauf si `IsErrorType` rebondit sur les domain entities, à vérifier)
- `grep -r "\.Success("` (en contexte Result) → 0 occurrence
- Smoke test WinUI manuel (lancement app, navigation, création playlist, validation Id=0 → ValidationError dans Result)
