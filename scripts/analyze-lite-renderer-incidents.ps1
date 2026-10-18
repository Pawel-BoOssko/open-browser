param(
    [string]$EventsPath = (Join-Path $env:LOCALAPPDATA
        ('OpenBrowser\ChatGPT-Lite\diagnostics\lite-events-' + (Get-Date -Format 'yyyy-MM-dd') + '.jsonl')),
    [string]$ResourcesPath = (Join-Path $env:LOCALAPPDATA
        ('OpenBrowser\ChatGPT-Lite\diagnostics\resources-' + (Get-Date -Format 'yyyy-MM-dd') + '.jsonl')),
    [ValidateRange(10,5000)][int]$EventTail = 1500,
    [ValidateRange(5,1000)][int]$ResourceTail = 150
)
# Read-only, bounded, metadata-only. Never loads chats, cookies, or API keys.
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $EventsPath -PathType Leaf)) {
    throw 'Event journal not found'
}
$events = @(
    Get-Content -LiteralPath $EventsPath -Tail $EventTail -Encoding UTF8 |
        ForEach-Object {
            try {
                $entry = $_ | ConvertFrom-Json -ErrorAction Stop
                if ($entry.utc -and $entry.eventName) { $entry }
            } catch { }
        }
)
$resources = @()
if (Test-Path -LiteralPath $ResourcesPath -PathType Leaf) {
    $resources = @(
        Get-Content -LiteralPath $ResourcesPath -Tail $ResourceTail -Encoding UTF8 |
            ForEach-Object {
                try {
                    $row = $_ | ConvertFrom-Json -ErrorAction Stop
                    if ($row.type -eq 'lite_process_resources' -and $row.utc) { $row }
                } catch { }
            }
    )
}
$incidentPattern = '^(webview_process_failed|response_probe_delayed|response_probe_failed|archive_probe_timeout|archive_probe_failed|dom_probe_timeout|dom_probe_failed|high_guard_probe_timeout|resume_probe_timeout|response_error_transition|chat_transport_loading_failed)$'
$candidates = @($events | Where-Object {
    $_.eventName -match $incidentPattern -and
    ($_.eventName -ne 'response_error_transition' -or
     $_.details.state -ne 'cleared')
} | Select-Object -Last 25)
$incidents = @(
    foreach ($event in $candidates) {
        $at = [datetimeoffset]::Parse([string]$event.utc).UtcDateTime
        $nearest = $null
        $seconds = [double]::PositiveInfinity
        foreach ($sample in $resources) {
            $diff = [math]::Abs((
                [datetimeoffset]::Parse([string]$sample.utc).UtcDateTime - $at).TotalSeconds)
            if ($diff -lt $seconds) { $nearest = $sample; $seconds = $diff }
        }
        if ($seconds -gt 180) { $nearest = $null }
        $near = @($events | Where-Object {
            $time = [datetimeoffset]::Parse([string]$_.utc).UtcDateTime
            [math]::Abs(($time - $at).TotalSeconds) -le 120
        })
        $errors = @($near | Where-Object {
            $_.eventName -eq 'web_backend_response' -and
            [int]$_.details.status -ge 400
        })
        $saved = @($near | Where-Object {
            $_.eventName -eq 'assistant_snapshot_saved'
        }).Count
        $heartbeat = @($near | Where-Object {
            $_.eventName -eq 'ui_heartbeat'
        }).Count
        $stage = switch -Regex ([string]$event.eventName) {
            '^webview_process_failed$' { 'renderer_process_failure'; break }
            '^response_error_transition$' { 'visible_page_error'; break }
            '^chat_transport_loading_failed$' { 'observed_transport_failure'; break }
            '^high_guard_probe_timeout$|^resume_probe_timeout$|^dom_probe_timeout$|^archive_probe_timeout$|^response_probe_delayed$' { 'renderer_javascript_delayed'; break }
            default { 'local_probe_failure'; break }
        }
        [ordered]@{
            timeUtc = $at.ToString('o')
            signal = [string]$event.eventName
            stage = $stage
            resourceSampleDistanceSeconds = if ($nearest) { [math]::Round($seconds,1) } else { $null }
            freeMemoryMiB = if ($nearest) { [double]$nearest.freeMemoryMiB } else { $null }
            pagefileUsedMiB = if ($nearest) { [double]$nearest.pagefileUsedMiB } else { $null }
            webviewCount = if ($nearest) { [int]$nearest.webviewCount } else { $null }
            httpErrorsNearby = $errors.Count
            localSnapshotEventsNearby = $saved
            uiHeartbeatsNearby = $heartbeat
            serverAnswerDelivery = 'unknown_not_proven'
        }
    }
)
[ordered]@{
    schema = 1
    scope = 'local_metadata_only'
    eventsRead = $events.Count
    resourceSamplesRead = $resources.Count
    incidentCount = $incidents.Count
    incidents = $incidents
    interpretation = 'HTTP 200, heartbeat, or saved local text do not prove server completion or full answer delivery'
} | ConvertTo-Json -Depth 7 -Compress
