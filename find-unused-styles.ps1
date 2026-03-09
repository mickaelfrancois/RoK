param(
    [string]$Root = ".",
    [string[]]$StyleFiles = @(
        "Presentation\Styles\Tokens.xaml",
        "Presentation\Styles\ButtonStyles.xaml",
        "Presentation\Styles\ControlsStyles.xaml",
        "Presentation\Styles\AlbumTemplates.xaml",
        "Presentation\Styles\RatingControlStyles.xaml"
    )
)

$allXamlAndCs = Get-ChildItem -Path $Root -Recurse -Include "*.xaml","*.cs" |
    Where-Object { $_.FullName -notmatch "\\obj\\" -and $_.FullName -notmatch "\\bin\\" }

$styleKeys = [System.Collections.Generic.List[PSCustomObject]]::new()

foreach ($styleFile in $StyleFiles) {
    $fullPath = Join-Path $Root $styleFile
    if (-not (Test-Path $fullPath)) { continue }

    $content = Get-Content $fullPath -Raw
    $matches  = [regex]::Matches($content, 'x:Key="([^"]+)"')

    foreach ($match in $matches) {
        $styleKeys.Add([PSCustomObject]@{
            Key  = $match.Groups[1].Value
            File = $styleFile
        })
    }
}

Write-Host "`n=== Duplicate keys ===" -ForegroundColor Yellow
$styleKeys | Group-Object Key | Where-Object { $_.Count -gt 1 } | ForEach-Object {
    Write-Host "  DUPLICATE: $($_.Name)" -ForegroundColor Red
}

Write-Host "`n=== Unused keys ===" -ForegroundColor Yellow
foreach ($entry in $styleKeys) {
    $key = $entry.Key

    $isUsed = $allXamlAndCs | Where-Object { $_.FullName -notlike "*$($entry.File)*" } | ForEach-Object {
        $c = Get-Content $_.FullName -Raw -ErrorAction SilentlyContinue
        $c -match [regex]::Escape($key)
    } | Where-Object { $_ -eq $true }

    if (-not $isUsed) {
        Write-Host "  UNUSED [$($entry.File)]: $key" -ForegroundColor DarkYellow
    }
}

Write-Host "`nDone." -ForegroundColor Green