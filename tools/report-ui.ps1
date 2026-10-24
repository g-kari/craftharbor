$ErrorActionPreference='Stop'
$repoRoot=Split-Path $PSScriptRoot -Parent
$fixture=Join-Path $repoRoot 'work/ui-fixture-modpack'
if(!(Test-Path (Join-Path $fixture 'ui-harbor-1360-0.png'))){$fixture=Join-Path $repoRoot 'work/ui-fixture-vanilla'}
Get-ChildItem -LiteralPath (Join-Path $repoRoot 'work') -Filter 'ui-fixture-*' -Directory | ForEach-Object { $progress=Join-Path $_.FullName 'ui-check-progress.txt'; if(Test-Path -LiteralPath $progress){Get-Content -LiteralPath $progress} }
# Small, bounded synthetic images in cloud logs permit remote review without using a local PC.
Add-Type -AssemblyName System.Drawing
# Keep originals in the artifact; encode secondary log previews as JPEG to fit
# the connector's bounded log response. Primary native evidence remains PNG.
$primary=@('ui-harbor-independent.png','ui-harbor-native-1024.png','ui-harbor-native-contrast-history.png')
$files=@('ui-harbor-independent.png','ui-harbor-native-1024.png','ui-harbor-native-contrast-history.png','ui-harbor-1360-0.png','ui-harbor-1024-0.png','ui-harbor-contrast-0.png','ui-harbor-1024-1.png','ui-harbor-1024-2.png','ui-harbor-1024-3.png','ui-harbor-1024-4.png','ui-harbor-1024-5.png','ui-harbor-1024-6.png','ui-harbor-1024-7.png','ui-harbor-1024-8.png','ui-hosting.png','ui-import.png','ui-servers.png','ui-harbor-history.png','ui-harbor-contrast-history.png','ui-servers-contrast.png','ui-hosting-contrast.png','ui-import-contrast.png')
foreach($name in $files){
 $path=Join-Path $fixture $name
 if(!(Test-Path -LiteralPath $path)){continue}
 $displayName=$name
 if($primary -contains $name){$bytes=[IO.File]::ReadAllBytes($path)}
 else{
  $source=[Drawing.Image]::FromFile($path);$memory=[IO.MemoryStream]::new()
  $parameters=[Drawing.Imaging.EncoderParameters]::new(1)
  try{
   $parameters.Param[0]=[Drawing.Imaging.EncoderParameter]::new([Drawing.Imaging.Encoder]::Quality,[long]85)
   $codec=[Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object MimeType -eq 'image/jpeg'
   $source.Save($memory,$codec,$parameters);$bytes=$memory.ToArray();$displayName=[IO.Path]::ChangeExtension($name,'.jpg')
  }finally{$parameters.Dispose();$memory.Dispose();$source.Dispose()}
 }
 $base64=[Convert]::ToBase64String($bytes)
 Write-Output ('IMAGE_BEGIN '+$displayName)
 for($i=0;$i -lt $base64.Length;$i+=2000){Write-Output ('IMAGE_DATA '+$base64.Substring($i,[Math]::Min(2000,$base64.Length-$i)))}
 Write-Output ('IMAGE_END '+$displayName)
}
