#requires -Version 7
# Stop hook — formate, une fois par tour, les .cs modifies/ajoutes dans l'arbre de travail.
# Batch par projet -> MSBuild charge au plus une fois par projet touche, au lieu d'une fois
# par edition. Best-effort : ne bloque jamais (toujours exit 0).
# Le formatage au commit reste assure independamment par le hook pre-commit Husky.

$ErrorActionPreference = 'SilentlyContinue'

$raw = [Console]::In.ReadToEnd()
if (-not [string]::IsNullOrWhiteSpace($raw)) {
    try { $payload = $raw | ConvertFrom-Json } catch { $payload = $null }
    if ($payload.stop_hook_active) { exit 0 }
}

$root = (git rev-parse --show-toplevel 2>$null)
if ($LASTEXITCODE -ne 0) { exit 0 }
$root = $root.Trim()

$tracked   = git diff --name-only --diff-filter=ACMR HEAD -- '*.cs' 2>$null
$untracked = git ls-files --others --exclude-standard -- '*.cs' 2>$null
$changed = @($tracked) + @($untracked) | Where-Object { $_ } | Select-Object -Unique
if (-not $changed) { exit 0 }

# Resoudre chaque fichier vers son .csproj le plus proche, puis regrouper par projet.
$byProject = @{}
foreach ($rel in $changed) {
    $full = Join-Path $root $rel
    if (-not (Test-Path -LiteralPath $full)) { continue }

    $project = $null
    $dir = Split-Path -Parent $full
    while ($dir -and (Test-Path -LiteralPath $dir)) {
        $candidate = Get-ChildItem -LiteralPath $dir -Filter *.csproj -File | Select-Object -First 1
        if ($candidate) { $project = $candidate.FullName; break }
        $parent = Split-Path -Parent $dir
        if ($parent -eq $dir) { break }
        $dir = $parent
    }
    if (-not $project) { continue }

    if (-not $byProject.ContainsKey($project)) {
        $byProject[$project] = [System.Collections.Generic.List[string]]::new()
    }
    $byProject[$project].Add($full)
}

foreach ($project in $byProject.Keys) {
    $files = $byProject[$project]
    try { dotnet format whitespace $project --include @($files) 2>$null | Out-Null } catch { }
}

exit 0
