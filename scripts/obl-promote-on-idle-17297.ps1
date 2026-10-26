$ErrorActionPreference='Stop'
$d='D:\projects\open-browser\diagnostics'
$events=Join-Path $env:LOCALAPPDATA 'OpenBrowser\ChatGPT-Lite\diagnostics\lite-events-2026-10-26.jsonl'
$stateFile=Join-Path $d 'obl-idle-promotion-state.json'
$log=Join-Path $d 'obl-idle-promotion-events.jsonl'
$taskName='OBL Promote 17297 On Idle'
$expectedOldPid=67152
$candidate='D:\Apps\OpenBrowserLite\releases\v1.72.97_20261026\BridgeBrowserAlpha0.exe'
$expectedSha='1b31e574eb3f1b36a25e0e20116863747a8022d752f4fb1158a68a4878c38124'
function Log([string]$stage,[string]$reason){
 $entry=@{utc=[DateTimeOffset]::UtcNow.ToString('o');stage=$stage;reason=$reason;oldPid=$expectedOldPid}|ConvertTo-Json -Compress
 [IO.File]::AppendAllText($log,$entry+[Environment]::NewLine,[Text.UTF8Encoding]::new($false))
}
function ResetIdle([string]$why){
 if(Test-Path $stateFile){Remove-Item $stateFile -Force}
 Log 'deferred' $why
}
try {
 $r=Get-Content (Join-Path $env:LOCALAPPDATA 'OpenBrowser\ChatGPT-Lite\diagnostics\lite-release-satanamd-state.json') -Raw | ConvertFrom-Json
 if($r.phase -ne 'queued' -or $r.previousPid -ne $expectedOldPid -or $r.candidateSha -ne $expectedSha){ResetIdle 'release_state_changed';return}
 $running=@(Get-CimInstance Win32_Process -Filter "name='BridgeBrowserAlpha0.exe'")
 if($running.Count -ne 1 -or $running[0].ProcessId -ne $expectedOldPid){ResetIdle 'live_identity_changed';return}
 & 'D:\projects\open-browser\scripts\obl-ui-preflight-session1.ps1'
 $ui=Get-Content (Join-Path $d 'obl-ui-preflight-session1.json') -Raw|ConvertFrom-Json
 if($ui.error -or $ui.pid -ne $expectedOldPid -or $ui.session -ne 1 -or $ui.count -lt 500){ResetIdle 'uia_unavailable';return}
 if(@($ui.actionButtons|Where-Object {$_.name -match 'Zatrzymaj|Stop generating|Przerwij'}).Count -gt 0){ResetIdle 'generation_stop_visible';return}
 $composer=@($ui.edits|Where-Object {$_.className -eq 'ProseMirror'})
 if($composer.Count -lt 1 -or @($composer|Where-Object {$_.emptyOrPlaceholder -ne $true}).Count -gt 0){ResetIdle 'draft_present_or_unknown';return}
 if($ui.windowTitle -match 'Generowanie|Renderer op|ChatGPT:|Blad'){ResetIdle 'title_not_ready';return}
 $samples=@(Get-Content $events -Tail 500|ConvertFrom-Json|Where-Object {$_.pid -eq $expectedOldPid})
 $generation=@($samples|Where-Object {$_.eventName -eq 'response_generation_transition'}|Select-Object -Last 1)
 if($generation.Count -ne 1 -or $generation[0].details.generating -ne $false){ResetIdle 'no_confirmed_generation_end';return}
 $lastGenUtc=[DateTimeOffset]::Parse($generation[0].utc)
 if(([DateTimeOffset]::UtcNow-$lastGenUtc).TotalSeconds -lt 50){ResetIdle 'generation_just_finished';return}
 $lastPost=@($samples|Where-Object {$_.eventName -eq 'web_backend_response' -and $_.details.method -eq 'POST'}|Select-Object -Last 1)
 if($lastPost.Count -gt 0 -and ([DateTimeOffset]::UtcNow-[DateTimeOffset]::Parse($lastPost[0].utc)).TotalSeconds -lt 50){ResetIdle 'recent_conversation_post';return}
 $now=[DateTimeOffset]::UtcNow
 if(-not(Test-Path $stateFile)){
  @{pid=$expectedOldPid;firstIdleUtc=$now.ToString('o')}|ConvertTo-Json|Set-Content $stateFile -Encoding UTF8
  Log 'eligible_first' 'second_fresh_ui_check_required';return
 }
 $prior=Get-Content $stateFile -Raw|ConvertFrom-Json
 if($prior.pid -ne $expectedOldPid -or ($now-[DateTimeOffset]::Parse($prior.firstIdleUtc)).TotalSeconds -lt 45){Log 'eligible_waiting' 'second_observation_not_due';return}
 $p=Get-Process -Id $expectedOldPid -ErrorAction Stop
 if($p.MainWindowHandle -eq 0 -or -not $p.Responding){ResetIdle 'no_interactive_window';return}
 Log 'safe_close_requested' 'ui_ready_draft_empty_two_checks'
 if(-not $p.CloseMainWindow()){Log 'close_refused' 'no_force';return}
 $p.WaitForExit(15000)|Out-Null
 if(-not $p.HasExited){Log 'close_timeout' 'no_force';return}
 Log 'clean_close_complete' 'normal_window_close'
 & 'D:\projects\open-browser\scripts\release-manager-satanamd.ps1' -Mode Keepalive | Out-Null
 Start-Sleep -Seconds 6
 $next=@(Get-CimInstance Win32_Process -Filter "name='BridgeBrowserAlpha0.exe'")
 if($next.Count -eq 1 -and (Get-FileHash $next[0].ExecutablePath -Algorithm SHA256).Hash.ToLowerInvariant() -eq $expectedSha){
  Log 'candidate_started' ('pid='+$next[0].ProcessId)
  Disable-ScheduledTask -TaskName $taskName | Out-Null
 }else{Log 'startup_unconfirmed' 'keepalive_will_apply_existing_rollback_rules'}
}catch {Log 'script_error' $_.Exception.Message}