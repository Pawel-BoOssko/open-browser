param(
    [string]$Path = (Join-Path $env:LOCALAPPDATA (
        'OpenBrowser\ChatGPT-Lite\diagnostics\resources-' +
        [DateTime]::UtcNow.ToString('yyyy-MM-dd') + '.jsonl')),
    [ValidateRange(2, 1000)][int]$MaxSamples = 120
)
# Read-only analysis of a bounded resource history. No chat text or process arguments.
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
    throw "No resource samples at the requested path"
}
$rows = @(
    Get-Content -LiteralPath $Path -Tail $MaxSamples -Encoding UTF8 |
        ForEach-Object {
            try {
                $r = $_ | ConvertFrom-Json -ErrorAction Stop
                if ($r.type -eq 'lite_process_resources' -and
                    $null -ne $r.freeMemoryMiB -and
                    $null -ne $r.pagefileUsedMiB) {
                    $r
                }
            } catch { }
        } |
        Sort-Object { [datetime]$_.utc }
)
if ($rows.Count -lt 2) { throw 'Need at least two valid samples' }
$first = $rows[0]
$last = $rows[-1]
$duration = ([datetime]$last.utc).ToUniversalTime() -
            ([datetime]$first.utc).ToUniversalTime()
if ($duration.TotalSeconds -le 0) { throw 'Non-positive sample interval' }
$free = @($rows | ForEach-Object { [double]$_.freeMemoryMiB })
$page = @($rows | ForEach-Object { [double]$_.pagefileUsedMiB })
$critical = @($free | Where-Object { $_ -lt 512 }).Count
$warning = @($free | Where-Object { $_ -ge 512 -and $_ -lt 1024 }).Count
$maxGapSeconds = 0.0
for ($i = 1; $i -lt $rows.Count; $i++) {
    $gap = (([datetime]$rows[$i].utc).ToUniversalTime() -
            ([datetime]$rows[$i-1].utc).ToUniversalTime()).TotalSeconds
    if ($gap -gt $maxGapSeconds) { $maxGapSeconds = $gap }
}
function WebViewPrivateSum($row) {
    $sum = @($row.webviewProcesses | Measure-Object -Property privateMiB -Sum)
    if ($sum.Count -eq 0 -or $null -eq $sum[0].Sum) { return 0.0 }
    return [double]$sum[0].Sum
}
function HostIdentity($row) {
    $hosts = @($row.hosts)
    if ($hosts.Count -ne 1 -or !$hosts[0].pid) { return '' }
    return [string]$hosts[0].pid
}
function WebViewIdentity($row) {
    $web = @($row.webviewProcesses)
    if ($web.Count -eq 0 -or $web.Count -ne [int]$row.webviewCount) { return '' }
    return [string]::Join(',', @($web | ForEach-Object { [string]$_.pid } | Sort-Object))
}
$stableHostFrom = $rows.Count - 1
$stableWebFrom = $rows.Count - 1
$finalHostId = HostIdentity $last
$finalWebId = WebViewIdentity $last
for ($i = $rows.Count - 2; $i -ge 0; $i--) {
    $gapSeconds = ([datetime]$rows[$i + 1].utc -
                   [datetime]$rows[$i].utc).TotalSeconds
    if ($gapSeconds -le 0 -or $gapSeconds -gt 300 -or
        !$finalHostId -or (HostIdentity $rows[$i]) -ne $finalHostId) { break }
    $stableHostFrom = $i
    if ($stableWebFrom -eq $i + 1 -and $finalWebId -and
        (WebViewIdentity $rows[$i]) -eq $finalWebId) {
        $stableWebFrom = $i
    }
}
$webStableCount = $rows.Count - $stableWebFrom
$webStableDelta = $null
$webStableMinutes = 0.0
if ($webStableCount -ge 2) {
    $webStableDelta = [math]::Round(
        ((WebViewPrivateSum $last) -
         (WebViewPrivateSum $rows[$stableWebFrom])), 1)
    $webStableMinutes = [math]::Round(
        (([datetime]$last.utc - [datetime]$rows[$stableWebFrom].utc).TotalMinutes), 1)
}
$firstWeb = WebViewPrivateSum $first
$lastWeb = WebViewPrivateSum $last
$allWeb = @($rows | ForEach-Object { WebViewPrivateSum $_ })
$pressure = if ($critical -gt 0) { 'critical_observed' } elseif ($warning -gt 0) {
    'warning_observed'
} else { 'normal_observed' }
$trendWindows = [ordered]@{}
$lastInstant = [DateTimeOffset]::Parse([string]$last.utc)
foreach ($window in @(@{ label = '5m'; minutes = 5 },
                      @{ label = '1h'; minutes = 60 },
                      @{ label = '24h'; minutes = 1440 })) {
    $threshold = $lastInstant.AddMinutes(-$window.minutes)
    $bucket = @($rows | Where-Object {
        [DateTimeOffset]::Parse([string]$_.utc) -ge $threshold
    })
    $firstInstant = [DateTimeOffset]::Parse([string]$bucket[0].utc)
    $hostPrivate = @($bucket | ForEach-Object {
        @($_.hosts | Measure-Object -Property privateMiB -Sum)[0].Sum
    })
    $webPrivate = @($bucket | ForEach-Object { WebViewPrivateSum $_ })
    $cpu = @($bucket | ForEach-Object {
        @($_.webviewProcesses | ForEach-Object {
            if ($null -ne $_.cpuCorePercent) { [double]$_.cpuCorePercent }
        })
    })
    $trendWindows[$window.label] = [ordered]@{
        samples = $bucket.Count
        observedMinutes = [math]::Round(($lastInstant - $firstInstant).TotalMinutes, 1)
        requestedMinutes = $window.minutes
        minFreeMemoryMiB = [math]::Round(($bucket | Measure-Object -Property freeMemoryMiB -Minimum).Minimum, 0)
        maxHostPrivateMiB = [math]::Round(($hostPrivate | Measure-Object -Maximum).Maximum, 1)
        maxWebviewPrivateMiB = [math]::Round(($webPrivate | Measure-Object -Maximum).Maximum, 1)
        maxWebviewCpuCorePercent = if ($cpu.Count) { [math]::Round(($cpu | Measure-Object -Maximum).Maximum, 1) } else { $null }
    }
}
$result = [ordered]@{
    schema = 1
    firstUtc = ([datetime]$first.utc).ToUniversalTime().ToString('o')
    lastUtc = ([datetime]$last.utc).ToUniversalTime().ToString('o')
    sampleCount = $rows.Count
    durationMinutes = [math]::Round($duration.TotalMinutes, 1)
    maxSampleGapSeconds = [math]::Round($maxGapSeconds, 1)
    minFreeMemoryMiB = [math]::Round(($free | Measure-Object -Minimum).Minimum, 0)
    lastFreeMemoryMiB = [math]::Round($free[-1], 0)
    criticalSamples = $critical
    warningSamples = $warning
    pressure = $pressure
    maxPagefileUsedMiB = [math]::Round(($page | Measure-Object -Maximum).Maximum, 0)
    webviewPrivateFirstMiB = [math]::Round($firstWeb, 1)
    webviewPrivateLastMiB = [math]::Round($lastWeb, 1)
    webviewPrivateDeltaMiB = [math]::Round(($lastWeb - $firstWeb), 1)
    webviewPrivateDeltaIsRaw = $true
    stableHostSampleCount = ($rows.Count - $stableHostFrom)
    stableWebviewSampleCount = $webStableCount
    stableWebviewDurationMinutes = $webStableMinutes
    stableWebviewPrivateDeltaMiB = $webStableDelta
    hasWebviewChurnOrRestart = ($webStableCount -lt $rows.Count)
    windows = $trendWindows
    maxWebviewPrivateMiB = [math]::Round(($allWeb | Measure-Object -Maximum).Maximum, 1)
    caution = 'Raw totals across restarts are not leak estimates; stable delta requires identical PIDs and <=300s gaps'
}
ConvertTo-Json -InputObject $result -Depth 5 -Compress
