$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$dist = Join-Path $repoRoot 'dist'
New-Item -ItemType Directory -Path $dist,(Join-Path $dist 'icons') -Force | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw '.NET Framework 4.x C# compiler is required.' }
$sourcePaths = Get-ChildItem -LiteralPath (Join-Path $repoRoot 'src') -Filter '*.cs' | Sort-Object Name | Select-Object -ExpandProperty FullName
$compilerArgs = @('/nologo','/target:winexe','/platform:x64',('/win32icon:'+(Join-Path $repoRoot 'assets\craftharbor.ico')),('/out:'+(Join-Path $dist 'CraftHarbor.exe')),
 '/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll','/reference:System.IO.Compression.dll','/reference:System.IO.Compression.FileSystem.dll',
 '/reference:System.Windows.Forms.DataVisualization.dll','/reference:System.Web.Extensions.dll','/reference:System.Security.dll') + $sourcePaths
& $compiler @compilerArgs
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
$hostArgs=@('/define:HOST_BINARY',('/out:'+(Join-Path $dist 'CraftHarbor.Host.exe')))+@($compilerArgs | Where-Object { $_ -notlike '/out:*' })
& $compiler @hostArgs
if($LASTEXITCODE -ne 0){throw 'Persistent host compilation failed.'}
Copy-Item -LiteralPath (Join-Path $repoRoot 'assets\craftharbor.ico') -Destination (Join-Path $dist 'icons\craftharbor.ico') -Force
New-Item -ItemType Directory -Path (Join-Path $dist 'recipes') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $repoRoot 'recipes\server-packs.json') -Destination (Join-Path $dist 'recipes\server-packs.json') -Force
foreach ($name in @('README.md','LICENSE','PRIVACY.md','CODE_SIGNING_POLICY.md')) {
    Copy-Item -LiteralPath (Join-Path $repoRoot $name) -Destination (Join-Path $dist $name) -Force
}
New-Item -ItemType Directory -Path (Join-Path $dist 'docs') -Force | Out-Null
foreach($name in @('PERFORMANCE.md','PROFILES.md','DESIGN.md','RELEASE_NOTES.md','SIGNPATH.md')){
    Copy-Item -LiteralPath (Join-Path $repoRoot ('docs\'+$name)) -Destination (Join-Path $dist ('docs\'+$name)) -Force
}
Write-Output "Built $dist\CraftHarbor.exe. This build is unsigned."
