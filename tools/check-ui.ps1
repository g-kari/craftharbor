$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$fixture = Join-Path $repoRoot 'work\ui-fixture'
$server = Join-Path $fixture 'server'
New-Item -ItemType Directory -Path (Join-Path $server 'mods'),(Join-Path $server 'config') -Force | Out-Null
[IO.File]::WriteAllBytes((Join-Path $server 'server.jar'),[byte[]]@())
$properties = @'
motd=UI validation fixture
difficulty=normal
gamemode=survival
max-players=10
view-distance=8
simulation-distance=6
pvp=true
allow-flight=false
'@
[IO.File]::WriteAllText((Join-Path $server 'server.properties'),$properties)
[IO.File]::WriteAllText((Join-Path $server 'config\ftbbackups2.json'),'{"backup_cron":"0 0 */2 * * ?","max_backups":7}')
foreach($name in @('whitelist.json','ops.json','banned-players.json')) { [IO.File]::WriteAllText((Join-Path $server $name),'[]') }
$exe = Join-Path $repoRoot 'dist\AstralManager.exe'
$check = Start-Process -FilePath $exe -ArgumentList @('--experience-check','--workspace',('"'+$fixture+'"')) -Wait -PassThru
$report = Join-Path $fixture 'experience-verification.txt'
if(Test-Path -LiteralPath $report) { Get-Content -LiteralPath $report }
if($check.ExitCode -ne 0) { throw 'UI fixture checks failed.' }
