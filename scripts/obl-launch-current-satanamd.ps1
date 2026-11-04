param([ValidateSet('Ensure','Repair')][string]$Mode='Ensure')
$ErrorActionPreference='Stop'
if($env:COMPUTERNAME -ne 'SATANAMD'){throw 'WRONG_HOST'}
$diag=Join-Path $env:LOCALAPPDATA 'OpenBrowser\ChatGPT-Lite\diagnostics'
$statePath=Join-Path $diag 'lite-release-satanamd-state.json'
$pausePath=Join-Path $diag 'keepalive-paused.json'
$log=Join-Path $diag ('obl-launch-'+(Get-Date -Format yyyy-MM-dd)+'.log')
[IO.Directory]::CreateDirectory($diag)|Out-Null
function Log([string]$stage,[string]$detail){
  $one=($detail -replace '[\r\n]+',' ')
  [IO.File]::AppendAllText($log,((Get-Date -Format o)+' | '+$stage+' | '+$one+[Environment]::NewLine),[Text.UTF8Encoding]::new($false))
}
function Hash([string]$path){
  if(-not(Test-Path -LiteralPath $path -PathType Leaf)){throw ('BINARY_NOT_FOUND: '+$path)}
  return (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
}
function Running(){return @(Get-CimInstance Win32_Process -Filter "Name='BridgeBrowserAlpha0.exe'")}
function StopVerifiedOne($old,[string]$reason,[string]$allowedA,[string]$allowedB){
  if(-not $old){throw 'STOP_FENCE_NO_SNAPSHOT'}
  if(@(Running).Count -ne 1){throw 'STOP_FENCE_NOT_SINGLE_PROCESS'}
  if([string]$old.Name -cne 'BridgeBrowserAlpha0.exe'){throw 'STOP_FENCE_BAD_NAME'}
  if([int]$old.SessionId -ne [int]$owner.SessionId){throw 'STOP_FENCE_BAD_SESSION'}
  if([string]$old.ExecutablePath -notin @($allowedA,$allowedB)){throw ('STOP_FENCE_UNKNOWN_EXE: '+$old.ExecutablePath)}
  if([string]$old.CommandLine -match '(?i)--lite-[^\s]*selftest'){throw 'STOP_FENCE_TEST_EXE'}
  $fresh=Get-CimInstance Win32_Process -Filter ('ProcessId='+[int]$old.ProcessId) -ErrorAction Stop
  if(-not $fresh -or $fresh.Name -cne $old.Name -or $fresh.CreationDate -ne $old.CreationDate -or $fresh.ExecutablePath -ne $old.ExecutablePath -or $fresh.SessionId -ne $old.SessionId){throw 'STOP_FENCE_PID_CHANGED'}
  $handle=[Diagnostics.Process]::GetProcessById([int]$fresh.ProcessId)
  try{
    if([math]::Abs(($handle.StartTime.ToUniversalTime()-$fresh.CreationDate.ToUniversalTime()).TotalMilliseconds) -gt 1){throw 'STOP_FENCE_START_TIME_CHANGED'}
    Log 'stop_verified' ('reason='+$reason+' pid='+$fresh.ProcessId+' startUtc='+$fresh.CreationDate.ToUniversalTime().ToString('o')+' exe='+$fresh.ExecutablePath)
    $handle.Kill()
    if(-not $handle.WaitForExit(8000)){throw ('STOP_FENCE_EXIT_TIMEOUT_PID='+$fresh.ProcessId)}
  }finally{$handle.Dispose()}
}
function SyncTrialPid($state,[int]$pidNow){
  if($state.phase -ne 'trial' -or [int]$state.candidatePid -eq $pidNow){return}
  $state.candidatePid=$pidNow
  $state.lastHealthyUtc=[DateTimeOffset]::UtcNow.ToString('o')
  $state.notes='launcher_verified_current_pid'
  $temp=$statePath+'.syncpid-'+[guid]::NewGuid().ToString('N')+'.tmp'
  [IO.File]::WriteAllText($temp,($state|ConvertTo-Json -Depth 10),[Text.UTF8Encoding]::new($false))
  Move-Item -LiteralPath $temp -Destination $statePath -Force
  Log 'state_synced' ('candidatePid='+$pidNow)
}
function StartVerified([string]$exe,[string]$sha){
  if((Hash $exe) -ne $sha){throw ('INVALID_BINARY_SHA: '+$exe)}
  if(@(Running).Count -ne 0){throw 'PROCESS_FENCE_NOT_CLEAR'}
  $p=Start-Process -FilePath $exe -ArgumentList '--lite-open-safely' -WorkingDirectory (Split-Path -Parent $exe) -PassThru
  Log 'start' ('pid='+$p.Id+' exe='+$exe)
  for($i=0;$i -lt 75;$i++){
    Start-Sleep -Milliseconds 400
    $r=@(Running)
    if($r.Count -gt 1){throw 'DUPLICATE_BROWSER_PROCESSES'}
    if($r.Count -eq 1 -and $r[0].ProcessId -eq $p.Id -and
       [int]$r[0].SessionId -eq [int]$owner.SessionId){
      # If CIM hides ExecutablePath, verify the exact file passed to Start-Process
      # and require the newly returned PID, session and healthy GUI before trial.
      $observedPath=[string]$r[0].ExecutablePath
      if(-not [string]::IsNullOrWhiteSpace($observedPath)){
        if((Hash $observedPath) -ne $sha){throw 'RUNNING_BINARY_SHA_MISMATCH'}
      }else{
        if((Hash $exe) -ne $sha){throw 'STARTED_BINARY_CHANGED'}
      }
      $w=Get-Process -Id $p.Id -ErrorAction SilentlyContinue
      if($w -and $w.Responding -and $w.MainWindowHandle -ne 0){Log 'healthy' ('pid='+$p.Id);return [int]$p.Id}
    }
  }
  throw ('GUI_START_TIMEOUT: '+$exe)
}
$owner=Get-CimInstance Win32_Process -Filter "ProcessId=$PID"
if(-not $owner -or $owner.SessionId -eq 0){Log 'refused' 'NON_INTERACTIVE_SESSION';throw 'NON_INTERACTIVE_SESSION: use interactive Windows Scheduled Task'}
$mutex=[Threading.Mutex]::new($false,'Local\OBLiteCanonicalLauncherSATANAMD')
$locked=$false
try{
  $locked=$mutex.WaitOne(5000)
  if(-not $locked){Log 'skipped' 'launcher_already_running';return}
  if(-not(Test-Path -LiteralPath $statePath)){throw 'RELEASE_STATE_MISSING'}
  $state=Get-Content -LiteralPath $statePath -Raw -Encoding UTF8|ConvertFrom-Json
  if($state.phase -notin @('trial','rolled_back','queued')){throw ('UNSUPPORTED_RELEASE_PHASE: '+$state.phase)}
  $running=@(Running)
  # P0-02 (2026-11-02): queued must deploy the candidate on a clean start.
  # Historical behavior always launched previousExe, stranding updates after reboot.
  $queuedColdStart=($state.phase -eq 'queued' -and $running.Count -eq 0)
  $usePrevious=($state.phase -eq 'rolled_back' -or
    ($state.phase -eq 'queued' -and -not $queuedColdStart))
  $target=if($usePrevious){$state.previousExe}else{$state.candidateExe}
  $sha=if($usePrevious){$state.previousSha}else{$state.candidateSha}
  $sha=([string]$sha).ToLowerInvariant()
  if((Hash $target) -ne $sha){throw ('RELEASE_SHA_MISMATCH: '+$target)}
  if($queuedColdStart -and (Hash $state.previousExe) -ne ([string]$state.previousSha).ToLowerInvariant()){
    throw 'ROLLBACK_SHA_MISMATCH'
  }
  if($state.phase -eq 'queued' -and $running.Count -eq 1 -and
      [int]$running[0].ProcessId -eq [int]$state.previousPid -and
      [int]$running[0].SessionId -eq [int]$owner.SessionId){
    # A running predecessor is handled by the authorized deploy task.
    # No restart of the OLD image or kill from the login launcher.
    Log 'queued_existing_live' ('pid='+$running[0].ProcessId)
    return
  }
  if($running.Count -eq 1 -and $running[0].ExecutablePath -eq $target -and
      ([string]$running[0].CommandLine -notmatch '(?i)--lite-[^\s]*selftest') -and
      (Hash $running[0].ExecutablePath) -eq $sha -and
      $running[0].SessionId -eq $owner.SessionId){
    if(Test-Path -LiteralPath $pausePath){Remove-Item -LiteralPath $pausePath -Force;Log 'unpause' 'owner requires permanent autostart'}
    SyncTrialPid $state ([int]$running[0].ProcessId)
    Log 'already_current' ('pid='+$running[0].ProcessId+' exe='+$running[0].ExecutablePath)
    return
  }
  if(Test-Path -LiteralPath $pausePath){Remove-Item -LiteralPath $pausePath -Force;Log 'unpause' 'owner requires permanent autostart'}
  if($running.Count -gt 1){throw 'STOP_FENCE_NOT_SINGLE_PROCESS'}
  if($running.Count -eq 1){
    StopVerifiedOne $running[0] 'outdated_release' ([string]$state.candidateExe) ([string]$state.previousExe)
  }
  for($i=0;$i -lt 32 -and @(Running).Count -gt 0;$i++){Start-Sleep -Milliseconds 250}
  if(@(Running).Count -ne 0){throw 'OLD_BROWSER_STILL_RUNNING'}
  try{
    $pidNew=StartVerified $target $sha
    if($queuedColdStart){
      # Promote only after the candidate SHA, one PID and responsive GUI passed.
      $state.phase='trial'
      $state.candidatePid=$pidNew
      $state.activatedUtc=[DateTimeOffset]::UtcNow.ToString('o')
      $state.lastHealthyUtc=$state.activatedUtc
      $state.notes='launcher_queued_cold_start_verified'
      $temp=$statePath+'.coldstart-'+[guid]::NewGuid().ToString('N')+'.tmp'
      [IO.File]::WriteAllText($temp,($state|ConvertTo-Json -Depth 10),[Text.UTF8Encoding]::new($false))
      Move-Item -LiteralPath $temp -Destination $statePath -Force
      Log 'queued_cold_start_promoted' ('pid='+$pidNew+' sha='+$sha)
    }else{SyncTrialPid $state $pidNew}
    Log 'success' ('version='+((Get-Item $target).VersionInfo.FileVersion)+' pid='+$pidNew)
    Write-Output ('OBL_LAUNCHED_PID='+$pidNew)
  }catch{
    $detail=$_.Exception.Message
    Log 'candidate_failed' $detail
    $fallback=[string]$state.previousExe
    $fallbackSha=([string]$state.previousSha).ToLowerInvariant()
    if($fallback -eq $target){throw}
    $cleanup=@(Running)
    if($cleanup.Count -gt 1){throw 'ROLLBACK_STOP_FENCE_NOT_SINGLE_PROCESS'}
    if($cleanup.Count -eq 1){
      StopVerifiedOne $cleanup[0] 'rollback_after_candidate_failure' ([string]$state.candidateExe) ([string]$state.previousExe)
    }
    for($i=0;$i -lt 32 -and @(Running).Count -gt 0;$i++){Start-Sleep -Milliseconds 250}
    $fallbackPid=StartVerified $fallback $fallbackSha
    $state.phase='rolled_back';$state.previousPid=$fallbackPid
    $state.notes='launcher_rollback_after_'+$detail
    $temp=$statePath+'.launcher-'+[guid]::NewGuid().ToString('N')+'.tmp'
    [IO.File]::WriteAllText($temp,($state|ConvertTo-Json -Depth 10),[Text.UTF8Encoding]::new($false))
    Move-Item -LiteralPath $temp -Destination $statePath -Force
    Log 'rollback_success' ('pid='+$fallbackPid+' exe='+$fallback)
    Write-Output ('OBL_ROLLBACK_PID='+$fallbackPid)
  }
}catch{
  Log 'error' ($_.Exception.GetType().Name+': '+$_.Exception.Message)
  throw
}finally{
  if($locked){try{$mutex.ReleaseMutex()}catch{}}
  $mutex.Dispose()
}
