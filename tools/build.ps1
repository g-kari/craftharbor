$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$dist = Join-Path $repoRoot 'dist'
New-Item -ItemType Directory -Path $dist,(Join-Path $dist 'icons') -Force | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw '.NET Framework 4.x C# compiler is required.' }
$sourcePaths = Get-ChildItem -LiteralPath (Join-Path $repoRoot 'src') -Filter '*.cs' | Sort-Object Name | Select-Object -ExpandProperty FullName
$compilerArgs = @('/nologo','/target:winexe','/platform:x64',('/win32icon:'+(Join-Path $repoRoot 'assets\astral.ico')),('/out:'+(Join-Path $dist 'AstralManager.exe')),
 '/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll','/reference:System.IO.Compression.dll','/reference:System.IO.Compression.FileSystem.dll',
 '/reference:System.Windows.Forms.DataVisualization.dll','/reference:System.Web.Extensions.dll','/reference:System.Security.dll') + $sourcePaths
& $compiler @compilerArgs
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
Copy-Item -LiteralPath (Join-Path $repoRoot 'assets\astral.ico') -Destination (Join-Path $dist 'icons\astral.ico') -Force
foreach ($name in @('README.md','LICENSE','PRIVACY.md','CODE_SIGNING_POLICY.md')) {
    Copy-Item -LiteralPath (Join-Path $repoRoot $name) -Destination (Join-Path $dist $name) -Force
}
Write-Output "Built $dist\AstralManager.exe. This build is unsigned."
