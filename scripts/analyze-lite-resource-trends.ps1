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
$firstWeb = WebViewPrivateSum $first
$lastWeb = WebViewPrivateSum $last
$allWeb = @($rows | ForEach-Object { WebViewPrivateSum $_ })
$pressure = if ($critical -gt 0) { 'critical_observed' } elseif ($warning -gt 0) {
    'warning_observed'
} else { 'normal_observed' }
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
    maxWebviewPrivateMiB = [math]::Round(($allWeb | Measure-Object -Maximum).Maximum, 1)
    caution = 'A process-count change or restart invalidates leak attribution'
}
ConvertTo-Json -InputObject $result -Depth 5 -Compress
