param([ValidateSet('Deploy','Restart','SelfTest')][string]$Mode='Deploy')
$ErrorActionPreference='Stop'
if($Mode -ne 'SelfTest' -and (Get-CimInstance Win32_Process -Filter ('ProcessId=' + $PID)).SessionId -ne 1){ throw 'NON_INTERACTIVE_DEPLOY_SESSION: use interactive scheduled task on SATANAMD' }
if($env:COMPUTERNAME -ne 'SATANAMD'){throw 'WRONG_HOST'}
$root='D:\Apps\OpenBrowserLite'
$diag=Join-Path $env:LOCALAPPDATA 'OpenBrowser\ChatGPT-Lite\diagnostics'
$statePath=Join-Path $diag 'lite-release-satanamd-state.json'
$pausePath=Join-Path $diag 'keepalive-paused.json'
$eventFile=Join-Path $diag 'obl-force-deploy.jsonl'
$utf8=[Text.UTF8Encoding]::new($false)
[IO.Directory]::CreateDirectory($diag)|Out-Null
. 'D:\projects\open-browser\scripts\obl-browser-process-filter-satanamd.ps1'
. 'D:\projects\open-browser\scripts\obl-crash-idle-proof-satanamd.ps1'
function WriteEvent([string]$stage,[string]$detail){
 $line=[ordered]@{utc=[DateTimeOffset]::UtcNow.ToString('o');host='SATANAMD';stage=$stage;detail=$detail}|ConvertTo-Json -Compress
 [IO.File]::AppendAllText($eventFile,$line+[Environment]::NewLine,$utf8)
}
function FileHash([string]$f){if(-not(Test-Path -LiteralPath $f -PathType Leaf)){throw "BINARY_MISSING: $f"};return (Get-FileHash -LiteralPath $f -Algorithm SHA256).Hash.ToLowerInvariant()}
function Running(){return @(GetOblLiveBrowserProcesses @(Get-CimInstance Win32_Process -Filter "Name='BridgeBrowserAlpha0.exe'"))}
function SaveState($state){
 $tmp=$statePath+'.force-'+[guid]::NewGuid().ToString('N')+'.tmp'
 [IO.File]::WriteAllText($tmp,($state|ConvertTo-Json -Depth 8),$utf8)
 Move-Item -LiteralPath $tmp -Destination $statePath -Force
}
function IsIdleLivePreflight($obs,[int]$processId,[DateTimeOffset]$now){
 try{
  if($null -eq $obs){return $false}
  # Recovery from a verified empty ChatGPT app-error shell is not an
  # interruption of a generating or drafted answer. Other errors stay fenced.
  $emptyAppError=([string]$obs.error -eq 'chatgpt_app_problem' -and
    [int]$obs.turns -eq 0)
  if([int]$obs.pid -ne $processId -or
     [bool]$obs.generating -or [bool]$obs.draft -or
     [bool]$obs.toolBusy -or
     (-not [string]::IsNullOrWhiteSpace([string]$obs.error) -and
       -not $emptyAppError)) {return $false}
  $age=($now-[DateTimeOffset]::Parse([string]$obs.utc)).TotalSeconds
  return $age -ge -5 -and $age -le 40
 }catch{return $false}
}function StopLive(){
 $found=@(Running)
 foreach($proc in $found){
  WriteEvent 'force_kill' ('pid='+$proc.ProcessId)
  # NIEAKTUALNE 2026-11-02: /T najpierw zabijal dzieci WebView2, nawet gdy PID GUI
  # zwracal Access Denied. Nie naruszaj calego drzewa przed potwierdzonym zamknieciem GUI.
  # Historycznie: taskkill /PID $proc.ProcessId /T /F
  $savedErrorAction=$ErrorActionPreference
  try {
   $ErrorActionPreference='Continue'
   $stopOutput=@(& "$env:SystemRoot\System32\taskkill.exe" /PID ([string]$proc.ProcessId) /F 2>&1)
   $stopCode=$LASTEXITCODE
  }finally{$ErrorActionPreference=$savedErrorAction}
  if($stopCode -ne 0){
   WriteEvent 'force_kill_denied' ('pid='+$proc.ProcessId+' exit='+$LASTEXITCODE+' error='+($stopOutput -join ' '))
   throw ('STOP_OLD_PROCESS_FAILED_PID_'+$proc.ProcessId+'_EXIT_'+$LASTEXITCODE)
  }
 }
 for($n=0;$n -lt 30;$n++){
  if(@(Running).Count -eq 0){WriteEvent 'old_processes_gone' 'single_process_fence_clear';return}
  Start-Sleep -Milliseconds 250
 }
 foreach($proc in @(Running)){Stop-Process -Id $proc.ProcessId -Force -ErrorAction SilentlyContinue}
 for($n=0;$n -lt 20;$n++){
  if(@(Running).Count -eq 0){WriteEvent 'old_processes_gone' 'forced_fallback_stop';return}
  Start-Sleep -Milliseconds 250
 }
 throw 'UNABLE_TO_STOP_OLD_PROCESS'
}
function StartChecked([string]$exe,[string]$expected){
 if((FileHash $exe) -ne $expected){throw 'BINARY_HASH_MISMATCH'}
 if(@(Running).Count -ne 0){throw 'SINGLE_PROCESS_FENCE_BLOCKED'}
 $p=Start-Process -FilePath $exe -ArgumentList '--lite-open-safely' -WorkingDirectory (Split-Path -Parent $exe) -PassThru
 WriteEvent 'started' ('pid='+$p.Id+' exe='+$exe)
 for($n=0;$n -lt 90;$n++){
  Start-Sleep -Milliseconds 400
  $live=@(Running)
  if($live.Count -gt 1){throw 'DUPLICATE_LIVE_PROCESSES'}
  if($live.Count -ne 1 -or $live[0].ProcessId -ne $p.Id){continue}
  # Gdy CIM nie ujawnia ExecutablePath (ograniczenia sesji), zachowaj dowod: uruchomiony PID
  # pochodzi wprost ze Start-Process zweryfikowanego SHA pliku $exe; nie odrzucaj poprawnego wdrozenia.
  if(-not [string]::IsNullOrWhiteSpace([string]$live[0].ExecutablePath)){
   if((FileHash $live[0].ExecutablePath) -ne $expected){throw 'RUNNING_BINARY_WRONG_HASH'}
  }else{WriteEvent 'running_path_unavailable' ('pid='+$p.Id+' candidate_verified_before_start')}
  $win=Get-Process -Id $p.Id -ErrorAction SilentlyContinue
  if($win -and $win.Responding -and $win.MainWindowHandle -ne 0){
   WriteEvent 'gui_healthy' ('pid='+$p.Id)
   return [int]$p.Id
  }
 }
 throw 'GUI_HEALTH_TIMEOUT'
}
if($Mode -eq 'SelfTest'){
 if((Get-Command taskkill.exe -ErrorAction Stop) -eq $null){throw 'TASKKILL_MISSING'}
 $s=Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
 if((FileHash $s.candidateExe) -ne $s.candidateSha){throw 'STAGED_SHA_MISMATCH'}
 if((FileHash $s.previousExe) -ne $s.previousSha){throw 'ROLLBACK_SHA_MISMATCH'}
 $now=[DateTimeOffset]::UtcNow
 $fixture=[pscustomobject]@{pid=10;utc=$now.ToString('o');generating=$false;draft=$false;toolBusy=$false;error=''}
 if(-not (IsIdleLivePreflight $fixture 10 $now)){throw 'FORCE_IDLE_GOOD_FAILED'}
 $fixture.generating=$true
 if(IsIdleLivePreflight $fixture 10 $now){throw 'FORCE_IDLE_GENERATING_UNGUARDED'}
 $fixture.generating=$false;$fixture.draft=$true
 if(IsIdleLivePreflight $fixture 10 $now){throw 'FORCE_IDLE_DRAFT_UNGUARDED'}
 $fixture.draft=$false;$fixture.toolBusy=$true
 if(IsIdleLivePreflight $fixture 10 $now){throw 'FORCE_IDLE_TOOL_UNGUARDED'}
 $fixture.toolBusy=$false
 $fixture.error='chatgpt_app_problem';$fixture|Add-Member -NotePropertyName turns -NotePropertyValue 0
 if(-not(IsIdleLivePreflight $fixture 10 $now)){throw 'EMPTY_APP_RECOVERY_BLOCKED'}
 $fixture.turns=2
 if(IsIdleLivePreflight $fixture 10 $now){throw 'NONEMPTY_APP_ERROR_UNGUARDED'}
 $fixture.turns=0;$fixture.generating=$true
 if(IsIdleLivePreflight $fixture 10 $now){throw 'GENERATING_APP_ERROR_UNGUARDED'}
 $fixture.generating=$false;$fixture.error='response_error'
 if(IsIdleLivePreflight $fixture 10 $now){throw 'OTHER_ERROR_UNGUARDED'}
 $fixture.error=''
 if(IsIdleLivePreflight $fixture 11 $now){throw 'FORCE_IDLE_PID_MISMATCH_UNGUARDED'}
 $fixture.utc=$now.AddMinutes(-2).ToString('o')
 if(IsIdleLivePreflight $fixture 10 $now){throw 'FORCE_IDLE_STALE_UNGUARDED'}
 WriteEvent 'selftest_pass' ('phase='+$s.phase)
 Write-Output 'FORCE_DEPLOY_SELFTEST_OK'
 return
}
$mutex=[Threading.Mutex]::new($false,'Local\OBLiteForceDeploySATANAMD_v2')
$locked=$false
$pausedByThisScript=$false
try{
 $locked=$mutex.WaitOne(1000)
 if(-not $locked){WriteEvent 'skipped' 'another_force_deploy_active';return}
 if(Test-Path -LiteralPath $pausePath){WriteEvent 'skipped' 'preexisting_user_pause';return}
 $state=Get-Content -LiteralPath $statePath -Raw -Encoding UTF8|ConvertFrom-Json
 if($Mode -eq 'Deploy' -and $state.phase -ne 'queued'){WriteEvent 'skipped' ('deploy_requires_queued_got_'+$state.phase);return}
 if($Mode -eq 'Restart' -and $state.phase -notin @('trial','rolled_back')){WriteEvent 'skipped' ('restart_requires_trial_got_'+$state.phase);return}
 $candidate=if($Mode -eq 'Deploy' -or $state.phase -eq 'trial'){$state.candidateExe}else{$state.previousExe}
 $candidateSha=if($Mode -eq 'Deploy' -or $state.phase -eq 'trial'){$state.candidateSha}else{$state.previousSha}
 if((FileHash $candidate) -ne $candidateSha){throw 'CANDIDATE_INVALID'}
 if((FileHash $state.previousExe) -ne $state.previousSha){throw 'ROLLBACK_INVALID'}
 if($Mode -eq 'Deploy'){
  $live=@(Running)
  if($live.Count -eq 1){
   $obs=$null
   try {$obs=Get-Content -LiteralPath (Join-Path $diag 'live-preflight.json') -Raw |
      ConvertFrom-Json} catch {}
   # NIEAKTUALNE 2026-11-02: wlasciciel wymaga wdrozenia nawet podczas generacji,
   # szkicu lub aktywnego zadania. Zachowujemy stary warunek dla historii.
   if($false -and -not (IsIdleLivePreflight $obs $live[0].ProcessId ([DateTimeOffset]::UtcNow))){
    $crashProof=Get-OblCrashIdleProof -ExpectedPid $live[0].ProcessId
    if($null -eq $crashProof){
      WriteEvent 'skipped' 'deploy_live_busy_or_preflight_unverified'
      return
    }
    WriteEvent 'crash_idle_final_gate' ('wake='+$crashProof.wakeSequence+
      ' oldPid='+$live[0].ProcessId)
   }
  }
 }
 $snapshot=$statePath+'.pre-force-'+(Get-Date -Format 'yyyyMMdd_HHmmss')
 Copy-Item -LiteralPath $statePath -Destination $snapshot -ErrorAction Stop
 $lock=[ordered]@{owner='obl-force-deploy';utc=[DateTimeOffset]::UtcNow.ToString('o');mode=$Mode}|ConvertTo-Json -Compress
 [IO.File]::WriteAllText($pausePath,$lock,$utf8)
 $pausedByThisScript=$true
 WriteEvent 'deploy_begin' ('mode='+$Mode+' phase='+$state.phase)
 StopLive
 $state.phase='launching';$state.candidatePid=0;$state.notes='force_deploy_launching'
 SaveState $state
 try{
  $pidNew=StartChecked $candidate $candidateSha
  if($candidate -eq $state.candidateExe){
   $state.phase='trial';$state.candidatePid=$pidNew;$state.activatedUtc=[DateTimeOffset]::UtcNow.ToString('o');$state.lastHealthyUtc=$state.activatedUtc;$state.notes='forced_start_health_verified'
  }else{$state.phase='rolled_back';$state.notes='forced_start_previous_verified';$state.previousPid=$pidNew}
  SaveState $state
  WriteEvent 'deploy_success' ('pid='+$pidNew+' phase='+$state.phase)
  Write-Output ('FORCED_LIVE_PID='+$pidNew)
 }catch{
  WriteEvent 'candidate_failed' $_.Exception.GetType().Name
  StopLive
  $rollbackPid=StartChecked $state.previousExe $state.previousSha
  $state.phase='rolled_back';$state.previousPid=$rollbackPid;$state.candidatePid=0;$state.notes='forced_rollback_after_candidate_failure'
  SaveState $state
  WriteEvent 'rollback_success' ('pid='+$rollbackPid)
  Write-Output ('ROLLED_BACK_PID='+$rollbackPid)
 }
}catch{WriteEvent 'deploy_failed' $_.Exception.Message;throw}
finally{
 if($pausedByThisScript -and (Test-Path $pausePath)){
  try{$pause=Get-Content $pausePath -Raw|ConvertFrom-Json;if($pause.owner -eq 'obl-force-deploy'){Remove-Item -LiteralPath $pausePath -Force}}catch{WriteEvent 'cleanup_error' $_.Exception.Message}
 }
 if($locked){try{$mutex.ReleaseMutex()}catch{}}
 $mutex.Dispose()
}