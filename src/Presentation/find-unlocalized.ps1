# Génère des warnings MSBuild pour text statique non localisé
$patterns = 'Text="[^"{]+','Content="[^"{]+','Header="[^"{]+','Label="[^"{]+','ToolTipService\.ToolTip="[^"{]+'
Get-ChildItem -Recurse -Filter *.xaml -File |
  Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' } |
  ForEach-Object {
    $file = $_.FullName
    Select-String -Path $file -Pattern $patterns |
      Where-Object { $_.Line -notmatch 'x:Uid=' } |
      Where-Object { $_.Line -notmatch '{\s*(x:)?(Bind|Binding|StaticResource|ThemeResource)' } |
      Where-Object { $_.Line -notmatch 'Text="\s*&\#' } |
      ForEach-Object {
        $snippet = ($_.Matches[0].Value) # ex: Text="Live"
        $code    = "LOC0001"
        $msg     = "Hard-coded $snippet (localiser via x:Uid)"
        # Sortie format MSBuild (colonne optionnelle : ici 1)
        Write-Output ("{0}({1},1): warning {2}: {3}" -f $file, $_.LineNumber, $code, $msg)
      }
  }
exit 0