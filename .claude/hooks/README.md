# Hooks Claude Code — Rok

Hooks declenches par le harness Claude Code (pas par Claude lui-meme), configures dans
`.claude/settings.json`. Les chemins sont relatifs a la racine du depot ; si un hook ne se
declenche pas, c'est probablement un probleme de repertoire courant — passe a un chemin absolu.

## `block-commit-on-default-branch.ps1` — PreToolUse(Bash)

Refuse tout `git commit` lance depuis `master` ou `main`, en application de la regle
`CLAUDE.md` « Never commit directly to master or main ». Le hook sort en code 2, ce qui bloque
l'appel et renvoie le message a Claude. Aucun cout perceptible.

## `format-changed-files.ps1` — Stop

Quand Claude finit de repondre, formate **une seule fois par tour** les `.cs` modifies ou
ajoutes dans l'arbre de travail (`git diff` + fichiers non suivis), en batch par projet via
`dotnet format whitespace` (analyzers sautes -> plus rapide). **Best-effort** : ne bloque
jamais (toujours exit 0), et ne fait rien si aucun `.cs` n'a change.

Pourquoi un hook `Stop` plutot que `PostToolUse(Edit)` : MSBuild n'est charge qu'au plus une
fois par projet touche, a la fin du tour, au lieu d'une fois par edition.

**Redondance assumee avec le commit** : le hook pre-commit Husky lance deja `dotnet format` sur
les `.cs` staged avant chaque commit. Ce hook `Stop` ne sert qu'au confort mid-session (code
formate avant meme de committer). Si la latence en fin de tour gene, **retire le bloc `Stop` de
`.claude/settings.json`** sans rien perdre cote commit. La Presentation (`Rok.csproj`) exige
`/p:Platform=x64` non fourni ici -> peut etre un no-op sur ces fichiers (sans gravite).
