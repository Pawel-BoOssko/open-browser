$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$script = Join-Path $repo 'scripts\audit-lite-release-state.ps1'
function Get-CimInstance {
    [CmdletBinding()]
    param([string]$ClassName, [string]$Filter)
    if ($ClassName -eq 'Win32_Process') {
        return @(
            [pscustomobject]@{ ProcessId = 101; CommandLine = 'BridgeBrowserAlpha0.exe --chatgpt-lite' },
            [pscustomobject]@{ ProcessId = 202; CommandLine = 'BridgeBrowserAlpha0.exe --lite-await-upgrade 101' }
        )
    }
    return [pscustomobject]@{ FreePhysicalMemory = 7340032 }
}
function Get-Process {
    [CmdletBinding()]
    param([string]$Name)
    return @(
        [pscustomobject]@{
            Id = 101; Path = 'C:\Fake\OpenBrowserChatGPTLite_v1.60.0\BridgeBrowserAlpha0.exe'
            StartTime = [datetime]'2026-10-18T14:00:00Z'
            Responding = $true; WorkingSet64 = 100MB; PrivateMemorySize64 = 80MB
        },
        [pscustomobject]@{
            Id = 202; Path = 'C:\Fake\OpenBrowserChatGPTLite_v1.61.0\BridgeBrowserAlpha0.exe'
            StartTime = [datetime]'2026-10-18T14:30:00Z'
            Responding = $true; WorkingSet64 = 30MB; PrivateMemorySize64 = 20MB
        }
    )
}
function Get-ScheduledTask { throw 'Synthetic task unavailable' }
function Get-ScheduledTaskInfo { throw 'Synthetic task unavailable' }
$a = & $script -ProgramsRoot (Join-Path $env:TEMP 'nonexistent-lite-release-fixture') |
    ConvertFrom-Json
if (@($a.live).Count -ne 1 -or [int]$a.live[0].pid -ne 101) {
    throw 'Upgrade helper was incorrectly classified as LIVE'
}
Write-Output 'HELPER_EXCLUDED_PASS'
if ($a.action -ne 'read_only_no_rollout' -or $a.schema -ne 1) {
    throw 'Audit changed runtime or schema'
}
Write-Output 'READ_ONLY_SCHEMA_PASS'
