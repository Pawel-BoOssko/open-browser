param(
    [string]$ProgramsRoot = (Join-Path $env:LOCALAPPDATA 'Programs'),
    [string]$TaskName = 'OpenBrowser ChatGPT Lite',
    [switch]$SkipSystemProbes
)
# Read-only release evidence. Never starts, kills or updates a process, task or shortcut.
# Reports only executable paths, versions, timestamps and resource counters.
$ErrorActionPreference = 'Stop'

function Get-BuildIdentity([string]$ExePath) {
    if (-not (Test-Path -LiteralPath $ExePath -PathType Leaf)) {
        return [pscustomobject]@{ exists = $false; productVersion = $null; builtAtUtc = $null; stamped = $false }
    }
    $info = (Get-Item -LiteralPath $ExePath).VersionInfo
    $product = [string]$info.ProductVersion
    $built = $null
    $stamped = $false
    if ($product -match '\+build\.(\d{8}T\d{6}Z)(?:[.+]|$)') {
        $candidate = [datetime]::MinValue
        if ([datetime]::TryParseExact($Matches[1], "yyyyMMdd'T'HHmmss'Z'",
                [Globalization.CultureInfo]::InvariantCulture,
                [Globalization.DateTimeStyles]::AssumeUniversal, [ref]$candidate)) {
            $built = $candidate.ToUniversalTime().ToString('o')
            $stamped = $true
        }
    }
    return [pscustomobject]@{
        exists = $true
        productVersion = $product
        builtAtUtc = $built
        stamped = $stamped
    }
}

$now = [DateTimeOffset]::UtcNow
$installations = @()
if (Test-Path -LiteralPath $ProgramsRoot -PathType Container) {
    $installations = @(
        Get-ChildItem -LiteralPath $ProgramsRoot -Directory -Filter 'OpenBrowserChatGPTLite_*' |
            Sort-Object LastWriteTimeUtc -Descending |
            Select-Object -First 25 |
            ForEach-Object {
                $exe = Join-Path $_.FullName 'BridgeBrowserAlpha0.exe'
                [pscustomobject]@{
                    folder = $_.Name
                    path = $exe
                    identity = Get-BuildIdentity $exe
                }
            }
    )
}
$live = @()
$scheduled = $null
$monitor = $null
$freeMiB = $null
$ramLevel = 'unknown'
if (-not $SkipSystemProbes) {
    $upgradeHelperPids = @()
    try {
        $upgradeHelperPids = @(Get-CimInstance Win32_Process -Filter "Name='BridgeBrowserAlpha0.exe'" |
            Where-Object { $_.CommandLine -match '--lite-await-upgrade\s+\d+' } |
            ForEach-Object { [int]$_.ProcessId })
    } catch { }
    try {
        $live = @(Get-Process -Name 'BridgeBrowserAlpha0' -ErrorAction SilentlyContinue |
            Where-Object { $_.Id -notin $upgradeHelperPids } |
            ForEach-Object {
                $exe = $_.Path
                if (-not $exe -or $exe -notlike '*OpenBrowserChatGPTLite_*') { return }
                [pscustomobject]@{
                    pid = $_.Id
                    path = $exe
                    startedUtc = $_.StartTime.ToUniversalTime().ToString('o')
                    responding = $_.Responding
                    workingMiB = [math]::Round($_.WorkingSet64 / 1MB, 1)
                    privateMiB = [math]::Round($_.PrivateMemorySize64 / 1MB, 1)
                    identity = Get-BuildIdentity $exe
                }
            })
    } catch { $live = @() }
    try {
        $task = Get-ScheduledTask -TaskName $TaskName -ErrorAction Stop
        $action = @($task.Actions | Where-Object { $_.Execute }) | Select-Object -First 1
        if ($action) {
            $exe = [Environment]::ExpandEnvironmentVariables([string]$action.Execute)
            $scheduled = [pscustomobject]@{
                executable = $exe
                exists = Test-Path -LiteralPath $exe -PathType Leaf
                matchesAnyLive = @($live | Where-Object {
                    [string]::Equals($_.path, $exe, [StringComparison]::OrdinalIgnoreCase)
                }).Count -gt 0
            }
        }
    } catch { }
    try {
        $info = Get-ScheduledTaskInfo -TaskName 'OpenBrowser Lite Resource Monitor' -ErrorAction Stop
        $monitor = [pscustomobject]@{
            lastRunUtc = $info.LastRunTime.ToUniversalTime().ToString('o')
            lastResult = [int]$info.LastTaskResult
        }
    } catch { }
    try {
        $os = Get-CimInstance Win32_OperatingSystem -ErrorAction Stop
        $freeMiB = [math]::Round($os.FreePhysicalMemory / 1024, 0)
        $ramLevel = if ($freeMiB -lt 512) { 'critical' } elseif ($freeMiB -lt 1024) {
            'warning'
        } else { 'normal' }
    } catch { }
}
$hasMismatch = $scheduled -and $live.Count -gt 0 -and -not $scheduled.matchesAnyLive
$result = [ordered]@{
    schema = 1
    utc = $now.ToString('o')
    host = $env:COMPUTERNAME
    probeMode = if ($SkipSystemProbes) { 'offline' } else { 'system' }
    freeMemoryMiB = $freeMiB
    memoryPressure = $ramLevel
    live = @($live)
    scheduledTask = $scheduled
    taskLiveMismatch = [bool]$hasMismatch
    resourceMonitor = $monitor
    installationCount = $installations.Count
    installations = @($installations)
    action = 'read_only_no_rollout'
}
ConvertTo-Json -InputObject $result -Depth 8 -Compress
