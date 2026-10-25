$ErrorActionPreference='Stop'
$repoRoot=Split-Path $PSScriptRoot -Parent
$fixtureRoot=Join-Path $repoRoot ('work\schedule-check-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixtureRoot -Force | Out-Null
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$fixture=Join-Path $fixtureRoot 'ScheduleFixture.exe'
& $compiler /nologo /target:exe "/out:$fixture" (Join-Path $PSScriptRoot 'ScheduleFixture.cs')
if($LASTEXITCODE -ne 0){throw 'Schedule fixture compilation failed.'}
$process=Start-Process -FilePath (Join-Path $repoRoot 'dist\CraftHarbor.exe') -ArgumentList @('--schedule-check','--workspace',('"'+$fixtureRoot+'"'),'--fixture',('"'+$fixture+'"')) -WindowStyle Hidden -PassThru
if(-not $process.WaitForExit(240000)){
    Stop-Process -Id $process.Id
    throw 'Headless schedule checks timed out.'
}
Get-Content (Join-Path $fixtureRoot 'schedule-verification.txt')
if($process.ExitCode -ne 0){throw 'Headless schedule checks failed.'}
