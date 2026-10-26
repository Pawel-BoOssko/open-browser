param([ValidateSet('Watch','SelfTest')][string]$Mode='Watch')
$ErrorActionPreference='Stop'
if($env:COMPUTERNAME -ne 'SATANAMD'){throw 'WRONG_HOST'}
$root='D:\projects\open-browser\scripts'
$diag=Join-Path $env:LOCALAPPDATA 'OpenBrowser\ChatGPT-Lite\diagnostics'
$release=Join-Path $diag 'lite-release-satanamd-state.json'
$wake=Join-Path $diag 'wake-lifecycle.jsonl'
$preflight=Join-Path $diag 'live-preflight.json'
$pause=Join-Path $diag 'keepalive-paused.json'
$watchState=Join-Path $diag 'obl-force-watch-state.json'
$events=Join-Path $diag 'obl-force-watch.jsonl'
$force=Join-Path $root 'obl-force-deploy-satanamd.ps1'
$utf8=[Text.UTF8Encoding]::new($false)
function Audit([string]$decision,[string]$reason){
 [IO.File]::AppendAllText($events,([ordered]@{utc=[DateTimeOffset]::UtcNow.ToString('o');decision=$decision;reason=$reason}|ConvertTo-Json -Compress)+[Environment]::NewLine,$utf8)
}
function Recover([string]$mode,[string]$reason){
 $now=[DateTimeOffset]::UtcNow
 if(Test-Path $watchState){
  try{$last=Get-Content $watchState -Raw | ConvertFrom-Json
   if(($now-[DateTimeOffset]::Parse([string]$last.lastRunUtc)).TotalMinutes -lt 10){Audit 'cooldown' $reason;return}
  }catch{}
 }
 $temp=$watchState+'.tmp'
 [IO.File]::WriteAllText($temp,([ordered]@{lastRunUtc=$now.ToString('o');reason=$reason;mode=$mode}|ConvertTo-Json),$utf8)
 Move-Item $temp $watchState -Force
 Audit 'recovery_start' ($reason+' mode='+$mode)
 & $force -Mode $mode
 Audit 'recovery_executed' $reason
}
if($Mode -eq 'SelfTest'){
 if(-not(Test-Path $force)){throw 'FORCE_SCRIPT_MISSING'}
 $state=Get-Content $release -Raw|ConvertFrom-Json
 if($state.phase -notin @('queued','trial','rolled_back')){throw 'RELEASE_STATE_NOT_RECOVERABLE'}
 if(-not(Test-Path $wake)){throw 'WAKE_JOURNAL_MISSING'}
 Write-Output 'OBL_FORCE_WATCH_SELFTEST_OK';return
}
try{
 if(Test-Path $pause){
  $staleForcePause=$false
  try{$hold=Get-Content $pause -Raw|ConvertFrom-Json
   if($hold.owner -eq 'obl-force-deploy' -and ([DateTimeOffset]::UtcNow-[DateTimeOffset]::Parse([string]$hold.utc)).TotalMinutes -gt 3){$staleForcePause=$true}
  }catch{}
  if($staleForcePause){Remove-Item -LiteralPath $pause -Force;Audit 'stale_force_pause_cleared' 'older_than_3_minutes'}
  else{Audit 'skip' 'keepalive_paused';return}
 }
 $state=Get-Content $release -Raw -Encoding UTF8|ConvertFrom-Json
 $ps=@(Get-CimInstance Win32_Process -Filter "Name='BridgeBrowserAlpha0.exe'")
 if($state.phase -eq 'queued'){
  if(([DateTimeOffset]::UtcNow-[DateTimeOffset]::Parse([string]$state.queuedAtUtc)).TotalMinutes -ge 1){Recover 'Deploy' 'queued_release_pending';return}
 }
 if($ps.Count -eq 0 -and $state.phase -in @('trial','rolled_back')){Recover 'Restart' 'browser_missing';return}
 if($ps.Count -gt 1){Recover 'Restart' 'multiple_browsers_detected';return}
 if($ps.Count -eq 0){Audit 'warning' 'browser_missing_unknown_state';return}
 $p=$ps[0]
 $stale=$true
 if(Test-Path $preflight){
  try{$obs=Get-Content $preflight -Raw|ConvertFrom-Json
    if($obs.pid -eq $p.ProcessId -and ([DateTimeOffset]::UtcNow-[DateTimeOffset]::Parse([string]$obs.utc)).TotalMinutes -lt 5){$stale=$false}
  }catch{}
 }
 if($stale){Recover 'Restart' 'no_fresh_webview_preflight';return}
 $rows=@(Get-Content $wake -Tail 1800 -Encoding UTF8|ForEach-Object {try{$_|ConvertFrom-Json}catch{}}|Where-Object {$_.actor -eq 'engine'})
 $ack=@($rows|Where-Object {$_.stage -eq 'receipt_confirmed'}|Select-Object -Last 1)
 $due=@($rows|Where-Object {$_.stage -eq 'due'}|Select-Object -Last 1)
 if($ack.Count -eq 0 -or $due.Count -eq 0){Audit 'observe' 'no_wake_history';return}
 $ackUtc=[DateTimeOffset]::Parse([string]$ack[0].utc)
 $dueUtc=[DateTimeOffset]::Parse([string]$due[0].utc)
 $now=[DateTimeOffset]::UtcNow
 if($dueUtc -le $ackUtc){Audit 'healthy' 'no_overdue_wake';return}
 if(($now-$ackUtc).TotalMinutes -lt 25){Audit 'healthy' 'recent_ack';return}
 $deferred=@($rows|Where-Object {$_.stage -eq 'deferred' -and ([DateTimeOffset]::Parse([string]$_.utc) -gt $ackUtc)})
 if($deferred.Count -ge 3){Recover 'Restart' ('wake_stalled_deferred_'+$deferred.Count);return}
 Audit 'observe' ('pending_wake_deferred_'+$deferred.Count)
}catch{Audit 'error' $_.Exception.Message;throw}