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
. (Join-Path $root 'obl-watch-stall-rules-satanamd.ps1')
. (Join-Path $root 'obl-browser-process-filter-satanamd.ps1')
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
 $deploymentOutput=@(& $force -Mode $mode)
 if(-not $?){throw 'FORCE_DEPLOY_SCRIPT_FAILED'}
 $confirmed=@($deploymentOutput|Where-Object {
    $_ -match '^(FORCED_LIVE_PID|ROLLED_BACK_PID)=\d+$'})
 if($confirmed.Count -gt 0){
   Audit 'recovery_executed' ($reason+' '+$confirmed[-1])
 }else{
   Audit 'recovery_skipped' ($reason+' no_live_pid_returned')
   if(Test-Path $watchState){
     try {
       $unchanged=Get-Content -LiteralPath $watchState -Raw|ConvertFrom-Json
       if([string]$unchanged.lastRunUtc -eq $now.ToString('o')){
         Remove-Item -LiteralPath $watchState -Force
       }
     }catch{Audit 'warning' 'recovery_skipped_state_cleanup_failed'}
   }
 }
}
function CanDeployQueuedWhenIdle($obs,[int]$livePid,[DateTimeOffset]$now,[DateTimeOffset]$nextDue){
 try {
  if($null -eq $obs){return $false}
  $emptyAppError=([string]$obs.error -eq 'chatgpt_app_problem' -and
    [int]$obs.turns -eq 0)
  if([int]$obs.pid -ne $livePid -or
    [bool]$obs.generating -or [bool]$obs.draft -or [bool]$obs.toolBusy -or
    (-not [string]::IsNullOrWhiteSpace([string]$obs.error) -and
      -not $emptyAppError)) {return $false}
  $age=($now-[DateTimeOffset]::Parse([string]$obs.utc)).TotalSeconds
  # Overdue rotation on an empty app-error page is not an imminent healthy
  # wake: it must not block a verified idle recovery release indefinitely.
  $overdueEmptyRecovery=$emptyAppError -and $nextDue -le $now.AddMinutes(-2)
  if($age -lt -5 -or $age -gt 40 -or
     [string]$obs.source -notmatch '^https://chatgpt\.com/c/[0-9a-f-]{36}$' -or
     ($nextDue -le $now.AddMinutes(2) -and -not $overdueEmptyRecovery)) {return $false}
  return $true
 }catch{return $false}
}
if($Mode -eq 'SelfTest'){
 if(-not(Test-Path $force)){throw 'FORCE_SCRIPT_MISSING'}
 $state=Get-Content $release -Raw|ConvertFrom-Json
 if($state.phase -notin @('queued','trial','rolled_back')){throw 'RELEASE_STATE_NOT_RECOVERABLE'}
 if(-not(Test-Path $wake)){throw 'WAKE_JOURNAL_MISSING'}
 $now=[DateTimeOffset]::UtcNow
 $fixture=[pscustomobject]@{utc=$now.ToString('o');pid=20;
   generating=$false;draft=$false;toolBusy=$false;error='';
   source='https://chatgpt.com/c/11111111-1111-1111-1111-111111111111'}
 $due=$now.AddMinutes(15)
 if(-not (CanDeployQueuedWhenIdle $fixture 20 $now $due)){throw 'IDLE_FIXTURE_FALSE_NEGATIVE'}
 $fixture.generating=$true
 if(CanDeployQueuedWhenIdle $fixture 20 $now $due){throw 'IDLE_GENERATING_NOT_BLOCKED'}
 $fixture.generating=$false;$fixture.draft=$true
 if(CanDeployQueuedWhenIdle $fixture 20 $now $due){throw 'IDLE_DRAFT_NOT_BLOCKED'}
 $fixture.draft=$false;$fixture.toolBusy=$true
 if(CanDeployQueuedWhenIdle $fixture 20 $now $due){throw 'IDLE_TOOL_BUSY_NOT_BLOCKED'}
 $fixture.toolBusy=$false;$fixture.utc=$now.AddMinutes(-2).ToString('o')
 if(CanDeployQueuedWhenIdle $fixture 20 $now $due){throw 'IDLE_STALE_NOT_BLOCKED'}
 $fixture.utc=$now.ToString('o')
 if(CanDeployQueuedWhenIdle $fixture 20 $now $now.AddMinutes(1)){throw 'IDLE_WAKE_IMMINENT_NOT_BLOCKED'}
 if(CanDeployQueuedWhenIdle $fixture 21 $now $due){throw 'IDLE_DIFFERENT_PROCESS_NOT_BLOCKED'}
 $fixture.error='chatgpt_app_problem';$fixture|Add-Member -NotePropertyName turns -NotePropertyValue 0
 if(-not (CanDeployQueuedWhenIdle $fixture 20 $now $now.AddMinutes(-4))){throw 'EMPTY_APP_ERROR_OVERDUE_RECOVERY_BLOCKED'}
 if(CanDeployQueuedWhenIdle $fixture 20 $now $now.AddMinutes(1)){throw 'APP_ERROR_IMMINENT_WAKE_UNGUARDED'}
 $fixture.generating=$true
 if(CanDeployQueuedWhenIdle $fixture 20 $now $now.AddMinutes(-4)){throw 'APP_ERROR_GENERATING_UNGUARDED'}
 $fixture.generating=$false;$fixture.draft=$true
 if(CanDeployQueuedWhenIdle $fixture 20 $now $now.AddMinutes(-4)){throw 'APP_ERROR_DRAFT_UNGUARDED'}
 $fixture.draft=$false;$fixture.turns=1
 if(CanDeployQueuedWhenIdle $fixture 20 $now $now.AddMinutes(-4)){throw 'APP_ERROR_NONEMPTY_UNGUARDED'}
 $fixture.turns=0;$fixture.error='response_error'
 if(CanDeployQueuedWhenIdle $fixture 20 $now $now.AddMinutes(-4)){throw 'OTHER_ERROR_UNGUARDED'}
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
 $ps=@(GetOblLiveBrowserProcesses @(Get-CimInstance Win32_Process -Filter "Name='BridgeBrowserAlpha0.exe'"))
 if($state.phase -eq 'queued'){
  if($ps.Count -eq 0){Recover 'Deploy' 'queued_browser_missing';return}
  if($ps.Count -ne 1 -or $ps[0].SessionId -ne 1 -or
     [int]$ps[0].ProcessId -ne [int]$state.previousPid){
    Audit 'observe' 'queued_browser_identity_unverified'
    return
  }
  # NIEAKTUALNE od 2026-11-02: oczekiwanie na idle/generating/draft/termin wake nie moze blokowac wydania.
  # Pozostawione ponizej historyczne warunki sa wylaczone zgodnie z decyzja wlasciciela.
  if($false){
  $now=[DateTimeOffset]::UtcNow
  if(($now-[DateTimeOffset]::Parse([string]$state.queuedAtUtc)).TotalSeconds -lt 60){
    Audit 'observe' 'queued_wait_first_minute'
    return
  }
  $obs=$null;$nextDue=$now
  try{
    $obs=Get-Content -LiteralPath $preflight -Raw|ConvertFrom-Json
    $projectDb=Get-Content -LiteralPath (Join-Path (Split-Path $diag -Parent) 'projects.json') -Raw|ConvertFrom-Json
    $lead=@($projectDb.Projects|Where-Object Key -eq 'obl')[0]
    $nextDue=[DateTimeOffset]::Parse([string]$lead.Wake.DueUtc)
  }catch{Audit 'observe' 'queued_idle_evidence_unavailable';return}
  if(-not (CanDeployQueuedWhenIdle $obs $ps[0].ProcessId $now $nextDue)){
    Audit 'observe' 'queued_release_lead_busy_or_imminent_wake'
    return
  }
  } # Koniec NIEAKTUALNEJ bramki idle: LIVE moze generowac podczas wdrozenia.
  # CIM moze zwrocic pusty ExecutablePath w sesji niepodwyzszonej.
  # Wtedy sprawdzamy zapisany previousExe z release state, jego SHA i PID/session.
  $previousPath=if([string]::IsNullOrWhiteSpace([string]$ps[0].ExecutablePath)){$state.previousExe}else{$ps[0].ExecutablePath}
  if(-not (Test-Path -LiteralPath $state.candidateExe) -or
     -not (Test-Path -LiteralPath $previousPath) -or
     (Get-FileHash -LiteralPath $state.candidateExe -Algorithm SHA256).Hash -ne $state.candidateSha -or
     (Get-FileHash -LiteralPath $previousPath -Algorithm SHA256).Hash -ne $state.previousSha){
    Audit 'warning' 'queued_release_binary_sha_mismatch'
    return
  }
  Recover 'Deploy' 'queued_release_immediate_owner_authorized'
  return
 }
 if($ps.Count -eq 0 -and $state.phase -in @('trial','rolled_back')){Recover 'Restart' 'browser_missing';return}
 if($ps.Count -gt 1){Recover 'Restart' 'multiple_browsers_detected';return}
 if($ps.Count -eq 0){Audit 'warning' 'browser_missing_unknown_state';return}
 $p=$ps[0]
 $stale=$true
 $unusablePage=$false
 if(Test-Path $preflight){
  try{$obs=Get-Content $preflight -Raw|ConvertFrom-Json
    if($obs.pid -eq $p.ProcessId -and ([DateTimeOffset]::UtcNow-[DateTimeOffset]::Parse([string]$obs.utc)).TotalMinutes -lt 5){
      $stale=$false
      $unusablePage=([string]$obs.error -eq 'chat_page_unavailable' -and [int]$obs.turns -eq 0)
    }
  }catch{}
 }
 if($unusablePage){
  # Only a persistent, same-PID empty page qualifies for process recovery.
  # A wake click/uncertain receipt is never retried or submitted by this path.
  try {
   $liteEvents=Join-Path $diag ('lite-events-'+[DateTimeOffset]::UtcNow.ToString('yyyy-MM-dd')+'.jsonl')
   if(Test-Path $liteEvents){
    $transitions=@(Get-Content $liteEvents -Tail 240 -Encoding UTF8 |
      ForEach-Object {try{$_|ConvertFrom-Json}catch{}} |
      Where-Object {$_.pid -eq $p.ProcessId -and $_.eventName -eq 'response_error_transition'} |
      Select-Object -Last 1)
    if($transitions.Count -eq 1 -and
      [string]$transitions[0].details.state -eq 'chat_page_unavailable' -and
      ([DateTimeOffset]::UtcNow-[DateTimeOffset]::Parse([string]$transitions[0].utc)).TotalSeconds -ge 120){
     Recover 'Restart' ('persistent_chat_page_unavailable_pid_'+$p.ProcessId)
     return
    }
   }
  }catch{Audit 'warning' ('unusable_page_probe_failed '+$_.Exception.Message)}
  Audit 'degraded' ('fresh_preflight_unusable_page pid='+$p.ProcessId+' turns=0 error=chat_page_unavailable')
  return
 }
 if($stale){
  $age=999999
  try{$rp=Get-Process -Id $p.ProcessId -ErrorAction Stop
   $age=((Get-Date)-$rp.StartTime).TotalSeconds}catch{}
  if($age -lt 150){Audit 'healthy' 'new_process_webview_startup_grace';return}
  # Rotation may be waiting for a real successor ACK while the old chat
  # preflight is stale. A fresh same-PID GUI heartbeat is evidence of a live
  # WebView2 process, so do not destroy the one-shot successor conversation.
  # This is a bounded exception, not an indefinite suppression of recovery.
  try {
   $projectPath=Join-Path (Split-Path $diag -Parent) 'projects.json'
   $liteEvents=Join-Path $diag ('lite-events-'+[DateTimeOffset]::Now.ToString('yyyy-MM-dd')+'.jsonl')
   if((Test-Path $projectPath) -and (Test-Path $liteEvents)){
    $projectDb=Get-Content $projectPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $now=[DateTimeOffset]::UtcNow
    $rotation=@($projectDb.Projects | Where-Object {
      $_.Wake -and $_.Wake.Enabled -and $_.Wake.RotationPhase -eq 'send_uncertain' -and
      $_.Wake.RotationReceipt -match '^[0-9a-fA-F]{32}$' -and
      ([DateTimeOffset]::Parse([string]$_.Wake.RotationStartedUtc) -le $now) -and
      (($now-[DateTimeOffset]::Parse([string]$_.Wake.RotationStartedUtc)).TotalMinutes -lt 30)
    })
    if($rotation.Count -gt 0){
     $heartbeats=@(Get-Content $liteEvents -Tail 150 -Encoding UTF8 |
       ForEach-Object {try {$_ | ConvertFrom-Json} catch {}} |
       Where-Object {$_.pid -eq $p.ProcessId -and $_.eventName -eq 'ui_heartbeat'} |
       Select-Object -Last 1)
     if($heartbeats.Count -eq 1 -and
       ($now-[DateTimeOffset]::Parse([string]$heartbeats[0].utc)).TotalSeconds -le 90 -and
       (Get-Process -Id $p.ProcessId -ErrorAction Stop).Responding){
      Audit 'observe' 'rotation_pending_live_gui_heartbeat_no_restart'
      return
     }
    }
   }
  } catch {Audit 'warning' ('rotation_guard_probe_failed '+$_.Exception.Message)}
  Recover 'Restart' 'no_fresh_webview_preflight';return
 }
 $rows=@(Get-Content $wake -Tail 1800 -Encoding UTF8|ForEach-Object {try{$_|ConvertFrom-Json}catch{}}|Where-Object {$_.actor -eq 'engine'})
 $ack=@($rows|Where-Object {$_.stage -eq 'receipt_confirmed'}|Select-Object -Last 1)
 $due=@($rows|Where-Object {$_.stage -eq 'due'}|Select-Object -Last 1)
 if($ack.Count -eq 0 -or $due.Count -eq 0){Audit 'observe' 'no_wake_history';return}
 $ackUtc=[DateTimeOffset]::Parse([string]$ack[0].utc)
 $dueUtc=[DateTimeOffset]::Parse([string]$due[0].utc)
 $now=[DateTimeOffset]::UtcNow
 if($dueUtc -le $ackUtc){
  $health=Join-Path $diag 'obl-wake-health.json'
  if(Test-Path $health){
   $wasDegraded=$false
   try {
    $previous=Get-Content $health -Raw | ConvertFrom-Json
    $wasDegraded=[string]$previous.status -in @('degraded','needs_attention')
   } catch {}
   if($wasDegraded){
    $hstatus=[ordered]@{schema=1;utc=$now.ToString('o');status='healthy';cause='recovered_after_ack';lastAckUtc=$ackUtc.ToString('o')}
    $tmp=$health+'.tmp'
    [IO.File]::WriteAllText($tmp,($hstatus|ConvertTo-Json -Compress),$utf8)
    Move-Item $tmp $health -Force
    Audit 'recovered' 'new_engine_ack_after_degraded'
   }
  }
  Audit 'healthy' 'no_overdue_wake';return
 }
 if(($now-$ackUtc).TotalMinutes -lt 25){Audit 'healthy' 'recent_ack';return}
 # A clicked wake with uncertain receipt must not be retried or
 # blindly resent. A bounded UNCERTAIN receipt fence is independent of generating state.
 $latestAttempt=@($rows|Where-Object {$_.stage -in @('submission_attempted','receipt_uncertain')}|Select-Object -Last 1)
 if($latestAttempt.Count -gt 0){
  $attemptUtc=[DateTimeOffset]::Parse([string]$latestAttempt[0].utc)
  if($attemptUtc -gt $ackUtc -and ($now-$attemptUtc).TotalMinutes -lt 10){
   Audit 'healthy' 'recent_uncertain_send_grace'
   return
  }
 }
 $deferred=@($rows|Where-Object {$_.stage -eq 'deferred' -and ([DateTimeOffset]::Parse([string]$_.utc) -gt $ackUtc)})
 # A backend GET 429 is external rate limiting, not a hung WebView2.
 # Restarting a healthy browser cannot lift this limit and triggers GETs.
 $lastDeferred=@($deferred|Select-Object -Last 1)
 $recent429=@($deferred|Where-Object {
   $_.result -eq 'backend_http_429' -and
   ([DateTimeOffset]::Parse([string]$_.utc) -gt $now.AddMinutes(-60))
 })
 if($lastDeferred.Count -gt 0 -and $recent429.Count -gt 0 -and
    $lastDeferred[0].result -in @('backend_http_429','response_error')){
  $health=Join-Path $diag 'obl-wake-health.json'
  $status=[ordered]@{
   schema=1; utc=$now.ToString('o'); status='degraded'
   cause='backend_http_429'; action='backoff_no_restart'
   lastAckUtc=$ackUtc.ToString('o')
   deferredSinceAck=$deferred.Count; recent429=$recent429.Count
   nextAction='retry_read_after_rate_limit_clears'
  }
  $htmp=$health+'.tmp'
  [IO.File]::WriteAllText($htmp,($status|ConvertTo-Json -Compress),$utf8)
  Move-Item $htmp $health -Force
  Audit 'degraded' ('backend_http_429_no_restart deferred_'+$deferred.Count+
    ' recent429_'+$recent429.Count)
  return
 }
 # A stuck last USER turn is not a crashed process. Restarting cannot
 # complete a pending ChatGPT answer and must not silently resend a prompt.
 if($lastDeferred.Count -gt 0 -and $deferred.Count -ge 3 -and
    $lastDeferred[0].result -eq 'same_answer_already_woken'){
  $health=Join-Path $diag 'obl-wake-health.json'
  $htmp=$health+'.tmp'
  $status=[ordered]@{
   schema=1;utc=$now.ToString('o');status='needs_attention'
   cause=[string]$lastDeferred[0].result;action='no_restart_no_resend'
   lastAckUtc=$ackUtc.ToString('o')
   deferredSinceAck=$deferred.Count
   nextAction='new_completed_answer_or_verified_handoff_required'
  }
  [IO.File]::WriteAllText($htmp,($status|ConvertTo-Json -Compress),$utf8)
  Move-Item $htmp $health -Force
  Audit 'needs_attention' (([string]$lastDeferred[0].result)+'_no_restart deferred_'+$deferred.Count)
  return
 }
 # Purely conversational / server-side deferrals are not proof of a broken GUI.
 # In particular same_answer_already_woken after a confirmed receipt can persist
 # indefinitely; restarting the healthy WebView every 10 minutes did not help.
 # Browser absence, stale preflight and multiple instances are handled above.
 $conversationalReasons=@('same_answer_already_woken',
  'backend_http_429','response_error','conversation_length_exceeded')
 if($lastDeferred.Count -gt 0 -and $deferred.Count -ge 3 -and
    $lastDeferred[0].result -in $conversationalReasons){
  $cause=[string]$lastDeferred[0].result
  $health=Join-Path $diag 'obl-wake-health.json'
  $htmp=$health+'.tmp'
  $status=[ordered]@{schema=1;utc=$now.ToString('o');status='needs_attention'
    cause=$cause;action='no_restart_no_resend';lastAckUtc=$ackUtc.ToString('o')
    deferredSinceAck=$deferred.Count
    nextAction='verify_last_reply_or_rotate_lead_using_durable_evidence'}
  [IO.File]::WriteAllText($htmp,($status|ConvertTo-Json -Compress),$utf8)
  Move-Item $htmp $health -Force
  Audit 'needs_attention' ($cause+'_no_restart deferred_'+$deferred.Count)
  return
 }
 if($deferred.Count -ge 3){Recover 'Restart' ('wake_stalled_deferred_'+$deferred.Count);return}
 Audit 'observe' ('pending_wake_deferred_'+$deferred.Count)
}catch{Audit 'error' $_.Exception.Message;throw}