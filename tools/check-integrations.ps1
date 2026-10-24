$ErrorActionPreference='Stop'
$repoRoot=Split-Path $PSScriptRoot -Parent
$scratch=Join-Path $repoRoot ('work\integration-'+[guid]::NewGuid().ToString('N'))
$server=Join-Path $scratch 'server'
New-Item -ItemType Directory -Path $server -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $server 'server.properties'),"server-port=25569`nonline-mode=true`n")
[IO.File]::WriteAllBytes((Join-Path $server 'server.jar'),[byte[]]@())
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$fixture=Join-Path $scratch 'HostFixture.exe'
& $compiler /nologo /target:exe "/out:$fixture" (Join-Path $PSScriptRoot 'HostFixture.cs')
if($LASTEXITCODE -ne 0){throw 'Host fixture build failed.'}
$exe=Join-Path $repoRoot 'dist\CraftHarbor.exe'
$process=Start-Process -FilePath $exe -ArgumentList @('--host-check',('"'+$scratch+'"'),('"'+$fixture+'"')) -Wait -PassThru
Get-Content (Join-Path $scratch 'host-verification.txt')
if($process.ExitCode -ne 0){throw 'Persistent host integration failed.'}
$pack=Join-Path $scratch 'pack-check'
$process=Start-Process -FilePath $exe -ArgumentList @('--pack-check',('"'+$pack+'"')) -Wait -PassThru
Get-Content (Join-Path $pack 'pack-verification.txt')
if($process.ExitCode -ne 0){throw 'Pack integration failed.'}
