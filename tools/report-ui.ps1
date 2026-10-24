$ErrorActionPreference='Stop'
$repoRoot=Split-Path $PSScriptRoot -Parent
$fixture=Join-Path $repoRoot 'work/ui-fixture-modpack'
# Small, bounded synthetic images in cloud logs permit remote review without using a local PC.
$files=@('ui-harbor-1360-0.png','ui-harbor-1024-0.png','ui-harbor-independent.png','ui-harbor-contrast-0.png','ui-harbor-1024-1.png','ui-harbor-1024-2.png','ui-harbor-1024-3.png','ui-harbor-1024-4.png','ui-harbor-1024-5.png','ui-harbor-1024-6.png','ui-harbor-1024-7.png','ui-harbor-1024-8.png','ui-hosting.png','ui-import.png','ui-servers.png','ui-harbor-history.png','ui-harbor-contrast-history.png','ui-servers-contrast.png','ui-hosting-contrast.png','ui-import-contrast.png')
foreach($name in $files){
 $path=Join-Path $fixture $name
 if(!(Test-Path -LiteralPath $path)){continue}
 $base64=[Convert]::ToBase64String([IO.File]::ReadAllBytes($path))
 Write-Output ('IMAGE_BEGIN '+$name)
 for($i=0;$i -lt $base64.Length;$i+=2000){Write-Output ('IMAGE_DATA '+$base64.Substring($i,[Math]::Min(2000,$base64.Length-$i)))}
 Write-Output ('IMAGE_END '+$name)
}
