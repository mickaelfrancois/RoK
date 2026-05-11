# Design Spec — Skill `work-guidelines`

**Date:** 2026-05-09  
**Status:** Approved

---

## Objectif

Créer un skill Claude Code invoqué automatiquement à chaque début de session pour établir et appliquer les règles de workflow git du projet. Conçu pour être intégré dans un plugin.

---

## Architecture

Un seul fichier skill `.claude/skills/work-guidelines/SKILL.md`, déclenché automatiquement par Claude au démarrage de chaque session (description frontmatter : `"Use at the start of every conversation to establish git workflow rules"`).

Le skill est structuré en deux phases distinctes :

```
Session start → [Phase INIT] → Vérification branche → Travail normal
                                                              │
                                                              ↓
                                              [Phase END] → Squash ? → finishing-a-development-branch
```

Les règles sont numérotées pour être facilement extensibles dans le plugin.

---

## Phase INIT — Début de session

Déclenchement : à l'invocation du skill, immédiatement.

**Action :** exécuter `git branch --show-current`.

### Cas 1 — Branche `master` ou `main`
Claude stoppe le travail et demande le nom de la branche feature à créer. Si le contexte de la conversation permet de le déduire, Claude propose un nom. Puis exécute `git checkout -b <nom>` avant de poursuivre.

### Cas 2 — Branche feature
Claude affiche un message court : `Branche \`<nom>\` — règles de travail actives.` et continue normalement. Pas d'interruption.

**Non-bloquant :** une fois la branche confirmée, Claude n'intervient plus sur le sujet sauf si une commande git tente de commiter sur `master`.

---

## Phase END — Fin de travail

Déclenchement : avant de déclarer le travail terminé, avant d'invoquer `finishing-a-development-branch` ou de proposer un PR/merge.

**Condition :** s'il y a au moins 2 commits sur la branche par rapport à `master` (`git log master..HEAD`).

**Action :** afficher la liste des commits de la branche et demander :

```
Travail terminé. Tu as N commits sur cette branche :

  abc1234 feat: add review-pr skill
  def5678 fix: improve robustness
  ...

→ Squasher en un seul commit ? [O]ui / [N]on
```

### Si "Oui"
1. `git reset --soft $(git merge-base HEAD master)` — ramène tous les commits en staged
2. Proposer un message de commit consolidé basé sur les messages existants
3. Commiter avec le message validé

### Si "Non"
Continuer directement vers `finishing-a-development-branch` sans modifier les commits.

---

## Localisation

Fichier : `.claude/skills/work-guidelines/SKILL.md`

Invocation : automatique à chaque début de session (description frontmatter suffit).

---

## Règles actives (v1)

1. **Branche obligatoire** : ne jamais travailler directement sur `master` ou `main` — créer une branche feature avant tout commit.
2. **Squash en fin de travail** : avant de terminer, proposer de squasher tous les commits de la branche en un seul si plusieurs commits existent.

*Le fichier est conçu pour accueillir des règles supplémentaires numérotées.*

---

## Contraintes

- Le skill ne commite jamais sans montrer le message proposé à l'utilisateur
- Le squash utilise `git reset --soft` (non-destructif sur le working tree)
- La Phase END n'est déclenchée que si ≥ 2 commits existent sur la branche
