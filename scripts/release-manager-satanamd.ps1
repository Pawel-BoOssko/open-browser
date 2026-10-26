# SATANAMD Open Browser Lite release gate. Runs from existing Keepalive only.
# Never kills a browser, never sends a ChatGPT message, no native ChatGPT tasks.
param(
 [ValidateSet('SelfTest','Audit','Queue','Keepalive','Rollback','Reconcile')][string]$Mode='Audit',
 [string]$CandidateExe='',
 [string]$ExpectedSha=''
)
$ErrorActionPreference='Stop'
$root='D:\Apps\OpenBrowserLite'
$legacy=Join-Path $root 'releases\v1.72.89_20261024\BridgeBrowserAlpha0.exe'
$diag=Join-Path $env:LOCALAPPDATA 'OpenBrowser\ChatGPT-Lite\diagnostics'
$statePath=Join-Path $diag 'lite-release-satanamd-state.json'
$statusPath=Join-Path $diag 'lite-keepalive-status.json'
$events=Join-Path $diag ('lite-events-'+(Get-Date -Format 'yyyy-MM-dd')+'.jsonl')
$pause=Join-Path $diag 'keepalive-paused.json'
$utf8=[Text.UTF8Encoding]::new($false)
function HashFile([string]$path) {
 $s=[Security.Cryptography.SHA256]::Create()
 try { $stream=[IO.File]::OpenRead($path); try { return [BitConverter]::ToString($s.ComputeHash($stream)).Replace('-','').ToLowerInvariant() } finally { $stream.Dispose() } }
 finally { $s.Dispose() }
}
function ReadState {
 if(-not (Test-Path -LiteralPath $statePath)){return $null}
 return (Get-Content -LiteralPath $statePath -Raw -Encoding UTF8|ConvertFrom-Json)
}
function WriteState($obj) {
 $tmp=$statePath+'.'+[guid]::NewGuid().ToString('N')+'.tmp'
 [IO.File]::WriteAllText($tmp,($obj|ConvertTo-Json -Depth 8),$utf8)
 Move-Item -LiteralPath $tmp -Destination $statePath -Force
}
function Report([string]$state,[string]$details) {
 $obj=[ordered]@{checkedAt=(Get-Date).ToString('o');state=$state;details=$details;host='SATANAMD'}
 $tmp=$statusPath+'.'+[guid]::NewGuid().ToString('N')+'.tmp'
 [IO.File]::WriteAllText($tmp,($obj|ConvertTo-Json -Compress),$utf8)
 Move-Item -LiteralPath $tmp -Destination $statusPath -Force
 Write-Output ($state+' '+$details)
}
function AuditEvent([string]$action,$metadata) {
 $obj=[ordered]@{utc=[DateTimeOffset]::UtcNow.ToString('o');action=$action;metadata=$metadata}
 [IO.File]::AppendAllText((Join-Path $diag 'lite-release-satanamd-events.jsonl'),($obj|ConvertTo-Json -Compress -Depth 5)+[Environment]::NewLine,$utf8)
}
function Running {
 return @(Get-CimInstance Win32_Process -Filter "name='BridgeBrowserAlpha0.exe'")
}
function HadCleanClose([int]$expectedPid,[string]$afterUtc) {
 if($expectedPid -le 0){return $false}
 $threshold=[datetimeoffset]::Parse($afterUtc).ToUniversalTime()
 # Keepalive may first resume just after midnight. Include yesterday's
 # daily event file; never trust an event from another PID or before queue.
 $candidates=@($events)
 $today='lite-events-'+(Get-Date -Format 'yyyy-MM-dd')+'.jsonl'
 if([IO.Path]::GetFileName($events) -eq $today){
  $yesterday='lite-events-'+(Get-Date).AddDays(-1).ToString('yyyy-MM-dd')+'.jsonl'
  $candidates+=Join-Path (Split-Path -Parent $events) $yesterday
 }
 foreach($path in $candidates){
  if(-not(Test-Path -LiteralPath $path -PathType Leaf)){continue}
  foreach($line in @(Get-Content -LiteralPath $path -Tail 500 -Encoding UTF8)){
   try{
    $item=$line|ConvertFrom-Json
    if($item.eventName -ne 'window_closing' -or [int]$item.pid -ne $expectedPid){continue}
    $when=[datetimeoffset]::Parse([string]$item.utc).ToUniversalTime()
    if($when -ge $threshold -and $when -le [DateTimeOffset]::UtcNow){return $true}
   }catch{}
  }
 }
 return $false
}
function StartSafe([string]$file) {
 if(-not(Test-Path -LiteralPath $file -PathType Leaf)){throw 'Startup executable absent'}
 if(@(Running).Count -ne 0){throw 'Startup fenced: browser exists'}
 $proc=Start-Process -FilePath $file -ArgumentList '--lite-open-safely' -WorkingDirectory (Split-Path -Parent $file) -PassThru
 # An unverified process is NOT killed. Report and allow monitoring after startup.
 for($i=0;$i -lt 18;$i++){
  Start-Sleep -Milliseconds 350
  $p=Get-Process -Id $proc.Id -ErrorAction SilentlyContinue
  if($p -and $p.Responding -and $p.MainWindowHandle -ne 0){return [int]$proc.Id}
 }
 return [int]$proc.Id
}
function CanQueue([int]$count,[bool]$same,[bool]$hashOk) {
 return $count -eq 1 -and $same -and $hashOk
}
function PreviousForQueue($state,[string]$defaultExe) {
 if($null -eq $state){return $defaultExe}
 if($state.phase -eq 'trial'){return [string]$state.candidateExe}
 if($state.phase -in @('queued','rolled_back')){return [string]$state.previousExe}
 return $defaultExe
}
function CanSupersede([string]$phase,[int]$currentPid,[int]$previousPid,[bool]$matchingPreviousExe) {
 # Changing the queued candidate is safe only while the ORIGINAL browser
 # with the ORIGINAL PID still runs. Never overwrite an in-progress launch.
 return $phase -eq 'queued' -and $currentPid -gt 0 -and
   $currentPid -eq $previousPid -and $matchingPreviousExe
}
[IO.Directory]::CreateDirectory($diag)|Out-Null
if($Mode -eq 'SelfTest'){
 if(-not(CanQueue 1 $true $true) -or (CanQueue 0 $true $true) -or
    (CanQueue 2 $true $true) -or (CanQueue 1 $false $true) -or
    (CanQueue 1 $true $false)){throw 'Queue predicate selftest failed'}
 if(-not(CanSupersede 'queued' 4321 4321 $true) -or
    (CanSupersede 'queued' 4322 4321 $true) -or
    (CanSupersede 'trial' 4321 4321 $true) -or
    (CanSupersede 'queued' 4321 4321 $false)){
   throw 'Queued replacement invariant selftest failed'
 }
 $fixture=[pscustomobject]@{phase='rolled_back';previousExe='v1.72.92';candidateExe='v1.72.93'}
 if((PreviousForQueue $null 'baseline') -ne 'baseline' -or
    (PreviousForQueue $fixture 'baseline') -ne 'v1.72.92'){throw 'Rolled back predecessor selftest failed'}
 $fixture.phase='trial'
 if((PreviousForQueue $fixture 'baseline') -ne 'v1.72.93'){throw 'Trial predecessor selftest failed'}
 $fixture.phase='queued'
 if((PreviousForQueue $fixture 'baseline') -ne 'v1.72.92'){throw 'Queued predecessor selftest failed'}
 $before=[DateTimeOffset]::UtcNow.AddSeconds(-2)
 $tmp=Join-Path $diag ('obl-close-fixture-'+[guid]::NewGuid().ToString('N')+'.jsonl')
 $realEvents=$events
 try{
  $events=$tmp
  $fake=[ordered]@{utc=[DateTimeOffset]::UtcNow.AddSeconds(-1).ToString('o');pid=54321;eventName='window_closing'}|ConvertTo-Json -Compress
  [IO.File]::WriteAllText($tmp,$fake+[Environment]::NewLine,$utf8)
  if(-not(HadCleanClose 54321 $before.ToString('o'))){throw 'Expected clean close missing'}
  if(HadCleanClose 54322 $before.ToString('o')){throw 'Wrong PID clean close accepted'}
  if(HadCleanClose 54321 ([DateTimeOffset]::UtcNow.AddMinutes(1).ToString('o'))){throw 'Stale clean close accepted'}
 }finally{
  $events=$realEvents
  if(Test-Path $tmp){Remove-Item -LiteralPath $tmp -Force}
 }
 # Midnight rollover fixture in an isolated folder, not production journal.
 $tempDir=Join-Path $diag ('release-selftest-'+[guid]::NewGuid().ToString('N'))
 [IO.Directory]::CreateDirectory($tempDir)|Out-Null
 try{
  $events=Join-Path $tempDir ('lite-events-'+(Get-Date -Format 'yyyy-MM-dd')+'.jsonl')
  $prior=Join-Path $tempDir ('lite-events-'+(Get-Date).AddDays(-1).ToString('yyyy-MM-dd')+'.jsonl')
  $fake=[ordered]@{utc=[DateTimeOffset]::UtcNow.AddHours(-23).ToString('o');pid=54321;eventName='window_closing'}|ConvertTo-Json -Compress
  [IO.File]::WriteAllText($prior,$fake+[Environment]::NewLine,$utf8)
  if(-not(HadCleanClose 54321 ([DateTimeOffset]::UtcNow.AddHours(-24).ToString('o')))){throw 'Yesterday clean close missing'}
  if(HadCleanClose 54321 ([DateTimeOffset]::UtcNow.AddHours(-22).ToString('o'))){throw 'Old close bypassed queuedAtUtc'}
 }finally{
  $events=$realEvents
  if(Test-Path $tempDir){Remove-Item -LiteralPath $tempDir -Recurse -Force}
 }
 Write-Output 'SATANAMD_RELEASE_SELFTEST_PASS_18'
 return
}
$mutex=[Threading.Mutex]::new($false,'Local\OBLiteSatanReleaseGuard_v1')
$locked=$false
try{
 $locked=$mutex.WaitOne(2000)
 if(-not $locked){Write-Output 'RELEASE_GUARD_BUSY';return}
 $state=ReadState
 if($Mode -eq 'Reconcile'){
  if(-not $state -or $state.phase -ne 'queued'){throw 'Reconcile requires a queued release'}
  $ps=@(Running)
  if($ps.Count -ne 1){throw 'Reconcile requires one live process'}
  $live=$ps[0]
  $running=Get-Process -Id $live.ProcessId -ErrorAction Stop
  if(-not $running.Responding){throw 'Live process not responding'}
  $heartbeatOk=$false
  foreach($line in @(Get-Content -LiteralPath $events -Tail 100 -Encoding UTF8)){
   try{$x=$line | ConvertFrom-Json;if($x.eventName -eq 'ui_heartbeat' -and [int]$x.pid -eq [int]$live.ProcessId -and ([DateTimeOffset]::UtcNow-[DateTimeOffset]::Parse($x.utc)).TotalSeconds -lt 120){$heartbeatOk=$true;break}}catch{}
  }
  if(-not $heartbeatOk){throw 'No recent WebView2 UI heartbeat'}
  if((HashFile $state.candidateExe) -ne $state.candidateSha -or
     (HashFile $live.ExecutablePath) -ne $state.candidateSha){throw 'Candidate and LIVE hashes differ'}
  Copy-Item -LiteralPath $statePath -Destination ($statePath+'.backup-'+(Get-Date -Format 'yyyyMMdd_HHmmss')+'-'+[guid]::NewGuid().ToString('N')) -ErrorAction Stop
  $state.phase='trial';$state.candidatePid=[int]$live.ProcessId
  $state.activatedUtc=[DateTimeOffset]::UtcNow.ToString('o')
  $state.lastHealthyUtc=$state.activatedUtc
  $state.notes='live_equivalent_sha_reconciled'
  WriteState $state
  AuditEvent 'live_sha_reconciled' @{pid=$live.ProcessId;candidateSha=$state.candidateSha}
  Write-Output ('RECONCILED_TRIAL_PID='+$live.ProcessId)
  return
 } if($Mode -eq 'Audit'){
  $ps=@(Running)
  Write-Output ('STATE='+$(if($state){$state.phase}else{'none'})+' RUNNING='+$ps.Count)
  if($state){Write-Output ('CANDIDATE='+$state.candidateExe+' PREVIOUS='+$state.previousExe)}
  return
 }
 if($Mode -eq 'Queue'){
  if(-not $CandidateExe -or -not $ExpectedSha){throw 'Explicit EXE and SHA required'}
  $candidate=[IO.Path]::GetFullPath($CandidateExe)
  if(-not $candidate.StartsWith((Join-Path $root 'releases')+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Candidate outside releases'}
  if(-not(Test-Path -LiteralPath $candidate -PathType Leaf)){throw 'Candidate missing'}
  $actual=HashFile $candidate
  if($actual -ne $ExpectedSha.ToLowerInvariant()){throw 'Candidate hash mismatch'}
  if($state -and $state.phase -eq 'queued' -and $state.candidateExe -eq $candidate -and $state.candidateSha -eq $actual){
   Write-Output 'ALREADY_QUEUED';return
  }
  if($state -and $state.phase -notin @('queued','trial','rolled_back','cancelled')){throw 'Unresolved previous release state'}
  $processes=@(Running)
  # After a rollback, previousExe is the last ACTUALLY RUNNING version.
  # Do not fall back to the hardcoded 1.72.89 when previous was 1.72.92.
  $previous=PreviousForQueue $state $legacy
  if(-not(Test-Path -LiteralPath $previous)){throw 'Previous binary absent'}
  if(-not(CanQueue $processes.Count ($processes.Count -eq 1 -and (HashFile $processes[0].ExecutablePath) -eq (HashFile $previous)) $true)){throw 'Exactly one expected live browser required'}
  if($state -and $state.phase -eq 'queued'){
   if(-not(CanSupersede $state.phase ([int]$processes[0].ProcessId) ([int]$state.previousPid) ((HashFile $processes[0].ExecutablePath) -eq $state.previousSha))){
    throw 'Queued predecessor identity changed; no replacement allowed'
   }
   if((HashFile $state.previousExe) -ne $state.previousSha){throw 'Queued rollback binary changed'}
  }
  if($candidate -eq $previous){throw 'Candidate equals previous'}
  $s=[pscustomobject]@{
   schema=1;phase='queued';queuedAtUtc=[DateTimeOffset]::UtcNow.ToString('o');
   previousExe=$previous;previousSha=(HashFile $previous);previousPid=[int]$processes[0].ProcessId;
   candidateExe=$candidate;candidateSha=$actual;candidatePid=0;
   activatedUtc=''; lastHealthyUtc=''; notes='not_activated';
  }
  if($state){
   Copy-Item -LiteralPath $statePath -Destination ($statePath+'.backup-'+(Get-Date -Format 'yyyyMMdd_HHmmss')+'-'+[guid]::NewGuid().ToString('N')) -ErrorAction Stop
  }
  WriteState $s
  AuditEvent 'release_queued' @{previousPid=$s.previousPid;candidateSha=$actual}
  Write-Output 'QUEUED_CURRENT_PROCESS_UNTOUCHED'
  return
 }
 if($Mode -eq 'Rollback'){
  if(-not $state){throw 'No recorded release'}
  if($state.phase -notin @('queued','trial')){throw 'Cannot rollback this phase'}
  $state.phase='rollback_requested'
  WriteState $state
  AuditEvent 'rollback_requested' @{previousPid=$state.previousPid}
  Write-Output 'ROLLBACK_ON_NEXT_SAFE_EXIT'
  return
 }
 # Keepalive mode. Runs from the existing Windows scheduler, never creates a task.
 if(Test-Path -LiteralPath $pause){Report 'paused_by_user' 'No launch while paused';return}
 $processes=@(Running)
 if($processes.Count -gt 1){Report 'blocked_duplicate_processes' ('count='+$processes.Count);return}
 if($processes.Count -eq 1){
  if($state -and $state.phase -eq 'launching'){
   if($processes[0].ExecutablePath -ne $state.candidateExe){
    Report 'blocked_launching_wrong_exe' 'Candidate identity mismatch';return
   }
   $check=Get-Process -Id $processes[0].ProcessId -ErrorAction SilentlyContinue
   if(-not $check -or -not $check.Responding -or $check.MainWindowHandle -eq 0){
    Report 'startup_unverified_no_force' 'Retain sole candidate; never start second';return
   }
   $state.phase='trial'
   $state.candidatePid=[int]$check.Id
   $state.activatedUtc=[DateTimeOffset]::UtcNow.ToString('o')
   $state.lastHealthyUtc=$state.activatedUtc
   WriteState $state
   AuditEvent 'candidate_trial_confirmed_late' @{pid=$check.Id}
  }
  if($state -and $state.phase -eq 'trial'){
   if((HashFile $processes[0].ExecutablePath) -ne $state.candidateSha){Report 'blocked_wrong_release' 'Trial binary hash mismatch';return}
   if([int]$processes[0].ProcessId -ne [int]$state.candidatePid){Report 'blocked_pid_mismatch' 'Trial identity changed';return}
   $p=Get-Process -Id $processes[0].ProcessId -ErrorAction SilentlyContinue
   if(-not $p.Responding){Report 'trial_unresponsive_no_force' 'Manual diagnosis needed';return}
   $state.lastHealthyUtc=[DateTimeOffset]::UtcNow.ToString('o')
   WriteState $state
  }
  Report 'running' ('existingProcesses=1 pid='+$processes[0].ProcessId)
  return
 }
 $target=$legacy
 if($state) {
  $prevOk=(Test-Path $state.previousExe -PathType Leaf) -and
   (HashFile $state.previousExe) -eq $state.previousSha
  if(-not $prevOk){Report 'blocked_previous_hash' 'Rollback binary missing or altered';return}
  if($state.phase -eq 'queued'){
   $candOk=(Test-Path $state.candidateExe -PathType Leaf) -and
    (HashFile $state.candidateExe) -eq $state.candidateSha
   if($candOk -and (HadCleanClose ([int]$state.previousPid) ([string]$state.queuedAtUtc))){
    $target=$state.candidateExe
    $state.phase='launching'
    WriteState $state
    AuditEvent 'clean_close_gate_pass' @{previousPid=$state.previousPid}
   }else{
    $state.phase='rolled_back';$state.notes='no_clean_close_or_candidate_hash'
    WriteState $state
    AuditEvent 'safe_rollback_before_launch' @{reason=$state.notes}
    $target=$state.previousExe
   }
  }elseif($state.phase -eq 'trial'){
   # Do not automatically retry a crashed candidate. A clean normal exit is
   # treated as normal Keepalive restart, unless owner has set pause marker.
   if(HadCleanClose ([int]$state.candidatePid) ([string]$state.activatedUtc)){
    $target=$state.candidateExe
    $state.phase='launching'
    WriteState $state
   }else{
    $state.phase='rolled_back';$state.notes='candidate_missing_without_clean_close'
    WriteState $state
    AuditEvent 'automatic_rollback' @{reason=$state.notes}
    $target=$state.previousExe
   }
  }elseif($state.phase -eq 'rollback_requested' -or $state.phase -eq 'rolled_back'){
   $target=$state.previousExe
  }elseif($state.phase -eq 'launching'){
   # A crash during first launch is treated as an unconfirmed upgrade.
   $state.phase='rolled_back';$state.notes='candidate_missing_after_launch'
   WriteState $state
   $target=$state.previousExe
  }else{Report 'blocked_unknown_release_phase' $state.phase;return}
 }
 $startedPid=StartSafe $target
 $ps=@(Running)
 if($ps.Count -ne 1 -or $ps[0].ExecutablePath -ne $target){
  Report 'blocked_startup_not_verified' ('pid='+$startedPid)
  return
 }
 $healthy=Get-Process -Id $startedPid -ErrorAction SilentlyContinue
 if(-not $healthy -or -not $healthy.Responding -or $healthy.MainWindowHandle -eq 0){
  # Leave the launch phase durable. A later successful health check may
  # transition to trial; a vanished candidate rolls back on the next check.
  Report 'startup_unverified_no_force' ('pid='+$startedPid)
  return
 }
 if($state -and $target -eq $state.candidateExe){
  $state.phase='trial';$state.candidatePid=$startedPid
  $state.activatedUtc=[DateTimeOffset]::UtcNow.ToString('o')
  $state.lastHealthyUtc=$state.activatedUtc
  WriteState $state
  AuditEvent 'candidate_trial_started' @{pid=$startedPid;sha=$state.candidateSha}
 }else{
  AuditEvent 'previous_binary_started' @{pid=$startedPid}
 }
 Report 'launched' ('pid='+$startedPid+' release='+[IO.Path]::GetFileName((Split-Path $target -Parent)))
}catch{
 try{Report 'error' $_.Exception.GetType().Name}catch{}
 throw
}finally{
 if($locked){try{$mutex.ReleaseMutex()}catch{}}
 $mutex.Dispose()
}
