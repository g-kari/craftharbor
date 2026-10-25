param([ValidateSet('all','primary','pages','dialogs','accessibility')][string]$Group='all')
$ErrorActionPreference='Stop'
$repoRoot=Split-Path $PSScriptRoot -Parent
$fixture=Join-Path $repoRoot 'work/ui-fixture-modpack'
if(!(Test-Path (Join-Path $fixture 'ui-harbor-1360-0.png'))){$fixture=Join-Path $repoRoot 'work/ui-fixture-vanilla'}
if($Group -eq 'primary' -or $Group -eq 'all'){
 Get-ChildItem -LiteralPath (Join-Path $repoRoot 'work') -Filter 'ui-fixture-*' -Directory | ForEach-Object {
  $progress=Join-Path $_.FullName 'ui-check-progress.txt'
  if(Test-Path -LiteralPath $progress){Get-Content -LiteralPath $progress}
 }
}
# Inspect logs after the job completes; active-job downloads can be incomplete.
# All images are synthetic native captures, never a user's local desktop.
$files=switch($Group){
 'all' { @('ui-distance-settings.png','ui-java-settings.png','ui-harbor-independent.png','ui-harbor-native-1024.png','ui-harbor-native-contrast-history.png','ui-harbor-1360-0.png','ui-harbor-history.png','ui-harbor-contrast-history.png','ui-hosting.png','ui-import.png','ui-servers.png','ui-hosting-contrast.png','ui-import-contrast.png','ui-servers-contrast.png');0..8 | ForEach-Object {'ui-harbor-1024-'+$_+'.png';'ui-harbor-contrast-'+$_+'.png'} }
 'primary' {@('ui-harbor-independent.png','ui-harbor-native-1024.png','ui-harbor-native-contrast-history.png','ui-harbor-1360-0.png')}
 'pages' {0..8 | ForEach-Object {'ui-harbor-1024-'+$_+'.png'}}
 'dialogs' {@('ui-hosting.png','ui-import.png','ui-servers.png','ui-hosting-contrast.png','ui-import-contrast.png','ui-servers-contrast.png')}
 'accessibility' {0..8 | ForEach-Object {'ui-harbor-contrast-'+$_+'.png'}}
}
foreach($name in $files){
 $path=Join-Path $fixture $name
 if(!(Test-Path -LiteralPath $path)){Write-Output ('Capture unavailable: '+$name);continue}
 $base64=[Convert]::ToBase64String([IO.File]::ReadAllBytes($path))
 Write-Output ('IMAGE_BEGIN '+$name)
 for($i=0;$i -lt $base64.Length;$i+=2000){Write-Output ('IMAGE_DATA '+$base64.Substring($i,[Math]::Min(2000,$base64.Length-$i)))}
 Write-Output ('IMAGE_END '+$name)
}

if($Group -eq 'all'){
 Get-ChildItem -LiteralPath (Join-Path $repoRoot 'work') -Filter 'settings-check-*' -Directory | ForEach-Object {
  $path=Join-Path $_.FullName 'ui-pending-settings.png'
  if(Test-Path -LiteralPath $path){
   $base64=[Convert]::ToBase64String([IO.File]::ReadAllBytes($path))
   Write-Output 'IMAGE_BEGIN ui-pending-settings.png'
   for($i=0;$i -lt $base64.Length;$i+=2000){Write-Output ('IMAGE_DATA '+$base64.Substring($i,[Math]::Min(2000,$base64.Length-$i)))}
   Write-Output 'IMAGE_END ui-pending-settings.png'
  }
 }
}
