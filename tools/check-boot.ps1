$ErrorActionPreference='Stop'
$repoRoot=Split-Path $PSScriptRoot -Parent
$fixtureRoot=Join-Path $repoRoot ('work\boot-check-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixtureRoot -Force | Out-Null
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$fixture=Join-Path $fixtureRoot 'BootFixture.exe'
& $compiler /nologo /target:exe "/out:$fixture" (Join-Path $PSScriptRoot 'BootFixture.cs')
if($LASTEXITCODE -ne 0){throw 'Boot fixture compilation failed.'}
$exe=Join-Path $repoRoot 'dist\CraftHarbor.exe'
$process=Start-Process -FilePath $exe -ArgumentList @('--boot-check','--workspace',('"'+$fixtureRoot+'"'),'--fixture',('"'+$fixture+'"')) -WindowStyle Hidden -PassThru
if(-not $process.WaitForExit(120000)){
    Stop-Process -Id $process.Id
    throw 'Boot recovery checks timed out.'
}
Get-Content (Join-Path $fixtureRoot 'boot-verification.txt')
if($process.ExitCode -ne 0){throw 'Boot recovery checks failed.'}
