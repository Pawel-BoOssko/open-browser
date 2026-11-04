# Read-only fallback evidence when the foreground WebView2 browser process
# exited and cannot produce fresh preflight samples. Never approves an active
# or unfinished wake. Use together with two independent 22-second checks.
function Get-OblCrashIdleProof {
    param(
        [int]$ExpectedPid,
        [DateTimeOffset]$NowUtc = [DateTimeOffset]::UtcNow,
        [string]$PreflightPath = (Join-Path $env:LOCALAPPDATA 'OpenBrowser\ChatGPT-Lite\diagnostics\live-preflight.json'),
        [string]$EventsPath = (Join-Path $env:LOCALAPPDATA ('OpenBrowser\ChatGPT-Lite\diagnostics\lite-events-'+(Get-Date -Format 'yyyy-MM-dd')+'.jsonl')),
        [string]$JournalPath = (Join-Path $env:LOCALAPPDATA 'OpenBrowser\ChatGPT-Lite\diagnostics\wake-lifecycle.jsonl')
    )
    try {
        $pre = Get-Content -LiteralPath $PreflightPath -Raw | ConvertFrom-Json
        if([int]$pre.pid -ne $ExpectedPid -or
            [bool]$pre.generating -or [bool]$pre.draft -or [bool]$pre.toolBusy -or
            -not [string]::IsNullOrWhiteSpace([string]$pre.error) -or
            -not ([string]$pre.source).StartsWith('https://chatgpt.com/c/')){
            return $null
        }
        $preUtc = [DateTimeOffset]::Parse([string]$pre.utc).ToUniversalTime()
        $events = @(Get-Content -LiteralPath $EventsPath -Tail 1500 | ConvertFrom-Json)
        $crash = @($events | Where-Object {
            $_.pid -eq $ExpectedPid -and
            $_.eventName -eq 'webview_process_failed' -and
            $_.details.kind -eq 'BrowserProcessExited'
        }) | Select-Object -Last 1
        if($null -eq $crash){return $null}
        $crashUtc = [DateTimeOffset]::Parse([string]$crash.utc).ToUniversalTime()
        if($crashUtc -le $preUtc -or
            ($NowUtc-$crashUtc).TotalMinutes -gt 65){return $null}
        $afterCrash = @($events | Where-Object {
            $_.pid -eq $ExpectedPid -and
            $_.eventName -in @('foreground_webview_recovered','webview_initialized') -and
            [DateTimeOffset]::Parse([string]$_.utc).ToUniversalTime() -gt $crashUtc
        })
        if($afterCrash.Count -ne 0){return $null}
        $heartbeat = @($events | Where-Object {
            $_.pid -eq $ExpectedPid -and $_.eventName -eq 'ui_heartbeat'
        }) | Select-Object -Last 1
        if($null -eq $heartbeat -or
           ($NowUtc-[DateTimeOffset]::Parse([string]$heartbeat.utc)).TotalSeconds -gt 65){
            return $null
        }
        $journal = @(Get-Content -LiteralPath $JournalPath -Tail 150 | ConvertFrom-Json)
        $finish = @($journal | Where-Object {
            $_.stage -eq 'chat_finished' -and
            -not [string]::IsNullOrWhiteSpace([string]$_.receipt)
        }) | Select-Object -Last 1
        if($null -eq $finish){return $null}
        $finishedUtc = [DateTimeOffset]::Parse([string]$finish.utc).ToUniversalTime()
        $quietSeconds = ($NowUtc-$finishedUtc).TotalSeconds
        if($finishedUtc -le $crashUtc -or $quietSeconds -lt 75 -or
            $quietSeconds -gt 800){return $null}
        $confirmed = @($journal | Where-Object {
            $_.stage -eq 'receipt_confirmed' -and
            $_.wakeSequence -eq $finish.wakeSequence -and
            $_.receipt -eq $finish.receipt -and $_.result -eq 'visible_ack'
        })
        if($confirmed.Count -eq 0){return $null}
        $newWork = @($journal | Where-Object {
            $_.stage -in @('due','attempt_reserved','submission_attempted',
                'notes_submitted','receipt_confirmed') -and
            [DateTimeOffset]::Parse([string]$_.utc).ToUniversalTime() -gt $finishedUtc
        })
        if($newWork.Count -ne 0){return $null}
        return [pscustomobject]@{
            utc=$NowUtc.ToString('o')
            source=[string]$pre.source
            mode='crashed_foreground_completed_wake'
            wakeSequence=[int]$finish.wakeSequence
            receipt=[string]$finish.receipt
            crashUtc=$crashUtc.ToString('o')
        }
    }catch{
        return $null
    }
}