# Audit only. Does not send, retry, restart, or alter wake data.
# Use on PBOX1 where the engine and model share the append-only JSONL file.
param(
    [string]$Path = (Join-Path $env:LOCALAPPDATA 'OpenBrowser\ChatGPT-Lite\diagnostics\wake-lifecycle.jsonl'),
    [switch]$ShowAll
)
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
    Write-Output 'NO_WAKE_JOURNAL'
    exit 2
}
$rows = [System.Collections.Generic.Dictionary[string,object]]::new(
    [StringComparer]::Ordinal)
$invalid = 0
$unpairedDue = 0
$readCount = 0
foreach ($line in [System.IO.File]::ReadLines($Path, [Text.Encoding]::UTF8)) {
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    try { $entry = $line | ConvertFrom-Json }
    catch { $invalid++; continue }
    $readCount++
    if ($entry.schema -ne 1 -or $entry.actor -notin @('engine','chat')) {
        $invalid++
        continue
    }
    $id = [string]$entry.receipt
    if ($id -notmatch '^[0-9a-f]{32}$') {
        if ($entry.stage -eq 'due' -or $entry.stage -eq 'deferred' -or
            $entry.stage -eq 'schedule_completed') { $unpairedDue++ }
        else { $invalid++ }
        continue
    }
    if (-not $rows.ContainsKey($id)) {
        $rows.Add($id, [pscustomobject]@{
            Receipt = $id
            Sequence = if ($null -ne $entry.wakeSequence) { [string]$entry.wakeSequence } else { 'legacy' }
            Attempt = $false
            Submit = $false
            Ack = $false
            Uncertain = $false
            Unsent = $false
            ChatStarted = $false
            ChatFinished = $false
            ChatBlocked = $false
            Mismatch = $false
            StartUtc = ''
        })
    }
    $x = $rows[$id]
    if ($null -ne $entry.wakeSequence -and $x.Sequence -ne 'legacy' -and
        $x.Sequence -ne [string]$entry.wakeSequence) { $x.Mismatch = $true }
    if ($x.Sequence -eq 'legacy' -and $null -ne $entry.wakeSequence) {
        $x.Sequence = [string]$entry.wakeSequence
    }
    if (-not $x.StartUtc) { $x.StartUtc = [string]$entry.utc }
    if ($entry.actor -eq 'engine') {
        switch ([string]$entry.stage) {
            'attempt_reserved' { $x.Attempt = $true }
            'submission_attempted' { $x.Submit = $true }
            'receipt_confirmed' { $x.Ack = $true }
            'receipt_uncertain' { $x.Uncertain = $true }
            'definitely_unsent' { $x.Unsent = $true }
        }
    } else {
        switch ([string]$entry.stage) {
            'chat_started' { $x.ChatStarted = $true }
            'chat_finished' { $x.ChatFinished = $true }
            'chat_blocked' { $x.ChatBlocked = $true }
        }
    }
}
$all = @($rows.Values | Sort-Object StartUtc)
$done = @($all | Where-Object ChatFinished).Count
$blocked = @($all | Where-Object ChatBlocked).Count
$started = @($all | Where-Object ChatStarted).Count
$ack = @($all | Where-Object Ack).Count
$waiting = @($all | Where-Object { $_.Ack -and -not $_.ChatStarted }).Count
$open = @($all | Where-Object {
    $_.ChatStarted -and -not $_.ChatFinished -and -not $_.ChatBlocked }).Count
$uncertain = @($all | Where-Object { $_.Uncertain -and -not $_.Ack }).Count
$ambiguous = @($all | Where-Object { $_.Attempt -and -not $_.Ack -and
    -not $_.Unsent }).Count
$disagree = @($all | Where-Object Mismatch).Count
Write-Output ("WAKE_AUDIT entries={0} receipts={1} due_without_receipt={2} bad_rows={3}" -f
    $readCount,$all.Count,$unpairedDue,$invalid)
Write-Output ("ACKED={0} CHAT_STARTED={1} CHAT_FINISHED={2} CHAT_BLOCKED={3}" -f
    $ack,$started,$done,$blocked)
$summaryFormat = "ACK_NO_CHAT_START={0} CHAT_STARTED_NOT_FINISHED={1} UNCERTAIN_RECEIPTS={2} " +
    "ATTEMPT_WITHOUT_ACK={3} SEQUENCE_MISMATCHES={4}"
Write-Output ($summaryFormat -f $waiting,$open,$uncertain,$ambiguous,$disagree)
if ($ShowAll) {
    $interesting = @($all)
} else {
    $interesting = @($all | Where-Object {
        $_.Mismatch -or
        ($_.ChatStarted -and -not $_.ChatFinished -and -not $_.ChatBlocked) -or
        ($_.Ack -and -not $_.ChatStarted) -or
        ($_.Uncertain -and -not $_.Ack)
    })
}
foreach ($x in $interesting) {
    $brief = $x.Receipt.Substring(0,8)
    $status = if ($x.Mismatch) { 'SEQUENCE_MISMATCH' }
        elseif ($x.ChatFinished) { 'CHAT_FINISHED' }
        elseif ($x.ChatBlocked) { 'CHAT_BLOCKED' }
        elseif ($x.ChatStarted) { 'CHAT_RUNNING_OR_UNCONFIRMED_END' }
        elseif ($x.Ack) { 'ACKED_NO_CHAT_STARTED' }
        elseif ($x.Uncertain) { 'UNCERTAIN_SEND' }
        elseif ($x.Unsent) { 'DEFINITELY_UNSENT' }
        else { 'RESERVED_ONLY' }
    Write-Output ("WAKE sequence={0} receipt={1} status={2} engineACK={3} started={4}" -f
        $x.Sequence,$brief,$status,$x.Ack,$x.ChatStarted)
}
if ($invalid -gt 0 -or $disagree -gt 0) { exit 1 }
exit 0
