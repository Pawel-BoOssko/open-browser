# Low-overhead, read-only external monitoring for the ChatGPT Lite profile.
# Never captures prompts, answers, cookies, tokens or complete URLs.
$ErrorActionPreference = 'SilentlyContinue'
$folder = Join-Path $env:LOCALAPPDATA 'OpenBrowser\ChatGPT-Lite\diagnostics'
[void](New-Item -ItemType Directory -Path $folder -Force)
$stamp = [DateTimeOffset]::UtcNow
$path = Join-Path $folder ('resources-' + $stamp.ToString('yyyy-MM-dd') + '.jsonl')
if ((Test-Path $path) -and (Get-Item $path).Length -gt 10MB) { exit 0 }
$os = Get-CimInstance Win32_OperatingSystem
$pf = Get-CimInstance Win32_PageFileUsage
$lite = @(Get-CimInstance Win32_Process -Filter "Name='BridgeBrowserAlpha0.exe'" | Where-Object { $_.CommandLine -match '--(chatgpt-lite|lite-open-safely)(\s|$)' })
$web = @(Get-CimInstance Win32_Process -Filter "Name='msedgewebview2.exe'" | Where-Object { $_.CommandLine -like '*ChatGPT-Lite*' })
function Measure-One($cim) {
    $p = Get-Process -Id $cim.ProcessId -ErrorAction SilentlyContinue
    if ($null -eq $p) { return $null }
    $kind = if ($cim.CommandLine -match '--type=([^ ]+)') { $Matches[1] } else { 'browser' }
    return [pscustomobject]@{
        pid = $cim.ProcessId
        parentPid = $cim.ParentProcessId
        role = $kind
        workingMiB = [math]::Round($p.WorkingSet64 / 1MB, 1)
        privateMiB = [math]::Round($p.PrivateMemorySize64 / 1MB, 1)
        cpuSecondsCumulative = [math]::Round($p.CPU, 2)
        responding = $p.Responding
    }
}
$liteRows = @($lite | ForEach-Object { Measure-One $_ } | Where-Object { $_ })
$webRows = @($web | ForEach-Object { Measure-One $_ } | Where-Object { $_ })
$logicalProcessors = [Environment]::ProcessorCount
$cpuSampleIntervalSeconds = $null
# CPU is cumulative per process; derive rate from a recent sample for same PID.
try {
    if (Test-Path -LiteralPath $path) {
        $lastLine = Get-Content -LiteralPath $path -Tail 1 -Encoding UTF8
        $previous = $lastLine | ConvertFrom-Json -ErrorAction Stop
        $elapsed = ($stamp - [DateTimeOffset]::Parse([string]$previous.utc)).TotalSeconds
        if ($previous.type -eq 'lite_process_resources' -and
            $elapsed -ge 2 -and $elapsed -le 900) {
            $cpuSampleIntervalSeconds = [math]::Round($elapsed, 1)
            foreach ($kind in @('hosts', 'webviewProcesses')) {
                $beforeRows = @($previous.$kind)
                $currentRows = if ($kind -eq 'hosts') { $liteRows } else { $webRows }
                foreach ($row in @($currentRows)) {
                    $before = $beforeRows | Where-Object { $_.pid -eq $row.pid } | Select-Object -First 1
                    if ($null -eq $before -or $null -eq $before.cpuSecondsCumulative) { continue }
                    $cpuDelta = [double]$row.cpuSecondsCumulative - [double]$before.cpuSecondsCumulative
                    if ($cpuDelta -lt 0) { continue } # Restart or PID reuse.
                    $perCore = [math]::Round(100 * $cpuDelta / $elapsed, 1)
                    $row | Add-Member -NotePropertyName cpuCorePercent -NotePropertyValue $perCore
                    $row | Add-Member -NotePropertyName cpuMachinePercent -NotePropertyValue ([math]::Round($perCore / $logicalProcessors, 2))
                }
            }
        }
    }
} catch {
    # The first sample, an incomplete line, or a replaced process is normal.
}
$knownPids = @($liteRows | ForEach-Object { $_.pid }) +
    @($webRows | ForEach-Object { $_.pid })
$orphanCandidates = @($webRows | Where-Object { $_.parentPid -notin $knownPids })
$top = @(Get-Process | Sort-Object WorkingSet64 -Descending | Select-Object -First 6 | ForEach-Object {
    [pscustomobject]@{ name = $_.ProcessName; pid = $_.Id; workingMiB = [math]::Round($_.WorkingSet64 / 1MB, 1) }
})
$record = [ordered]@{
    utc = $stamp.ToString('o')
    type = 'lite_process_resources'
    totalMemoryMiB = [math]::Round($os.TotalVisibleMemorySize / 1024, 0)
    freeMemoryMiB = [math]::Round($os.FreePhysicalMemory / 1024, 0)
    pagefileUsedMiB = [math]::Round(($pf | Measure-Object CurrentUsage -Sum).Sum, 0)
    logicalProcessors = $logicalProcessors
    cpuSampleIntervalSeconds = $cpuSampleIntervalSeconds
    hostCount = $liteRows.Count
    hosts = $liteRows
    webviewCount = $webRows.Count
    webviewProcesses = $webRows
    orphanWebviewCandidateCount = $orphanCandidates.Count
    orphanWebviewCandidatePids = @($orphanCandidates | ForEach-Object { $_.pid })
    topMemoryConsumers = $top
}
try {
    $json = ConvertTo-Json -InputObject $record -Depth 7 -Compress
    [IO.File]::AppendAllText($path, $json + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
    Get-ChildItem $folder -File -Filter 'resources-*.jsonl' | Where-Object { $_.LastWriteTimeUtc -lt $stamp.UtcDateTime.AddDays(-14) } | Remove-Item -Force
} catch {
    exit 1
}
