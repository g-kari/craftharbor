$ErrorActionPreference='Stop'
$repoRoot=Split-Path $PSScriptRoot -Parent
$dist=Join-Path $repoRoot 'dist'
$request=Get-Content -LiteralPath (Join-Path $repoRoot 'release.json') -Raw | ConvertFrom-Json
$version=[string]$request.version
if($version -notmatch '^\d+\.\d+\.\d+(-[a-z0-9.-]+)?$'){throw 'Invalid release version.'}
$numericVersion=($version.Split('-')[0])+'.0'
$setupName="CraftHarbor-Setup-$version.exe"
$uninstallName="CraftHarbor-Uninstall-$version.exe"
foreach($name in @('CraftHarbor.exe','CraftHarbor.Host.exe',$setupName,$uninstallName)){
 $info=[Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $dist $name))
 $actualVersion=("{0}.{1}.{2}.{3}" -f $info.FileMajorPart,$info.FileMinorPart,$info.FileBuildPart,$info.FilePrivatePart)
 if($actualVersion -ne $numericVersion){throw "Version mismatch in $name : $actualVersion, expected $numericVersion"}
}
$archive="CraftHarbor-$version-windows-x64.zip"
$names=@($archive,($archive+'.sha256'),$setupName,($setupName+'.sha256'),$uninstallName,($uninstallName+'.sha256'))
$assets=@()
foreach($name in $names){
 $path=Join-Path $dist $name
 if(!(Test-Path -LiteralPath $path)){throw "Missing release asset: $name"}
 $hash=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
 if($name -notlike '*.sha256'){
  $expected=(Get-Content -LiteralPath ($path+'.sha256') -Raw).Trim()
  if($expected -ne ($hash+'  '+$name)){throw "Checksum sidecar mismatch: $name"}
 }
 $assets+=@{name=$name;size=(Get-Item -LiteralPath $path).Length;sha256=$hash}
}
$notes=Join-Path $repoRoot ("docs/releases/v"+$version+".md")
if(!(Test-Path -LiteralPath $notes)){throw 'Release notes missing.'}
$sourceCommit=$env:GITHUB_SHA
if($sourceCommit -notmatch '^[a-f0-9]{40}$'){throw 'Release manifest requires the CI source commit.'}
$manifest=@{version=$version;source_commit=$sourceCommit;assets=$assets}
[IO.File]::WriteAllText((Join-Path $dist 'release-manifest.json'),($manifest | ConvertTo-Json -Depth 5),[Text.UTF8Encoding]::new($false))
Write-Output "PASS: $version app/host/setup/uninstall versions, six assets and SHA-256 sidecars agree."
