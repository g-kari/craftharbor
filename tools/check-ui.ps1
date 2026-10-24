param([ValidateSet('vanilla','modpack','argfile')][string]$Scenario='vanilla')
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$fixture = Join-Path $repoRoot ('work\ui-fixture-'+$Scenario)
$server = Join-Path $fixture 'server'
New-Item -ItemType Directory -Path (Join-Path $server 'config') -Force | Out-Null
if($Scenario -ne 'argfile') { [IO.File]::WriteAllBytes((Join-Path $server 'custom-server.jar'),[byte[]]@()) }
$properties = @'
motd=UI validation fixture
difficulty=normal
gamemode=survival
max-players=10
view-distance=8
simulation-distance=6
pvp=true
allow-flight=false
server-port=25567
'@
[IO.File]::WriteAllText((Join-Path $server 'server.properties'),$properties)
if($Scenario -eq 'modpack') {
    New-Item -ItemType Directory -Path (Join-Path $server 'mods') -Force | Out-Null
    [IO.File]::WriteAllBytes((Join-Path $server 'mods\spark-fixture.jar'),[byte[]]@())
    [IO.File]::WriteAllText((Join-Path $server 'config\ftbbackups2.json'),'{"backup_cron":"0 0 */2 * * ?","max_backups":7}')
    [IO.File]::WriteAllText((Join-Path $fixture 'manager-profile.json'),'{"Name":"Sample Modpack Server","MinecraftVersion":"1.20.1","PackName":"Example Automation Pack","PackVersion":"1.0","ClientMode":"modpack","ClientUrl":"https://example.invalid/modpack","LaunchMode":"jar","JarFile":"custom-server.jar","SparkMode":"auto"}')
}
if($Scenario -eq 'argfile') {
    New-Item -ItemType Directory -Path (Join-Path $server 'libraries\example') -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $server 'libraries\example\win_args.txt'),'-jar example.jar')
    [IO.File]::WriteAllText((Join-Path $fixture 'manager-profile.json'),'{"Name":"Java Argfile Server","ClientMode":"custom","LaunchMode":"argfile","ArgumentsFile":"libraries/example/win_args.txt","SparkMode":"disabled"}')
}
foreach($name in @('whitelist.json','ops.json','banned-players.json')) { [IO.File]::WriteAllText((Join-Path $server $name),'[]') }
$exe = Join-Path $repoRoot 'dist\CraftHarbor.exe'
$check = Start-Process -FilePath $exe -ArgumentList @('--experience-check','--workspace',('"'+$fixture+'"')) -Wait -PassThru
$report = Join-Path $fixture 'experience-verification.txt'
if(Test-Path -LiteralPath $report) { Get-Content -LiteralPath $report }
if($check.ExitCode -ne 0) { throw 'UI fixture checks failed.' }
