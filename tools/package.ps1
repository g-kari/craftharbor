$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$dist = Join-Path $repoRoot 'dist'
$files = @('CraftHarbor.exe','CraftHarbor.Host.exe','icons\craftharbor.ico','recipes\server-packs.json','README.md','LICENSE','PRIVACY.md','CODE_SIGNING_POLICY.md','docs\PERFORMANCE.md','docs\PROFILES.md','docs\DESIGN.md','docs\RELEASE_NOTES.md','docs\SIGNPATH.md')
foreach ($file in $files) { if (-not (Test-Path -LiteralPath (Join-Path $dist $file))) { throw "Missing package file: $file" } }
$archivePath = Join-Path $dist 'CraftHarbor-0.4.0-preview-windows-x64.zip'
$stage = Join-Path $repoRoot ('work\package-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path (Join-Path $stage 'icons'),(Join-Path $stage 'recipes'),(Join-Path $stage 'docs') -Force | Out-Null
foreach ($file in $files) { Copy-Item -LiteralPath (Join-Path $dist $file) -Destination (Join-Path $stage $file) }
Compress-Archive -LiteralPath (Join-Path $stage 'CraftHarbor.exe'),(Join-Path $stage 'CraftHarbor.Host.exe'),(Join-Path $stage 'icons'),(Join-Path $stage 'recipes'),(Join-Path $stage 'docs'),(Join-Path $stage 'README.md'),(Join-Path $stage 'LICENSE'),(Join-Path $stage 'PRIVACY.md'),(Join-Path $stage 'CODE_SIGNING_POLICY.md') -DestinationPath $archivePath -Force
$hash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText(($archivePath+'.sha256'),$hash+'  '+[IO.Path]::GetFileName($archivePath)+[Environment]::NewLine,[Text.UTF8Encoding]::new($false))
Write-Output $archivePath
