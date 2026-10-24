$ErrorActionPreference='Stop'
$files=Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.ps1' | Sort-Object Name
foreach($file in $files){
 $tokens=$null;$errors=$null
 [void][Management.Automation.Language.Parser]::ParseFile($file.FullName,[ref]$tokens,[ref]$errors)
 if($errors.Count -gt 0){
  foreach($error in $errors){Write-Output ($file.Name+':'+$error.Extent.StartLineNumber+': '+$error.Message)}
  throw "PowerShell syntax validation failed: $($file.Name)"
 }
}
Write-Output "PASS: parsed $($files.Count) PowerShell scripts without executing them."
