# Open Browser Lite: durable release switch with automatic rollback via existing Keepalive.
# User-owned PBOX1. No new scheduled tasks. Does not modify WebView2 profile or DPAPI.
param(
    [ValidateSet('Audit','Promote','Watchdog','Rollback')][string]$Mode='Audit',
    [string]$CandidateExe='',
    [string]$ExpectedSha=''
)
$ErrorActionPreference='Stop'
$root=Join-Path $env:LOCALAPPDATA 'OpenBrowser\ChatGPT-Lite\diagnostics'
$stateFile=Join-Path $root 'lite-release-state.json'
$logFile=Join-Path $root 'lite-release-actions.log'
$shortcut=Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Open Browser - ChatGPT Lite.lnk'
$staged=if($CandidateExe){[IO.Path]::GetFullPath($CandidateExe)}else{Join-Path $env:LOCALAPPDATA 'Programs\OpenBrowserChatGPTLite_v1.72.58_20261022_candidate\BridgeBrowserAlpha0.exe'}
$expectedSha=if($ExpectedSha){$ExpectedSha.ToLowerInvariant()}else{'0eb1bafbf912bf89cb6c5aeee8f589f9287e22e8fed9621f1e0baca373f9952e'}
$utf8=[Text.UTF8Encoding]::new($false)
[IO.Directory]::CreateDirectory($root)|Out-Null
$mutex=[Threading.Mutex]::new($false,'Local\OBLiteReleaseGuard')
if(-not $mutex.WaitOne(2000)){Write-Output 'GUARD_BUSY';exit 4}
function Log([string]$line){
 $entry=(Get-Date).ToUniversalTime().ToString('o')+' '+$line+[Environment]::NewLine
 [IO.File]::AppendAllText($logFile,$entry,$utf8)
 Write-Output $line
}
function Save($state){
 $tmp=$stateFile+'.tmp-'+[guid]::NewGuid().ToString('N')
 [IO.File]::WriteAllText($tmp,($state|ConvertTo-Json -Depth 6),$utf8)
 Move-Item -LiteralPath $tmp -Destination $stateFile -Force
}
function Load {
 if(-not(Test-Path -LiteralPath $stateFile)){return $null}
 return (Get-Content -LiteralPath $stateFile -Raw -Encoding UTF8|ConvertFrom-Json)
}
# Only an explicitly logged normal close for the exact trial process can
# authorize ONE keepalive restart. A crash, missing log, wrong PID, changed
# executable or repeated disappearance still invokes normal rollback.
function HasRecentTrialCleanClose($s,[string]$eventsFolder){
 $pidExpected=[int]$s.candidatePid
 if($pidExpected -le 0){return $false}
 $lastHealthy=[datetime]::MinValue
 if($s.lastHealthyUtc){$lastHealthy=([datetimeoffset]::Parse([string]$s.lastHealthyUtc)).UtcDateTime}
 $now=[datetime]::UtcNow
 foreach($day in @($now.ToString('yyyy-MM-dd'),$now.AddDays(-1).ToString('yyyy-MM-dd'))){
  $file=Join-Path $eventsFolder ('lite-events-'+$day+'.jsonl')
  if(-not(Test-Path -LiteralPath $file)){continue}
  foreach($line in @(Get-Content -LiteralPath $file -Tail 250 -Encoding UTF8)){
   try{
    $e=$line|ConvertFrom-Json -ErrorAction Stop
    if($e.eventName -ne 'window_closing' -or [int]$e.pid -ne $pidExpected){continue}
    $stamp=([datetimeoffset]::Parse([string]$e.utc)).UtcDateTime
    $age=($now-$stamp).TotalSeconds
    if($age -ge 0 -and $age -le 600 -and $stamp -ge $lastHealthy){
     return $true
    }
   }catch{continue}
  }
 }
 return $false
}
function SetTarget([string]$exe){
 if(-not(Test-Path -LiteralPath $exe -PathType Leaf)){throw 'Expected executable not found'}
 $w=New-Object -ComObject WScript.Shell
 $link=$w.CreateShortcut($shortcut)
 $link.TargetPath=$exe
 $link.Arguments='--lite-open-safely'
 $link.WorkingDirectory=Split-Path -Parent $exe
 $link.Save()
 $check=$w.CreateShortcut($shortcut)
 if($check.TargetPath -ne $exe){throw 'Shortcut verification failed'}
}
function StopLite([string]$expectedPath=''){
 $found=@(Get-CimInstance Win32_Process -Filter "name='BridgeBrowserAlpha0.exe'")
 foreach($item in $found){
  if($expectedPath -and $item.ExecutablePath -ne $expectedPath){throw 'Another build is running, abort stop'}
  $p=Get-Process -Id $item.ProcessId -ErrorAction SilentlyContinue
  if(-not $p){continue}
  try{[void]$p.CloseMainWindow()}catch{}
  try{Wait-Process -Id $item.ProcessId -Timeout 12 -ErrorAction Stop}catch{
   $alive=Get-Process -Id $item.ProcessId -ErrorAction SilentlyContinue
   if($alive){Log ('force_close_only_lite_pid='+$item.ProcessId);Stop-Process -Id $item.ProcessId -Force}
  }
 }
}
function Launch([string]$exe){
 if(-not(Test-Path -LiteralPath $exe)){throw 'Launch target missing'}
 $proc=Start-Process -FilePath $exe -ArgumentList '--lite-open-safely' -WorkingDirectory (Split-Path $exe) -PassThru
 $good=$false;$title=''
 for($i=0;$i -lt 45;$i++){
  Start-Sleep -Milliseconds 500
  $p=Get-Process -Id $proc.Id -ErrorAction SilentlyContinue
  if($p -and $p.MainWindowHandle -ne 0 -and $p.Responding){
   $title=$p.MainWindowTitle
   if($title){$good=$true;break}
  }
 }
 if(-not $good){throw ('Window not healthy for PID '+$proc.Id)}
 Log ('window_responsive pid='+$proc.Id)
 return $proc.Id
}
function Rollback($state,[string]$reason){
 Log ('ROLLBACK_REASON='+$reason)
 $state.phase='rolling_back';$state.reason=$reason;$state.updatedUtc=(Get-Date).ToUniversalTime().ToString('o');Save $state
 $old=[string]$state.previousExe
 if(-not(Test-Path -LiteralPath $old)){throw 'Previous known-running EXE missing'}
 $running=@(Get-CimInstance Win32_Process -Filter "name='BridgeBrowserAlpha0.exe'")
 foreach($i in $running){if($i.ExecutablePath -ne $old){StopLite ([string]$i.ExecutablePath)}}
 SetTarget $old
 $running=@(Get-CimInstance Win32_Process -Filter "name='BridgeBrowserAlpha0.exe'"|Where-Object {$_.ExecutablePath -eq $old})
 if($running.Count -eq 0){[void](Launch $old)}
 $state.phase='rolled_back';$state.updatedUtc=(Get-Date).ToUniversalTime().ToString('o');Save $state
 Log 'ROLLBACK_SUCCESS'
}
try{
 if($Mode -eq 'Audit'){
  $s=Load
  if($s){Log ('STATE='+$s.phase+' candidate='+[IO.Path]::GetFileName((Split-Path $s.candidateExe -Parent)))}else{Log 'NO_RELEASE_STATE'}
  exit 0
 }
 if($Mode -eq 'Promote'){
  $s=Load
  if($s -and $s.phase -in @('promoting','rolling_back')){throw 'Previous release transition is unresolved'}
  # A healthy trial may become the rollback target of the next verified build.
  # Keep its state snapshot before replacing the current release transaction.
  if($s -and $s.phase -eq 'trial'){
   $prevProc=@(Get-CimInstance Win32_Process -Filter "name='BridgeBrowserAlpha0.exe'"|Where-Object {$_.ExecutablePath -eq $s.candidateExe})
   if($prevProc.Count -ne 1 -or -not (Get-Process -Id $prevProc[0].ProcessId -ErrorAction SilentlyContinue).Responding){throw 'Active trial is not healthy'}
   $archive=Join-Path $root ('lite-release-prior-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'.json')
   [IO.File]::Copy($stateFile,$archive,$false)
   Log ('PRIOR_TRIAL_SAVED='+$archive)
  }
  if(-not(Test-Path $staged)){throw 'Staged executable missing'}
  $hash=[BitConverter]::ToString([Security.Cryptography.SHA256]::Create().ComputeHash([IO.File]::ReadAllBytes($staged))).Replace('-','').ToLowerInvariant()
  if($hash -ne $expectedSha){throw 'Candidate SHA-256 mismatch'}
  $shell=New-Object -ComObject WScript.Shell
  $previous=$shell.CreateShortcut($shortcut).TargetPath
  if(-not(Test-Path $previous)){throw 'Existing installed version is missing'}
  if($previous -eq $staged){throw 'Candidate is already active'}
  $oldProc=@(Get-CimInstance Win32_Process -Filter "name='BridgeBrowserAlpha0.exe'")
  if(@($oldProc|Where-Object {$_.ExecutablePath -ne $previous}).Count){throw 'Unexpected running version'}
  $s=[pscustomobject]@{phase='promoting';candidateExe=$staged;candidateSha=$expectedSha;previousExe=$previous;startedUtc=(Get-Date).ToUniversalTime().ToString('o');updatedUtc=(Get-Date).ToUniversalTime().ToString('o');badPolls=0;reason='';lastHealthyUtc=$null;candidatePid=0;cleanCloseRestarts=0}
  Save $s;Log 'PROMOTION_STARTED_BACKUP_REGISTERED'
  try{
   StopLite $previous
   SetTarget $staged
   $s.candidatePid=[int](Launch $staged)
   $s.phase='trial';$s.updatedUtc=(Get-Date).ToUniversalTime().ToString('o');$s.lastHealthyUtc=$s.updatedUtc;Save $s
   Log 'PROMOTION_TRIAL_RUNNING'
  }catch{
   $errorText=$_.Exception.Message
   Log ('PROMOTION_FAILED='+$errorText)
   Rollback $s ('promote_failed: '+$errorText)
   throw
  }
  exit 0
 }
 $s=Load
 if(-not $s){Log 'NO_RELEASE_TO_WATCH';exit 0}
 if($Mode -eq 'Rollback'){Rollback $s 'manual_requested';exit 0}
 if($s.phase -eq 'rolled_back'){exit 0}
 if($s.phase -eq 'promoting'){
  $age=([datetime]::UtcNow-[datetime]$s.startedUtc).TotalSeconds
  if($age -gt 90){Rollback $s 'stale_promoting_state'}
  exit 0
 }
 if($s.phase -eq 'rolling_back'){Rollback $s 'resume_interrupted_rollback';exit 0}
 if($s.phase -ne 'trial'){throw 'Unexpected release state'}
 $proc=@(Get-CimInstance Win32_Process -Filter "name='BridgeBrowserAlpha0.exe'")
 $new=@($proc|Where-Object {$_.ExecutablePath -eq $s.candidateExe})
 $other=@($proc|Where-Object {$_.ExecutablePath -ne $s.candidateExe})
 if($other.Count -gt 0){Rollback $s 'unexpected_active_release';exit 0}
 if($new.Count -eq 0){
  # The existing Keepalive, invoked immediately after this release guard,
  # can reopen the same verified target. This protects an otherwise healthy
  # trial from being incorrectly rolled back after a deliberate window close.
  $cleanRestarts=[int]$s.cleanCloseRestarts
  if($cleanRestarts -eq 0 -and
     (HasRecentTrialCleanClose $s $root) -and
     (Test-Path -LiteralPath $s.candidateExe -PathType Leaf)){
    $sha=[BitConverter]::ToString([Security.Cryptography.SHA256]::Create().ComputeHash(
      [IO.File]::ReadAllBytes($s.candidateExe))).Replace('-','').ToLowerInvariant()
    $link=(New-Object -ComObject WScript.Shell).CreateShortcut($shortcut).TargetPath
    if($sha -eq [string]$s.candidateSha -and $link -eq [string]$s.candidateExe){
     $s |Add-Member -NotePropertyName cleanCloseRestarts -NotePropertyValue 1 -Force
     $s.updatedUtc=(Get-Date).ToUniversalTime().ToString('o')
     Save $s
     Log 'TRIAL_CLEAN_CLOSE_KEEPALIVE_RESTART_ONCE'
     exit 0
    }
  }
  Rollback $s 'candidate_process_missing';exit 0
 }
 $bad=@($new|Where-Object { $p=Get-Process -Id $_.ProcessId -ErrorAction SilentlyContinue; -not $p -or -not $p.Responding -or $p.MainWindowHandle -eq 0 })
 if($bad.Count){$s.badPolls=[int]$s.badPolls+1;Log ('TRIAL_UI_NOT_RESPONDING polls='+$s.badPolls)}
 else{$s.badPolls=0;$s.lastHealthyUtc=(Get-Date).ToUniversalTime().ToString('o')}
 $s.updatedUtc=(Get-Date).ToUniversalTime().ToString('o')
 Save $s
 if($s.badPolls -ge 2){Rollback $s 'two_consecutive_unresponsive_checks';exit 0}
 exit 0
}catch{
 Log ('RELEASE_MANAGER_ERROR='+$_.Exception.GetType().Name+' '+$_.Exception.Message)
 exit 2
}finally{try{$mutex.ReleaseMutex()}catch{};$mutex.Dispose()}
