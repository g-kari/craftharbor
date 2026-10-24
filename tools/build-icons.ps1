param([Parameter(Mandatory=$true)][string]$OutputPath)
$ErrorActionPreference='Stop'
$repoRoot=Split-Path $PSScriptRoot -Parent
Add-Type -AssemblyName System.Drawing
$atlas=[Drawing.Image]::FromFile((Join-Path $repoRoot 'assets/harbor-icons.png'))
$source=[Drawing.Rectangle]::new(1296,770,221,221)
$sizes=@(16,24,32,48,64,128,256)
$payloads=[Collections.Generic.List[byte[]]]::new()
try{
 foreach($size in $sizes){
  $bitmap=[Drawing.Bitmap]::new($size,$size,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $graphics=[Drawing.Graphics]::FromImage($bitmap)
  $memory=[IO.MemoryStream]::new()
  try{
   $graphics.Clear([Drawing.Color]::Transparent)
   $graphics.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
   $graphics.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::Half
   $graphics.DrawImage($atlas,[Drawing.Rectangle]::new(0,0,$size,$size),$source,[Drawing.GraphicsUnit]::Pixel)
   $bitmap.Save($memory,[Drawing.Imaging.ImageFormat]::Png)
   $payloads.Add($memory.ToArray())
  }finally{$memory.Dispose();$graphics.Dispose();$bitmap.Dispose()}
 }
 New-Item -ItemType Directory -Path (Split-Path $OutputPath -Parent) -Force | Out-Null
 $stream=[IO.File]::Create($OutputPath);$writer=[IO.BinaryWriter]::new($stream)
 try{
  $writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]$sizes.Length)
  $offset=6+16*$sizes.Length
  for($i=0;$i -lt $sizes.Length;$i++){
   $dimension=if($sizes[$i] -eq 256){0}else{$sizes[$i]}
   $writer.Write([byte]$dimension);$writer.Write([byte]$dimension);$writer.Write([byte]0);$writer.Write([byte]0)
   $writer.Write([uint16]1);$writer.Write([uint16]32);$writer.Write([uint32]$payloads[$i].Length);$writer.Write([uint32]$offset)
   $offset+=$payloads[$i].Length
  }
  foreach($payload in $payloads){$writer.Write([byte[]]$payload)}
 }finally{$writer.Dispose();$stream.Dispose()}
 $icon=[Drawing.Icon]::new($OutputPath)
 try{if($icon.Width -lt 16 -or $icon.Height -lt 16){throw 'Invalid branded ICO dimensions.'}}finally{$icon.Dispose()}
 Write-Output 'Built seven-size transparent lighthouse icon for app, setup and uninstall.'
}finally{$atlas.Dispose()}
