$ErrorActionPreference='Stop'
$repoRoot=Split-Path $PSScriptRoot -Parent
$scratch=Join-Path $repoRoot ('work\settings-check-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $scratch -Force | Out-Null
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$fixture=Join-Path $scratch 'HostFixture.exe'
& $compiler /nologo /target:exe "/out:$fixture" (Join-Path $PSScriptRoot 'HostFixture.cs')
if($LASTEXITCODE -ne 0){throw 'Settings fixture compilation failed.'}
$exe=Join-Path $repoRoot 'dist\CraftHarbor.exe'
$process=Start-Process -FilePath $exe -ArgumentList @('--settings-check',('"'+$scratch+'"'),('"'+$fixture+'"')) -WindowStyle Hidden -PassThru
if(-not $process.WaitForExit(120000)){Stop-Process -Id $process.Id;throw 'Settings checks timed out.'}
Get-Content (Join-Path $scratch 'settings-verification.txt')
if($process.ExitCode -ne 0){throw 'Settings and whitelist checks failed.'}
