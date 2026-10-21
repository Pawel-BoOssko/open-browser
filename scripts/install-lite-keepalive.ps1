# Install/update Open Browser Lite keepalive for the current logged-on Windows user.
# Run once on PBOX1; no administrator account, login credentials or ChatGPT Tasks.
param([switch]$Remove)
$ErrorActionPreference = 'Stop'
$name = 'Open Browser Lite Keepalive'
$folder = Join-Path $env:LOCALAPPDATA 'OpenBrowser\ChatGPT-Lite\diagnostics'
$target = Join-Path $folder 'keepalive-chatgpt-lite.ps1'

if ($Remove) {
    Unregister-ScheduledTask -TaskName $name -Confirm:$false -ErrorAction SilentlyContinue
    Write-Output 'KEEPALIVE_TASK_REMOVED'
    return
}

New-Item -ItemType Directory -Path $folder -Force | Out-Null
$source = Join-Path $PSScriptRoot 'keepalive-chatgpt-lite.ps1'
if (-not [string]::Equals($source, $target, [StringComparison]::OrdinalIgnoreCase)) {
    Copy-Item -LiteralPath $source -Destination $target -Force
}
$user = [Security.Principal.WindowsIdentity]::GetCurrent().Name
$engine = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
$args = '-NoLogo -NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -File "' + $target + '"'
$action = New-ScheduledTaskAction -Execute $engine -Argument $args
$onLogon = New-ScheduledTaskTrigger -AtLogOn -User $user
$everyFiveMinutes = New-ScheduledTaskTrigger -Once -At (Get-Date).AddMinutes(1) `
    -RepetitionInterval (New-TimeSpan -Minutes 5) -RepetitionDuration (New-TimeSpan -Days 3650)
$principal = New-ScheduledTaskPrincipal -UserId $user -LogonType Interactive -RunLevel Limited
$settings = New-ScheduledTaskSettingsSet -MultipleInstances IgnoreNew -ExecutionTimeLimit (New-TimeSpan -Minutes 2) -StartWhenAvailable
Register-ScheduledTask -TaskName $name -Action $action `
    -Trigger @($onLogon, $everyFiveMinutes) -Principal $principal `
    -Settings $settings -Force | Out-Null
& $target -DryRun
Write-Output ('KEEPALIVE_TASK_INSTALLED for ' + $user)
