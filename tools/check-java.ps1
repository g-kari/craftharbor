param([string]$JavaPath)
$ErrorActionPreference='Stop'
$repoRoot=Split-Path $PSScriptRoot -Parent
if(-not $JavaPath){
    if($env:JAVA_HOME_17_X64){$JavaPath=Join-Path $env:JAVA_HOME_17_X64 'bin\java.exe'}
    else{$JavaPath=(Get-Command java.exe -ErrorAction Stop).Source}
}
if(-not (Test-Path -LiteralPath $JavaPath)){throw 'The Java executable was not found.'}
$process=Start-Process -FilePath (Join-Path $repoRoot 'dist\CraftHarbor.exe') -ArgumentList @('--java-check',('"'+$JavaPath+'"')) -WindowStyle Hidden -Wait -PassThru
Get-Content (Join-Path $repoRoot 'dist\java-verification.txt')
if($process.ExitCode -ne 0){throw 'Java runtime option validation failed.'}
