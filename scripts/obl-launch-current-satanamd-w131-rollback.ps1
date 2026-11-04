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
    if($r.Count -eq 1 -and $r[0].ProcessId -eq $p.Id -and (Hash $r[0].ExecutablePath) -eq $sha){
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
  $target=if($state.phase -eq 'rolled_back' -or $state.phase -eq 'queued'){$state.previousExe}else{$state.candidateExe}
  $sha=if($state.phase -eq 'rolled_back' -or $state.phase -eq 'queued'){$state.previousSha}else{$state.candidateSha}
  $sha=([string]$sha).ToLowerInvariant()
  if((Hash $target) -ne $sha){throw ('RELEASE_SHA_MISMATCH: '+$target)}
  $running=@(Running)
  if($running.Count -eq 1 -and (Hash $running[0].ExecutablePath) -eq $sha -and
      $running[0].SessionId -eq $owner.SessionId){
    if(Test-Path -LiteralPath $pausePath){Remove-Item -LiteralPath $pausePath -Force;Log 'unpause' 'owner requires permanent autostart'}
    SyncTrialPid $state ([int]$running[0].ProcessId)
    Log 'already_current' ('pid='+$running[0].ProcessId+' exe='+$running[0].ExecutablePath)
    return
  }
  if(Test-Path -LiteralPath $pausePath){Remove-Item -LiteralPath $pausePath -Force;Log 'unpause' 'owner requires permanent autostart'}
  foreach($old in $running){
    Log 'stop_outdated' ('pid='+$old.ProcessId+' path='+$old.ExecutablePath)
    & "$env:SystemRoot\System32\taskkill.exe" /PID ([string]$old.ProcessId) /T /F |Out-Null
  }
  for($i=0;$i -lt 32 -and @(Running).Count -gt 0;$i++){Start-Sleep -Milliseconds 250}
  if(@(Running).Count -ne 0){throw 'OLD_BROWSER_STILL_RUNNING'}
  try{
    $pidNew=StartVerified $target $sha
    SyncTrialPid $state $pidNew
    Log 'success' ('version='+((Get-Item $target).VersionInfo.FileVersion)+' pid='+$pidNew)
    Write-Output ('OBL_LAUNCHED_PID='+$pidNew)
  }catch{
    $detail=$_.Exception.Message
    Log 'candidate_failed' $detail
    $fallback=[string]$state.previousExe
    $fallbackSha=([string]$state.previousSha).ToLowerInvariant()
    if($fallback -eq $target){throw}
    foreach($old in @(Running)) {& "$env:SystemRoot\System32\taskkill.exe" /PID ([string]$old.ProcessId) /T /F |Out-Null}
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
