# Work Guidelines Skill Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Créer le skill Claude Code `work-guidelines` qui s'invoque automatiquement à chaque début de session pour vérifier la branche git et proposer un squash de commits en fin de travail.

**Architecture:** Un seul fichier markdown `.claude/skills/work-guidelines/SKILL.md`. Pas de code C#, pas de tests unitaires. Le skill est structuré en deux phases : INIT (vérification de branche au démarrage) et END (squash optionnel avant de terminer).

**Tech Stack:** Claude Code skills (markdown), git

---

## File Structure

| Action | Fichier | Rôle |
|--------|---------|------|
| Create | `.claude/skills/work-guidelines/SKILL.md` | Skill principal — deux phases INIT et END |

---

### Task 1 : Créer le skill `work-guidelines`

**Files:**
- Create: `.claude/skills/work-guidelines/SKILL.md`

- [ ] **Step 1 : Créer le fichier skill**

Créer `.claude/skills/work-guidelines/SKILL.md` avec ce contenu exact :

````markdown
---
name: work-guidelines
description: Use at the start of every conversation to establish git workflow rules
---

# Skill : work-guidelines

Appliquer ces règles de travail pour toute la session.

## Phase INIT — À invoquer immédiatement au démarrage

### Règle 1 — Branche obligatoire

Exécuter :
```bash
git branch --show-current
```

- Si le résultat est `master` ou `main` : stopper et demander le nom de la branche feature à créer. Si le contexte de la conversation permet de le déduire, proposer un nom. Puis exécuter `git checkout -b <nom>` avant de poursuivre.
- Sinon : afficher `Branche \`<nom>\` — règles de travail actives.` et continuer normalement.

Une fois la branche confirmée, ne plus intervenir sur ce sujet sauf si une commande git tente de commiter sur `master`.

## Phase END — Avant de terminer le travail

Déclencher cette phase quand tu t'apprêtes à invoquer `finishing-a-development-branch`, à annoncer "implémentation terminée", ou à proposer un PR/merge.

### Règle 2 — Squash des commits

Vérifier le nombre de commits sur la branche :
```bash
git log master..HEAD --oneline
```

Si **1 seul commit** : passer directement à `finishing-a-development-branch`.

Si **2 commits ou plus** : afficher la liste et demander :

```
Travail terminé. Tu as N commits sur cette branche :

  abc1234 feat: ...
  def5678 fix: ...
  ...

→ Squasher en un seul commit ? [O]ui / [N]on
```

**Si "Oui" :**
1. Exécuter :
```bash
git reset --soft $(git merge-base HEAD master)
```
2. Proposer un message de commit consolidé basé sur les messages existants
3. Attendre la validation du message par l'utilisateur, puis commiter

**Si "Non" :** continuer directement vers `finishing-a-development-branch`.
````

- [ ] **Step 2 : Vérifier que le fichier existe et que le frontmatter est valide**

```bash
git status
```

Résultat attendu : `.claude/skills/work-guidelines/SKILL.md` listé comme nouveau fichier non suivi.

- [ ] **Step 3 : Commiter**

```bash
git add .claude/skills/work-guidelines/SKILL.md
git commit -m "feat: add work-guidelines skill for git workflow enforcement"
```

Résultat attendu : commit créé sur la branche `feat/work-guidelines-skill`.

---

### Task 2 : Vérification structurelle

**Files:** aucune modification — vérification uniquement.

- [ ] **Step 1 : Vérifier que le skill apparaît dans la liste**

Le skill doit apparaître dans la liste des skills disponibles au prochain démarrage de session Claude Code. Vérifier que le fichier est bien sous `.claude/skills/work-guidelines/SKILL.md` (et non `.claude/skills/work-guidelines.md` ou autre chemin incorrect).

```bash
ls .claude/skills/work-guidelines/
```

Résultat attendu : `SKILL.md` listé.

- [ ] **Step 2 : Vérifier le frontmatter**

Le frontmatter doit contenir exactement :
- `name: work-guidelines`
- `description: Use at the start of every conversation to establish git workflow rules`

Ces deux champs sont requis pour que Claude Code détecte et affiche le skill dans sa liste.

- [ ] **Step 3 : Vérifier les deux phases dans le contenu**

Le fichier doit contenir :
- La section `## Phase INIT` avec la commande `git branch --show-current` et les deux cas (master → créer branche / feature → continuer)
- La section `## Phase END` avec `git log master..HEAD --oneline`, la condition ≥ 2 commits, et les deux cas (Oui → squash / Non → continuer)
