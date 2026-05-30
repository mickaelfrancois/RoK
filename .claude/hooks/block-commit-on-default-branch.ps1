#requires -Version 7
# PreToolUse(Bash) hook — refuse tout `git commit` lance depuis master/main.
# Applique mecaniquement la regle CLAUDE.md "Never commit directly to master or main".
# Contrat de hook : lit le payload JSON sur stdin, exit 2 = blocage (stderr renvoye a Claude).

$ErrorActionPreference = 'Stop'

$raw = [Console]::In.ReadToEnd()
if ([string]::IsNullOrWhiteSpace($raw)) { exit 0 }

try { $payload = $raw | ConvertFrom-Json } catch { exit 0 }

$command = $payload.tool_input.command
if ([string]::IsNullOrWhiteSpace($command)) { exit 0 }

# On ne s'interesse qu'aux commits.
if ($command -notmatch '(^|[\s;&|])git\b[^;&|]*\bcommit\b') { exit 0 }

$branch = (git rev-parse --abbrev-ref HEAD 2>$null)
if ($LASTEXITCODE -ne 0) { exit 0 }
$branch = $branch.Trim()

if ($branch -eq 'master' -or $branch -eq 'main') {
    [Console]::Error.WriteLine(
        "Commit bloque : tu es sur '$branch'. Politique du depot (CLAUDE.md) : " +
        "cree d'abord une branche -> git checkout -b <type>/<short-description>")
    exit 2
}

exit 0
