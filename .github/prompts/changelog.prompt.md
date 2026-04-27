---
mode: ask
description: Generate a new changelog entry from git commits since the last tag
---

Generate a new changelog entry for `CHANGELOG.md` based on the git commits below.

## Commits since last tag

Run this command and paste the output here:

git log $(git describe --tags --abbrev=0)..HEAD --pretty=format:"%s" --no-merges


## Rules
- Version follows semantic versioning: MAJOR.MINOR.PATCH.
- Date written in French (e.g., "27 avril 2026").
- Sections order: Ajouté, Modifié, Corrigé (keep empty sections).
- Entries separated by `--` between versions.
- Each entry is a concise human-readable sentence in French.
- Reference GitHub issues with `(#NNN)` when applicable.

## Commit mapping
- `feat:` → Ajouté
- `fix:` → Corrigé
- `refactor:`, `perf:`, `chore:`, `style:`, `docs:`, `build:` → Modifié
- `BREAKING CHANGE:` → Ajouté with ⚠️ prefix
- Ignore: `chore(release):`, `ci:`, `test:`

## Output
Insert the new block at the top of [CHANGELOG.md](../../CHANGELOG.md), above the previous version, without modifying existing entries.