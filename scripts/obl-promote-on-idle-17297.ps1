# Compatibility shim for obsolete idle-only v1.72.97 promotion.
# Owner override 2026-10-28: generating/draft/toolBusy NEVER block deployment.
$ErrorActionPreference='Stop'
if($env:COMPUTERNAME -ne 'SATANAMD'){throw 'WRONG_HOST'}
$statePath=Join-Path $env:LOCALAPPDATA 'OpenBrowser\ChatGPT-Lite\diagnostics\lite-release-satanamd-state.json'
$state=Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
if($state.phase -ne 'queued'){Write-Output 'NO_QUEUED_RELEASE';return}
$task=Get-ScheduledTask -TaskName 'OBL Force Deploy 17299'
if($task.Principal.LogonType -ne 'Interactive'){throw 'DEPLOY_TASK_NOT_INTERACTIVE'}
Start-ScheduledTask -TaskName 'OBL Force Deploy 17299'
'CANONICAL_FORCE_DEPLOY_QUEUED_WITHOUT_IDLE_CHECK'
