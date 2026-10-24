$ErrorActionPreference='Stop'
$repoRoot=Split-Path $PSScriptRoot -Parent
$workRoot=[IO.Path]::GetFullPath((Join-Path $repoRoot 'work'))
$fixture=[IO.Path]::GetFullPath((Join-Path $workRoot ('installer-check-'+[guid]::NewGuid().ToString('N'))))
if(-not $fixture.StartsWith($workRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Installer fixture escaped the workspace.'}
$registryPath='HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{3B9C29C1-CC53-49D4-9B35-EA0D4835CB08}_is1'
if(Test-Path -LiteralPath $registryPath){throw 'An existing CraftHarbor installation is present; skip isolated installer test to preserve it.'}
$shortcut=Join-Path ([Environment]::GetFolderPath('Programs')) 'CraftHarbor.lnk'
if(Test-Path -LiteralPath $shortcut){throw 'An existing Start menu shortcut is present; skip isolated installer test to preserve it.'}
$version=(Get-Content -LiteralPath (Join-Path $repoRoot 'release.json') -Raw | ConvertFrom-Json).version
$setup=Join-Path $repoRoot ("dist\CraftHarbor-Setup-"+$version+'.exe')
New-Item -ItemType Directory -Path $fixture -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $fixture 'uninstall.exe'),'synthetic obsolete CraftHarbor launcher')
$process=Start-Process -FilePath $setup -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/SP-',('/DIR="'+$fixture+'"'),'/TASKS=""') -WindowStyle Hidden -Wait -PassThru
if($process.ExitCode -ne 0){throw "Setup failed: $($process.ExitCode)"}
foreach($file in @('CraftHarbor.exe','CraftHarbor.Host.exe','CraftHarbor-Uninstall.exe','unins000.exe','recipes\server-packs.json')){if(-not(Test-Path -LiteralPath (Join-Path $fixture $file))){throw "Installed file missing: $file"}}
if((Get-FileHash -LiteralPath (Join-Path $fixture 'CraftHarbor.exe')).Hash -ne (Get-FileHash -LiteralPath (Join-Path $repoRoot 'dist\CraftHarbor.exe')).Hash){throw 'Installed application hash differs.'}
if(-not(Test-Path -LiteralPath $registryPath)){throw 'Installed-apps registration missing.'}
if(Test-Path -LiteralPath (Join-Path $fixture 'uninstall.exe')){throw 'Obsolete generic launcher was not removed.'}
if((Get-FileHash -LiteralPath (Join-Path $fixture 'CraftHarbor-Uninstall.exe')).Hash -ne (Get-FileHash -LiteralPath (Join-Path $repoRoot ("dist\CraftHarbor-Uninstall-"+$version+'.exe'))).Hash){throw 'Installed branded uninstall launcher differs.'}
$world=Join-Path $fixture 'server\world\keep.txt'
New-Item -ItemType Directory -Path (Split-Path $world -Parent) -Force | Out-Null
[IO.File]::WriteAllText($world,'synthetic workspace data must survive uninstall')
$privateMarker=Join-Path $env:LOCALAPPDATA ('CraftHarbor\installer-retention-'+[guid]::NewGuid().ToString('N')+'.txt')
New-Item -ItemType Directory -Path (Split-Path $privateMarker -Parent) -Force | Out-Null
[IO.File]::WriteAllText($privateMarker,'synthetic user-setting retention marker')
$uninstaller=[IO.Path]::GetFullPath((Join-Path $fixture 'unins000.exe'))
if(-not $uninstaller.StartsWith($fixture+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Unexpected test uninstaller path.'}
$process=Start-Process -FilePath $uninstaller -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART') -WindowStyle Hidden -Wait -PassThru
if($process.ExitCode -ne 0){throw "Uninstall failed: $($process.ExitCode)"}
if(Test-Path -LiteralPath (Join-Path $fixture 'CraftHarbor.exe')){throw 'Application was not removed.'}
if(-not(Test-Path -LiteralPath $world)-or-not(Test-Path -LiteralPath $privateMarker)){throw 'User data was removed by uninstall.'}
if(Test-Path -LiteralPath $registryPath){throw 'Uninstall registration was not removed.'}
Remove-Item -LiteralPath $privateMarker
$report='PASS: per-user setup installed matching app/host/recipe/branded-uninstall files, removed the obsolete generic launcher and registered the app; uninstall removed owned binaries and registration while retaining a synthetic world and user settings. Only an isolated workspace fixture was installed.'
[IO.File]::WriteAllText((Join-Path $fixture 'installer-verification.txt'),$report)
Write-Output $report
