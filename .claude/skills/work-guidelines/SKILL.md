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
