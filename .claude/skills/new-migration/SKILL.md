---
name: new-migration
description: Cree une nouvelle migration SQLite pour Rok (classe MigrationN + enregistrement DI + test optionnel), en suivant le pattern du depot. A utiliser pour "ajouter une migration", "nouvelle table/colonne", "changement de schema".
disable-model-invocation: true
---

# Nouvelle migration SQLite (Rok)

Scaffolde une migration en respectant le pattern exact du projet. **Ne devine pas le numero** :
calcule-le depuis l'existant.

## Etapes

### 1. Trouver le prochain numero de version

Liste `src/Rok.Infrastructure/Migration/Migration*.cs`, prends le plus grand `N`, le prochain
est `N+1`. (Au moment d'ecrire cette skill, la tete etait `Migration12` -> verifie toujours
l'etat reel, ne te fie pas a ce chiffre.)

### 2. Creer la classe `src/Rok.Infrastructure/Migration/Migration{N+1}.cs`

```csharp
namespace Rok.Infrastructure.Migration;

public class Migration{N+1} : IMigration
{
    public int TargetVersion => {N+1};

    public void Apply(IDbConnection connection)
    {
        // Une instruction par ALTER/CREATE. SQLite : un seul ADD COLUMN par ALTER TABLE.
        connection.Execute("ALTER TABLE <Table> ADD COLUMN <Colonne> <TYPE> NULL;");
    }
}
```

Regles tirees du code existant (`Migration12`, `MigrationService`) :
- `connection.Execute(...)` (extension Dapper) ; types SQLite : `TEXT`, `INTEGER`, alignement
  visuel des noms de colonnes comme dans `Migration12`.
- Colonnes ajoutees a posteriori : prefere `NULL` (SQLite refuse `NOT NULL` sans `DEFAULT` sur
  une table peuplee).
- Une migration ne touche QUE son increment ; pas de logique conditionnelle sur la version.

### 3. Enregistrer dans `src/Rok.Infrastructure/DependencyInjection.cs`

Ajoute la ligne a la suite de la derniere, dans l'ordre :

```csharp
services.AddSingleton<IMigration, Migration{N+1}>();
```

**C'est l'oubli classique** — sans cet enregistrement la migration n'est jamais appliquee
(`MigrationService` injecte `IEnumerable<IMigration>`).

### 4. Mettre a jour la doc

Le `CLAUDE.md` mentionne « current head is MigrationN » — mets-le a jour si present.

### 5. Test optionnel

Si la migration porte une logique non triviale, ajoute un test dans
`tests/UnitTests/Rok.Infrastructure.UnitTests/Migrations/Migration{N+1}Tests.cs` sur le modele
de `Migration11Tests.cs` (fixture SQLite reelle `SqliteDatabaseFixture`, DisplayName EN
snake_case, AAA).

### 6. Verifier

```bash
dotnet build /p:Platform=x64
dotnet test tests/UnitTests/Rok.Infrastructure.UnitTests/Rok.Infrastructure.UnitTests.csproj /p:Platform=x64
```

N'oublie pas : si l'entite Domain associee change, mets a jour le `[Table(...)]` et le
repository Dapper concerne.
