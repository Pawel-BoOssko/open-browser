$ErrorActionPreference = 'Stop'
$launcher = Join-Path $PSScriptRoot 'claude-deepseek.ps1'
Write-Output '=== launcher ==='
if (!(Test-Path $launcher)) { throw 'Missing launcher file' }
Get-Item $launcher | Select-Object FullName,Length,LastWriteTime | Format-List
Write-Output '=== claude path ==='
where.exe claude
Write-Output '=== syntax check ==='
$errors = $null
[System.Management.Automation.PSParser]::Tokenize((Get-Content $launcher -Raw), [ref]$errors) | Out-Null
if ($errors) { $errors | Format-List *; throw 'PSParser errors in launcher' }
Write-Output 'PowerShell parser check OK'
Write-Output '=== argument forwarding smoke test ==='
& $launcher --version
