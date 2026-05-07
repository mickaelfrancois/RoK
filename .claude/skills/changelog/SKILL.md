---
name: changelog
description: Génère une entrée CHANGELOG.md depuis le dernier tag git, en français, puis crée le commit et le tag de release.
---

## Flux d'exécution

### Étape 1 — Vérification de la branche

```bash
git branch --show-current
```

Si la branche courante est `master` ou `main` :
- Lire la version depuis `src/Presentation/Package.appxmanifest` (attribut `Version` de l'élément `<Identity`) — le format est `X.Y.Z.0`, ne garder que `X.Y.Z`
- Créer et basculer sur la branche `bump-X.Y.Z` :
  ```bash
  git checkout -b bump-X.Y.Z
  ```

### Étape 2 — Lecture de la version cible

Extraire la version depuis `src/Presentation/Package.appxmanifest` :
```bash
grep -oP 'Version="\K[^"]+' src/Presentation/Package.appxmanifest | head -1
```
Tronquer le dernier segment `.0` pour obtenir `X.Y.Z`.

### Étape 3 — Collecte des commits depuis le dernier tag

```bash
git log $(git describe --tags --abbrev=0)..HEAD --pretty=format:"%s"
```

Filtrer et mapper les types de commits conventionnels :

| Type de commit | Section changelog |
|---|---|
| `feat` | `### Ajouté` |
| `fix` | `### Corrigé` |
| `refactor`, `perf` | `### Modifié` |
| Footer `BREAKING CHANGE` | `### Cassant` |
| `docs`, `test`, `chore`, `build`, `ci`, `style` | *(ignorer)* |

Pour chaque commit retenu, **reformuler la description en français** à partir du message de commit (supprimer le préfixe `type(scope): `, reformuler en une phrase nominale claire et concise adaptée à un utilisateur final).

### Étape 4 — Mise à jour de CHANGELOG.md

Insérer une nouvelle entrée **en tête du fichier**, juste après la ligne `# ChangeLog`, en respectant scrupuleusement ce format :

```markdown
## [X.Y.Z] Store – DD mois YYYY

### Ajouté

- ...

### Modifié

- ...

### Corrigé

- ...

--

```

Règles de format :
- La date est en français : `29 avril 2026` (pas de zéro devant le jour, mois en minuscule)
- Omettre une section si elle est vide (ne pas laisser de section vide)
- Séparer les entrées avec `--` suivi d'une ligne vide
- Ne pas modifier les entrées existantes

### Étape 5 — Validation utilisateur

Afficher le bloc de changelog généré et demander confirmation :

> Voici le changelog généré pour la version X.Y.Z. Confirmes-tu ? (oui / modifications à apporter)

Attendre la réponse avant de continuer. Si l'utilisateur demande des modifications, les appliquer et redemander validation.

### Étape 6 — Commit et tag (après validation)

Stager les fichiers modifiés :
```bash
git add CHANGELOG.md
```

Créer le commit :
```bash
git commit -m "build: bump version to vX.Y.Z"
```

Créer le tag :
```bash
git tag vX.Y.Z
```

Confirmer à l'utilisateur : branche, commit hash, tag créé.
