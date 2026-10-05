#requires -Version 7
# PreToolUse(Bash) hook — refuse tout `git commit` lance depuis master/main.
# Applique mecaniquement la regle CLAUDE.md "Never commit directly to master or main".
# Contrat de hook : lit le payload JSON sur stdin, exit 2 = blocage (stderr renvoye a Claude).
#
# La branche testee est celle du depot vise par la commande : `git -C <chemin> commit`
# (battle legion en worktree) est evalue sur <chemin>, pas sur le repertoire de session.
# Seule une vraie sous-commande `commit` compte : un nom de branche ou de fichier qui
# contient le mot "commit" ne declenche pas le hook.

$ErrorActionPreference = 'Stop'

$raw = [Console]::In.ReadToEnd()
if ([string]::IsNullOrWhiteSpace($raw)) { exit 0 }

try { $payload = $raw | ConvertFrom-Json } catch { exit 0 }

$command = $payload.tool_input.command
if ([string]::IsNullOrWhiteSpace($command)) { exit 0 }

# git [options globales] commit : -C <chemin>, -c <cle=valeur>, --option[=valeur].
$gitCommit = '(?<![\w/.-])git((?:\s+(?:-C\s+(?:"[^"]*"|''[^'']*''|\S+)|-c\s+\S+|--[A-Za-z-]+(?:=\S+)?))*)\s+commit(?=\s|$)'
$matches = [regex]::Matches($command, $gitCommit)
if ($matches.Count -eq 0) { exit 0 }

foreach ($match in $matches) {
    $targetDir = $null

    foreach ($option in [regex]::Matches($match.Groups[1].Value, '-C\s+(?:"([^"]*)"|''([^'']*)''|(\S+))')) {
        $targetDir = ($option.Groups[1].Value, $option.Groups[2].Value, $option.Groups[3].Value |
            Where-Object { $_ } | Select-Object -First 1)
    }

    if ($targetDir) {
        if (-not (Test-Path -LiteralPath $targetDir -PathType Container)) { continue }

        $branch = (git -C $targetDir rev-parse --abbrev-ref HEAD 2>$null)
    }
    else {
        $branch = (git rev-parse --abbrev-ref HEAD 2>$null)
    }

    if ($LASTEXITCODE -ne 0) { continue }

    $branch = "$branch".Trim()

    if ($branch -eq 'master' -or $branch -eq 'main') {
        [Console]::Error.WriteLine(
            "Commit bloque : tu es sur '$branch'. Politique du depot (CLAUDE.md) : " +
            "cree d'abord une branche -> git checkout -b <type>/<short-description>")
        exit 2
    }
}

exit 0
