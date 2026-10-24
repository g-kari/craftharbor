$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$assetDir = Join-Path (Split-Path $PSScriptRoot -Parent) 'assets'
New-Item -ItemType Directory -Path $assetDir -Force | Out-Null
$sizes = @(16,24,32,48,64,128,256)
$frames = [Collections.Generic.List[byte[]]]::new()
foreach ($size in $sizes) {
    $bitmap = [Drawing.Bitmap]::new($size,$size,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [Drawing.Graphics]::FromImage($bitmap)
    $g.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([Drawing.Color]::Transparent)
    $g.ScaleTransform($size / 256.0,$size / 256.0)
    $bg = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(18,27,40))
    $top = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(178,246,225))
    $left = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(111,218,192))
    $right = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(54,143,134))
    $wave = [Drawing.Pen]::new([Drawing.Color]::FromArgb(111,218,192),12)
    $wave.StartCap = $wave.EndCap = [Drawing.Drawing2D.LineCap]::Round
    $g.FillEllipse($bg,0,0,256,256)
    $g.FillPolygon($top,[Drawing.PointF[]]@([Drawing.PointF]::new(128,40),[Drawing.PointF]::new(190,76),[Drawing.PointF]::new(128,112),[Drawing.PointF]::new(66,76)))
    $g.FillPolygon($left,[Drawing.PointF[]]@([Drawing.PointF]::new(66,76),[Drawing.PointF]::new(128,112),[Drawing.PointF]::new(128,180),[Drawing.PointF]::new(66,144)))
    $g.FillPolygon($right,[Drawing.PointF[]]@([Drawing.PointF]::new(128,112),[Drawing.PointF]::new(190,76),[Drawing.PointF]::new(190,144),[Drawing.PointF]::new(128,180)))
    $g.DrawBezier($wave,44,192,72,172,76,213,104,194)
    $g.DrawBezier($wave,104,194,132,174,136,215,164,194)
    $g.DrawBezier($wave,164,194,186,178,196,207,214,191)
    $stream = [IO.MemoryStream]::new()
    $bitmap.Save($stream,[Drawing.Imaging.ImageFormat]::Png)
    $frames.Add($stream.ToArray())
    if ($size -eq 256) { $bitmap.Save((Join-Path $assetDir 'craftharbor.png'),[Drawing.Imaging.ImageFormat]::Png) }
    $stream.Dispose(); $g.Dispose(); $bitmap.Dispose(); $bg.Dispose(); $top.Dispose(); $left.Dispose(); $right.Dispose(); $wave.Dispose()
}
$stream = [IO.File]::Create((Join-Path $assetDir 'craftharbor.ico'))
$writer = [IO.BinaryWriter]::new($stream)
$writer.Write([UInt16]0); $writer.Write([UInt16]1); $writer.Write([UInt16]$sizes.Length)
$offset = 6 + 16 * $sizes.Length
for ($i=0; $i -lt $sizes.Length; $i++) {
    $dimension = if ($sizes[$i] -eq 256) { 0 } else { $sizes[$i] }
    $writer.Write([byte]$dimension); $writer.Write([byte]$dimension); $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([UInt16]1); $writer.Write([UInt16]32); $writer.Write([UInt32]$frames[$i].Length); $writer.Write([UInt32]$offset)
    $offset += $frames[$i].Length
}
foreach ($frame in $frames) { $writer.Write($frame) }
$writer.Dispose()
