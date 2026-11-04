param([Parameter(Mandatory=$true)][int]$Sequence,[Parameter(Mandatory=$true)][string]$Receipt)
$ErrorActionPreference = 'Stop'
if($env:COMPUTERNAME -ne 'SATANAMD'){throw 'WRONG_HOST'}
$base = Join-Path $env:LOCALAPPDATA 'OpenBrowser\ChatGPT-Lite'
$diag = Join-Path $base 'diagnostics'
$project = @( (Get-Content (Join-Path $base 'projects.json') -Raw | ConvertFrom-Json).Projects | Where-Object Key -eq 'obl' )[0]
$release = Get-Content (Join-Path $diag 'lite-release-satanamd-state.json') -Raw | ConvertFrom-Json
$procs = @(Get-CimInstance Win32_Process -Filter "name='BridgeBrowserAlpha0.exe'")
$live = if($procs.Count -eq 1){$procs[0]}else{$null}
$journal = @(foreach($line in [IO.File]::ReadLines((Join-Path $diag 'wake-lifecycle.jsonl'))){try{$e=ConvertFrom-Json -InputObject $line; if([int]$e.wakeSequence -eq $Sequence -and $e.receipt -eq $Receipt){$e}}catch{}})
$engine = @($journal | Where-Object actor -eq 'engine')
$reserved = @($engine | Where-Object stage -eq 'attempt_reserved')
$click = @($engine | Where-Object {$_.stage -eq 'submission_attempted' -and $_.result -eq 'click_invoked'})
$ack = @($engine | Where-Object {$_.stage -eq 'receipt_confirmed' -and $_.result -eq 'visible_ack'})
$started = @($journal | Where-Object {$_.actor -eq 'chat' -and $_.stage -eq 'chat_started'})
$preflight = Get-Content (Join-Path $diag 'live-preflight.json') -Raw | ConvertFrom-Json
$preflightAgeSeconds = ([DateTimeOffset]::UtcNow - [DateTimeOffset]::Parse([string]$preflight.utc)).TotalSeconds
$binaryOk = $false
$expectedSha = switch ([string]$release.phase) {
  'queued' { $release.previousSha }
  'trial' { $release.candidateSha }
  'rolled_back' { $release.previousSha }
  default { '' }
}
$expectedPid = switch ([string]$release.phase) {
  'queued' { $release.previousPid }
  'trial' { $release.candidatePid }
  'rolled_back' { $release.previousPid }
  default { 0 }
}
if($null -ne $live -and $expectedSha){
  $binaryOk = ((Get-FileHash -LiteralPath $live.ExecutablePath -Algorithm SHA256).Hash -eq $expectedSha) -and
    ([int]$expectedPid -eq [int]$live.ProcessId)
}
if($release.phase -eq 'queued'){
  $binaryOk = $binaryOk -and (Test-Path -LiteralPath $release.candidateExe) -and
    ((Get-FileHash -LiteralPath $release.candidateExe -Algorithm SHA256).Hash -eq $release.candidateSha)
}
$rollbackOk = (Test-Path -LiteralPath $release.previousExe) -and (Get-FileHash -LiteralPath $release.previousExe -Algorithm SHA256).Hash -eq $release.previousSha
$leadCount = @($project.Chats | Where-Object Role -eq 'lead').Count
$eventPath = Join-Path $diag ('lite-events-'+(Get-Date -Format 'yyyy-MM-dd')+'.jsonl')
$recentEvents = @(if(Test-Path -LiteralPath $eventPath){foreach($line in (Get-Content -LiteralPath $eventPath -Tail 200)){try{ConvertFrom-Json -InputObject $line}catch{}}})
$hb = @($recentEvents | Where-Object { $_.eventName -eq 'ui_heartbeat' -and $_.pid -eq $live.ProcessId } | Select-Object -Last 1)
$dom = @($recentEvents | Where-Object { $_.eventName -eq 'hard_dom_health' -and $_.pid -eq $live.ProcessId } | Select-Object -Last 1)
$hbAge = if($hb.Count -gt 0){([DateTimeOffset]::UtcNow - [DateTimeOffset]::Parse([string]$hb[0].utc)).TotalSeconds}else{1e9}
$domAge = if($dom.Count -gt 0){([DateTimeOffset]::UtcNow - [DateTimeOffset]::Parse([string]$dom[0].utc)).TotalSeconds}else{1e9}
$checks = [ordered]@{
  wakeReservedOnce=($reserved.Count -eq 1)
  wakeClickOnce=($click.Count -eq 1)
  wakeVisibleAckOnce=($ack.Count -eq 1)
  chatStarted=($started.Count -eq 1)
  schedulerEnabled=([bool]$project.Wake.Enabled)
  scheduler15min=([int]$project.Wake.FastMinutes -eq 15)
  schedulerSequenceAtLeast=([int]$project.Wake.WakeSequence -ge $Sequence)
  soleLead=($leadCount -eq 1)
  singleLiveGuiProcess=($procs.Count -eq 1 -and $live.SessionId -eq 1)
  correctLiveBinary=$binaryOk
  rollbackIntegrity=$rollbackOk
  currentPreflight=($preflight.pid -eq $live.ProcessId -and $preflightAgeSeconds -ge -5 -and $preflightAgeSeconds -le 240 -and [string]::IsNullOrEmpty($preflight.error))
  boundedTurns=([int]$preflight.turns -le 10)
  recentRealUiHeartbeat=($hbAge -ge -5 -and $hbAge -le 95)
  recentHealthyDom=($domAge -ge -5 -and $domAge -le 95 -and [int]$dom[0].details.visibleTurns -le 10 -and [bool]$dom[0].details.enabled -and [int]$dom[0].details.failures -eq 0 -and [int]$dom[0].details.reinsertions -eq 0)
}
$failed=@($checks.Keys | Where-Object {-not $checks[$_]})
[pscustomobject]@{utc=[DateTimeOffset]::UtcNow.ToString('o');wakeSequence=$Sequence;receipt=$Receipt;livePid=$live.ProcessId;leadCount=$leadCount;preflightAgeSec=[math]::Round($preflightAgeSeconds,1);checks=$checks;failed=$failed;passed=($failed.Count -eq 0)} | ConvertTo-Json -Compress -Depth 4
if($failed.Count -gt 0){exit 3}else{exit 0}
