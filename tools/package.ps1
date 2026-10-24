$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$dist = Join-Path $repoRoot 'dist'
$files = @('AstralManager.exe','icons\astral.ico','README.md','LICENSE','PRIVACY.md','CODE_SIGNING_POLICY.md')
foreach ($file in $files) { if (-not (Test-Path -LiteralPath (Join-Path $dist $file))) { throw "Missing package file: $file" } }
$archivePath = Join-Path $dist 'AstralServerManager-0.2.0-preview-windows-x64.zip'
$stage = Join-Path $repoRoot ('work\package-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path (Join-Path $stage 'icons') -Force | Out-Null
foreach ($file in $files) { Copy-Item -LiteralPath (Join-Path $dist $file) -Destination (Join-Path $stage $file) }
Compress-Archive -LiteralPath (Join-Path $stage 'AstralManager.exe'),(Join-Path $stage 'icons'),(Join-Path $stage 'README.md'),(Join-Path $stage 'LICENSE'),(Join-Path $stage 'PRIVACY.md'),(Join-Path $stage 'CODE_SIGNING_POLICY.md') -DestinationPath $archivePath -Force
$hash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText(($archivePath+'.sha256'),$hash+'  '+[IO.Path]::GetFileName($archivePath)+[Environment]::NewLine,[Text.UTF8Encoding]::new($false))
Write-Output $archivePath
