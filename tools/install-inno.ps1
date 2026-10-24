$ErrorActionPreference='Stop'
$repoRoot=Split-Path $PSScriptRoot -Parent
$tools=Join-Path $repoRoot 'work\tools'
New-Item -ItemType Directory -Path $tools -Force | Out-Null
$installer=Join-Path $tools 'innosetup-6.7.3.exe'
$destination=Join-Path $tools 'inno'
Invoke-WebRequest -Uri 'https://github.com/jrsoftware/issrc/releases/download/is-6_7_3/innosetup-6.7.3.exe' -OutFile $installer -UseBasicParsing
if((Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash.ToLowerInvariant() -ne '9c73c3bae7ed48d44112a0f48e66742c00090bdb5bef71d9d3c056c66e97b732'){throw 'Inno Setup compiler hash mismatch.'}
if((Get-AuthenticodeSignature -LiteralPath $installer).Status -ne 'Valid'){throw 'Inno Setup compiler signature is not valid.'}
$process=Start-Process -FilePath $installer -ArgumentList @('/VERYSILENT','/CURRENTUSER','/SUPPRESSMSGBOXES','/NORESTART','/NOICONS','/SP-',('/DIR="'+$destination+'"')) -WindowStyle Hidden -Wait -PassThru
if($process.ExitCode -ne 0){throw 'Inno Setup compiler installation failed.'}
Write-Output (Join-Path $destination 'ISCC.exe')
