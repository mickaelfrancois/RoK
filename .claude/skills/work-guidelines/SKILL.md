---
name: work-guidelines
description: Use at the start of every conversation to establish git workflow rules
---

# Skill : work-guidelines

Appliquer ces règles de travail pour toute la session.

⚠ Ces règles s'appliquent pour toute la durée de la session. En particulier, la **Phase END doit être exécutée AVANT toute annonce de fin de travail**, même si plusieurs heures de conversation se sont écoulées depuis l'invocation du skill.

## Phase INIT — À invoquer immédiatement au démarrage

### Règle 1 — Branche obligatoire

Exécuter :
```bash
rtk git branch --show-current
```

- Si le résultat est `master` ou `main` : stopper et demander le nom de la branche feature à créer. Si le contexte de la conversation permet de le déduire, proposer un nom. Puis exécuter `rtk git checkout -b <nom>` avant de poursuivre.
- Si le résultat est **vide** (HEAD détachée) : afficher `⚠ HEAD détachée — impossible de vérifier la branche. Continuer avec précaution.` et poursuivre normalement.
- Sinon : afficher `Branche \`<nom>\` — règles de travail actives.` et continuer normalement.

Une fois la branche confirmée, ne plus intervenir sur ce sujet sauf si une commande git tente de commiter sur `master` ou `main`.

## Phase END — Avant de terminer le travail

Déclencher cette phase quand tu t'apprêtes à invoquer `superpowers:finishing-a-development-branch`, à annoncer "implémentation terminée", ou à proposer un PR/merge.

### Règle 2 — Squash des commits

Vérifier le nombre de commits sur la branche :
```bash
BASE=$(rtk git rev-parse --verify master > /dev/null 2>&1 && echo master || echo main)
rtk git log $BASE..HEAD --oneline
```

Si **1 seul commit** : passer directement à `superpowers:finishing-a-development-branch`.

Si **2 commits ou plus** : afficher la liste et demander :

```
Travail terminé. Tu as N commits sur cette branche :

  abc1234 feat: ...
  def5678 fix: ...
  ...

→ Squasher en un seul commit ? [O]ui / [N]on
```

**Si "Oui" :**
1. Déterminer la branche de base et le commit de fusion :
```bash
BASE=$(rtk git rev-parse --verify master > /dev/null 2>&1 && echo master || echo main)
MERGE_BASE=$(rtk git merge-base HEAD $BASE) || { echo "⛔ Impossible de trouver la base commune avec la branche de base. Squash annulé."; exit 1; }
```
Si `MERGE_BASE` est vide ou que la commande échoue, s'arrêter et afficher le message d'erreur — ne pas poursuivre.

2. Exécuter le reset :
```bash
rtk git reset --soft $MERGE_BASE
```

3. Proposer un message de commit consolidé au format Conventional Commits (`type(scope): description`) basé sur les messages existants
4. Attendre la validation du message par l'utilisateur, puis commiter

**Si "Non" :** continuer directement vers `superpowers:finishing-a-development-branch`.
