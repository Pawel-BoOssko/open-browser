param(
    [string]$Path = (Join-Path $env:LOCALAPPDATA ('OpenBrowser\ChatGPT-Lite\diagnostics\resources-' + [DateTime]::UtcNow.ToString('yyyy-MM-dd') + '.jsonl'))
)
# Read-only analysis of two adjacent samples. Never reads chat contents.
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $Path)) { Write-Error 'Resource samples unavailable'; exit 2 }
$rows = @(
    Get-Content -LiteralPath $Path -Tail 16 -Encoding UTF8 |
        ForEach-Object {
            try {
                $item = $_ | ConvertFrom-Json
                if ($item.type -eq 'lite_process_resources') { $item }
            } catch { }
        }
)
if ($rows.Count -lt 2) { Write-Error 'Need two valid resource samples'; exit 2 }
$before = $rows[$rows.Count - 2]
$after = $rows[$rows.Count - 1]
$seconds = (([datetime]$after.utc).ToUniversalTime() - ([datetime]$before.utc).ToUniversalTime()).TotalSeconds
if ($seconds -le 0 -or $seconds -gt 3600) { Write-Error 'Invalid sample interval'; exit 2 }
$previous = @{}
foreach ($process in @($before.hosts) + @($before.webviewProcesses)) {
    $previous[[string]$process.pid] = $process
}
$cpu = @(
    foreach ($process in @($after.hosts) + @($after.webviewProcesses)) {
        $old = $previous[[string]$process.pid]
        if ($null -eq $old) { continue }
        $delta = [double]$process.cpuSecondsCumulative - [double]$old.cpuSecondsCumulative
        if ($delta -lt 0) { continue }
        [pscustomobject]@{
            pid = [int]$process.pid
            role = [string]$process.role
            cpuOneCorePercent = [math]::Round(100 * $delta / $seconds, 1)
            cpuSystemPercent = [math]::Round(100 * $delta / $seconds / [Environment]::ProcessorCount, 2)
            workingMiB = [double]$process.workingMiB
            privateMiB = [double]$process.privateMiB
        }
    }
)
$free = [double]$after.freeMemoryMiB
$level = if ($free -lt 512) { 'critical' } elseif ($free -lt 1024) { 'warning' } else { 'normal' }
$result = [ordered]@{
    utc = ([datetime]$after.utc).ToUniversalTime().ToString('o')
    intervalSeconds = [math]::Round($seconds, 1)
    memoryPressure = $level
    freeMemoryMiB = $free
    totalMemoryMiB = [double]$after.totalMemoryMiB
    pagefileUsedMiB = [double]$after.pagefileUsedMiB
    hostCount = [int]$after.hostCount
    webviewCount = [int]$after.webviewCount
    logicalProcessors = [Environment]::ProcessorCount
    cpuByProcess = @($cpu | Sort-Object cpuOneCorePercent -Descending | Select-Object -First 10)
}
ConvertTo-Json -InputObject $result -Depth 5 -Compress
