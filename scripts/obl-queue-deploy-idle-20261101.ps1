param()
$ErrorActionPreference='Stop'
$dir='D:\projects\open-browser'
$diag=Join-Path $env:LOCALAPPDATA 'OpenBrowser\ChatGPT-Lite\diagnostics'
$log=Join-Path $diag 'obl-queue-deploy-idle-20261101.jsonl'
. (Join-Path $dir 'scripts\obl-crash-idle-proof-satanamd.ps1')
function Note([string]$status,[string]$reason) {
    $line=[ordered]@{
       utc=[DateTimeOffset]::UtcNow.ToString('o')
       stage=$status
       detail=$reason
    }|ConvertTo-Json -Compress
    [IO.File]::AppendAllText($log, $line+[Environment]::NewLine,
        [Text.UTF8Encoding]::new($false))
}
function ReadCandidate {
    $s=Get-Content (Join-Path $diag 'lite-release-satanamd-state.json') -Raw |
      ConvertFrom-Json
    if($s.phase -ne 'queued'){return $null}
    return $s
}
function CurrentIdle([int]$expectedPid) {
    $obs=Get-Content (Join-Path $diag 'live-preflight.json') -Raw | ConvertFrom-Json
    $time=[DateTimeOffset]::Parse([string]$obs.utc)
    $age=([DateTimeOffset]::UtcNow - $time).TotalSeconds
    if($age -gt 35) {
      $proof=Get-OblCrashIdleProof -ExpectedPid $expectedPid
      if($null -ne $proof){return $proof}
      return $null
    }
    if([int]$obs.pid -ne $expectedPid -or $age -lt -5 -or $age -gt 35 -or
      [bool]$obs.generating -or [bool]$obs.draft -or [bool]$obs.toolBusy -or
      -not [string]::IsNullOrWhiteSpace([string]$obs.error) -or
      -not ([string]$obs.source).StartsWith('https://chatgpt.com/c/')) {
      return $null
    }
    return $obs
}
try {
    if($env:COMPUTERNAME -ne 'SATANAMD'){Note 'skip' 'wrong_host';return}
    $state=ReadCandidate
    if($null -eq $state){Note 'skip' 'not_queued';return}
    $live=@(Get-CimInstance Win32_Process -Filter "Name='BridgeBrowserAlpha0.exe'")
    if($live.Count -ne 1){Note 'skip' ('live_count_'+$live.Count);return}
    $pidLive=[int]$live[0].ProcessId
    # P0-02 2026-11-02: deployment is immediate, regardless of generation,
    # editor draft, tool activity or imminent wake; owner's production policy.
    # Identity and release SHA remain mandatory. Historical idle code follows
    # below disabled, to retain the prior rationale without destructive deletion.
    if([int]$state.previousPid -ne $pidLive -or [int]$live[0].SessionId -ne 1){
       Note 'skip' 'previous_live_pid_or_session_changed'
       return
    }
    if([string]$live[0].ExecutablePath -ne [string]$state.previousExe){
       Note 'skip' 'previous_live_executable_mismatch'
       return
    }
    if((Get-FileHash -LiteralPath $state.previousExe -Algorithm SHA256).Hash -ne $state.previousSha){
       Note 'skip' 'previous_live_sha_mismatch'
       return
    }
    $current=ReadCandidate
    if($null -eq $current -or $current.candidateSha -ne $state.candidateSha){
       Note 'skip' 'candidate_changed'
       return
    }
    # NIEAKTUALNE 2026-11-02: two-sample idle preflight prevented rollout.
    if($false){
    $first=CurrentIdle $pidLive
    if($null -eq $first) {
       $o=Get-Content (Join-Path $diag 'live-preflight.json') -Raw | ConvertFrom-Json
       Note 'skip' ('preflight_not_idle pid='+$o.pid+' generating='+$o.generating+
         ' draft='+$o.draft+' toolBusy='+$o.toolBusy+' error='+$o.error)
       return
    }
    Start-Sleep -Seconds 22
    $second=CurrentIdle $pidLive
    if($null -eq $second -or $first.utc -eq $second.utc -or
      $first.source -ne $second.source) {
      Note 'skip' 'idle_not_stable_across_two_samples'
      return
    }
    $current=ReadCandidate
    if($null -eq $current -or $current.candidateSha -ne $state.candidateSha) {
      Note 'skip' 'candidate_changed'
      return
    }
    if($second.PSObject.Properties.Name -contains 'mode') {
        Note 'crash_idle_deploy_evidence' ('mode='+$second.mode+
            ' wake='+$second.wakeSequence+' oldPid='+$pidLive)
    }
    } # Koniec NIEAKTUALNEJ bramki idle, history retained.
    Note 'deploy_attempt' ('candidate='+$current.candidateExe)
    $messages=& (Join-Path $dir 'scripts\obl-force-deploy-satanamd.ps1') -Mode Deploy 2>&1
    $output=($messages|Out-String).Trim()
    $end=Get-Content (Join-Path $diag 'lite-release-satanamd-state.json') -Raw |
      ConvertFrom-Json
    Note 'deploy_result' ('phase='+$end.phase+' output='+$output)
    if($end.phase -eq 'trial' -and
      $end.candidateSha -eq $current.candidateSha) {
      # P0-02: retain enabled recurring task for future queued releases.
      # ReadCandidate exits immediately when no release is queued.
      Note 'task_kept_enabled' 'candidate_trial_confirmed'
      # NIEAKTUALNE 2026-11-02: one-shot self-disabling prevented later
      # releases from being deployed without manual elevation.
      if($false){
      try {
         Disable-ScheduledTask -TaskName 'OBL Queue Deploy Idle 20261101' | Out-Null
         Note 'task_disabled' 'candidate_trial_confirmed'
      }catch{
         Note 'task_disable_failed' $_.Exception.ToString()
      }
      }
    }
}catch {
    Note 'error' $_.Exception.ToString().Replace([Environment]::NewLine,' | ')
}