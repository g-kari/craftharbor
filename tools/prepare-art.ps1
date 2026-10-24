$ErrorActionPreference='Stop'
# Reuse the promotional site's original MIT artwork at an immutable revision.
$repoRoot=Split-Path $PSScriptRoot -Parent
$assetDir=Join-Path $repoRoot 'assets'
$target=Join-Path $assetDir 'harbor-ui.png'
if(Test-Path -LiteralPath $target){return}
$workDir=Join-Path $repoRoot 'work/art'
New-Item -ItemType Directory -Path $workDir -Force | Out-Null
$original=Join-Path $workDir 'harbor-original.png'
Invoke-WebRequest -UseBasicParsing -Uri 'https://raw.githubusercontent.com/g-kari/craftharbor-site/e587c600ec17971682b1a0569aefc178c0a68d98/public/assets/harbor.png' -OutFile $original
$bytes=[IO.File]::ReadAllBytes($original)
$prefix=[Text.Encoding]::ASCII.GetBytes('blob '+$bytes.Length+[char]0)
$sha=[Security.Cryptography.SHA1]::Create()
try{$gitHash=[BitConverter]::ToString($sha.ComputeHash($prefix+$bytes)).Replace('-','').ToLowerInvariant()}finally{$sha.Dispose()}
if($gitHash -ne 'd9363f93da1a6c77e5aeffb44b3be3475959abf2'){throw 'Promotional artwork hash mismatch'}
Add-Type -AssemblyName System.Drawing
$source=[Drawing.Image]::FromFile($original)
try{
 $width=480
 $height=[int][Math]::Round($source.Height*$width/$source.Width)
 $bitmap=New-Object Drawing.Bitmap($width,$height)
 try{$graphics=[Drawing.Graphics]::FromImage($bitmap)
  try{$graphics.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::NearestNeighbor;$graphics.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::Half;$graphics.DrawImage($source,0,0,$width,$height)}finally{$graphics.Dispose()}
  $bitmap.Save($target,[Drawing.Imaging.ImageFormat]::Png)
 }finally{$bitmap.Dispose()}
}finally{$source.Dispose()}
