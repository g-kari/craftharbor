param([string]$CompilerPath='')
$ErrorActionPreference='Stop'
$repoRoot=Split-Path $PSScriptRoot -Parent
$dist=Join-Path $repoRoot 'dist'
$version=(Get-Content -LiteralPath (Join-Path $repoRoot 'release.json') -Raw | ConvertFrom-Json).version
if($version -notmatch '^\d+\.\d+\.\d+(-[a-z0-9.-]+)?$'){throw 'Invalid release version.'}
$setupName="CraftHarbor-Setup-$version.exe"
$uninstallName="CraftHarbor-Uninstall-$version.exe"
foreach($file in @('CraftHarbor.exe','CraftHarbor.Host.exe')){if(-not(Test-Path -LiteralPath (Join-Path $dist $file))){throw "Build $file before packaging."}}
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:winexe /platform:x64 "/win32icon:$(Join-Path $dist 'icons\craftharbor.ico')" "/out:$(Join-Path $dist $uninstallName)" /reference:System.Windows.Forms.dll (Join-Path $repoRoot 'installer\UninstallLauncher.cs')
if($LASTEXITCODE -ne 0){throw 'Uninstall launcher compilation failed.'}
if(-not $CompilerPath){foreach($candidate in @("${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe","$env:ProgramFiles\Inno Setup 7\ISCC.exe")){if(Test-Path -LiteralPath $candidate){$CompilerPath=$candidate;break}}}
if(-not(Test-Path -LiteralPath $CompilerPath)){throw 'Inno Setup 6.7+ is required. Pass -CompilerPath to ISCC.exe.'}
& $CompilerPath (Join-Path $repoRoot 'installer\setup.iss')
if($LASTEXITCODE -ne 0){throw 'Installer build failed.'}
foreach($name in @($setupName,$uninstallName)){$file=Join-Path $dist $name;$hash=(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant();[IO.File]::WriteAllText(($file+'.sha256'),$hash+'  '+$name+[Environment]::NewLine,[Text.UTF8Encoding]::new($false))}
