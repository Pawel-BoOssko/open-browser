param([switch]$RunNow)
$ErrorActionPreference='Stop'
if($env:COMPUTERNAME -ne 'SATANAMD'){throw 'WRONG_HOST'}
$launcher='D:\projects\open-browser\scripts\obl-launch-current-satanamd.ps1'
$keeper='D:\Apps\OpenBrowserLite\keepalive-satanamd.ps1'
$icon='D:\Apps\OpenBrowserLite\OpenBrowserLite.ico'
$diag=Join-Path $env:LOCALAPPDATA 'OpenBrowser\ChatGPT-Lite\diagnostics'
$backup=Join-Path $diag 'launcher-shortcuts-backup-20261028'
[IO.Directory]::CreateDirectory($backup)|Out-Null
if(-not(Test-Path -LiteralPath $launcher)){throw 'CANONICAL_LAUNCHER_MISSING'}
if(-not(Test-Path -LiteralPath $keeper)){throw 'KEEPALIVE_SCRIPT_MISSING'}
if(-not(Test-Path -LiteralPath $icon)){throw 'LAUNCHER_ICON_MISSING'}
$startMenu=Join-Path ([Environment]::GetFolderPath('StartMenu')) 'Programs'
$startup=[Environment]::GetFolderPath('Startup')
$desktop=[Environment]::GetFolderPath('Desktop')
$links=@(
 (Join-Path $startMenu 'Open Browser Lite.lnk'),
 (Join-Path $startup 'Open Browser Lite.lnk'),
 (Join-Path $desktop 'Open Browser Lite.lnk'),
 (Join-Path $desktop 'Open Browser - ChatGPT Lite.lnk'),
 (Join-Path $desktop 'OpenBrowser.lnk')
)
$exe=Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
$arg='-NoLogo -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File "'+$launcher+'" -Mode Repair'
$ws=New-Object -ComObject WScript.Shell
foreach($path in $links){
  $folder=Split-Path -Parent $path
  [IO.Directory]::CreateDirectory($folder)|Out-Null
  if(Test-Path -LiteralPath $path){
    $file=([IO.Path]::GetFileName($path) -replace '[^a-zA-Z0-9._-]','_')
    $kind=if($folder -eq $startup){'Startup-'}elseif($folder -eq $startMenu){'Menu-'}else{'Desktop-'}
    $saved=Join-Path $backup ($kind+$file)
    if(-not(Test-Path $saved)){Copy-Item -LiteralPath $path -Destination $saved}
  }
  $sc=$ws.CreateShortcut($path)
  $sc.TargetPath=$exe
  $sc.Arguments=$arg
  $sc.WorkingDirectory='D:\Apps\OpenBrowserLite'
  $sc.IconLocation=$icon+',0'
  $sc.Description='Open Browser Lite — zawsze aktualna wersja, z automatycznym przywróceniem'
  $sc.Save()
  $verify=$ws.CreateShortcut($path)
  if($verify.TargetPath -ne $exe -or -not $verify.Arguments.Contains($launcher)){throw ('SHORTCUT_VERIFY_FAILED '+$path)}
  Write-Output ('SHORTCUT_OK='+$path)
}
$pause=Join-Path $diag 'keepalive-paused.json'
if(Test-Path $pause){
  $save=Join-Path $backup ('keepalive-paused.'+(Get-Date -Format 'yyyyMMdd_HHmmss')+'.json')
  Copy-Item $pause $save -ErrorAction Stop
  Remove-Item -LiteralPath $pause -Force -ErrorAction Stop
  Write-Output ('OLD_PAUSE_CLEARED_BACKUP='+$save)
}
$t=Get-ScheduledTask -TaskName 'Open Browser Lite SATANAMD Keepalive' -ErrorAction Stop
if($t.State -eq 'Disabled'){throw 'EXISTING_KEEPALIVE_TASK_DISABLED'}
if(-not ($t.Triggers |Where-Object {$_.CimClass.CimClassName -eq 'MSFT_TaskLogonTrigger' -and $_.Enabled})){
  throw 'KEEPALIVE_NO_ENABLED_LOGON_TRIGGER'
}
if(-not (([IO.File]::ReadAllText($keeper)).Contains($launcher))){throw 'KEEPALIVE_NOT_POINTING_TO_CANONICAL_LAUNCHER'}
Write-Output 'AUTOSTART_VERIFIED=StartupShortcut+LogonKeepaliveTask'
if($RunNow){
  Start-ScheduledTask -TaskName 'Open Browser Lite SATANAMD Keepalive' -ErrorAction Stop
  Write-Output 'INTERACTIVE_LAUNCH_REQUESTED=TRUE'
}
Write-Output 'REPAIR_COMPLETED=TRUE'
