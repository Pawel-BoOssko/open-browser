# Open Browser Lite: one-user watchdog. Runs only in an interactive Windows user session.
# Never restarts a running browser and never promotes staged versions.
param([switch]$DryRun)

$ErrorActionPreference = 'Stop'
$diagnostics = Join-Path $env:LOCALAPPDATA 'OpenBrowser\ChatGPT-Lite\diagnostics'
$statusFile = Join-Path $diagnostics 'lite-keepalive-status.json'

function Save-Status([string]$state, [string]$details) {
    [System.IO.Directory]::CreateDirectory($diagnostics) | Out-Null
    $payload = [ordered]@{
        checkedAt = (Get-Date).ToString('o')
        state = $state
        details = $details
    } | ConvertTo-Json -Compress
    [System.IO.File]::WriteAllText($statusFile, $payload,
        [System.Text.UTF8Encoding]::new($false))
}

try {
    $existing = @(Get-Process -Name 'BridgeBrowserAlpha0' -ErrorAction SilentlyContinue)
    if ($existing.Count -gt 0) {
        if (@($existing | Where-Object { $_.Responding }).Count -gt 0) {
            Save-Status 'running' ("existingProcesses=" + $existing.Count)
            return
        }
        # Only recover a persistently frozen window. One failed heartbeat can
        # be transient while WebView2 handles a very large conversation.
        $pids = ($existing | Select-Object -ExpandProperty Id | Sort-Object) -join ','
        $previous = if (Test-Path -LiteralPath $statusFile) {
            try { Get-Content -LiteralPath $statusFile -Raw | ConvertFrom-Json }
            catch { $null }
        } else { $null }
        $again = $previous -and $previous.state -eq 'unresponsive_once' -and
            $previous.details -eq ("pids=" + $pids) -and
            ((Get-Date) - [datetime]$previous.checkedAt).TotalMinutes -le 12
        if (-not $again) {
            Save-Status 'unresponsive_once' ("pids=" + $pids)
            return
        }
        if ($DryRun) {
            Save-Status 'would_recover_hung' ("pids=" + $pids)
            return
        }
        $existing | Stop-Process -Force
        Start-Sleep -Seconds 2
        if (@(Get-Process -Name 'BridgeBrowserAlpha0' -ErrorAction SilentlyContinue).Count -gt 0) {
            Save-Status 'recovery_waiting' ("pids=" + $pids)
            return
        }
    }

    $shortcut = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Open Browser - ChatGPT Lite.lnk'
    if (-not (Test-Path -LiteralPath $shortcut -PathType Leaf)) {
        Save-Status 'missing_shortcut' 'Start Menu shortcut not found'
        return
    }
    $shell = New-Object -ComObject WScript.Shell
    $target = $shell.CreateShortcut($shortcut).TargetPath
    $expectedRoot = Join-Path $env:LOCALAPPDATA 'Programs\OpenBrowserChatGPTLite_'
    if (-not $target.StartsWith($expectedRoot, [StringComparison]::OrdinalIgnoreCase) -or
        -not (Test-Path -LiteralPath $target -PathType Leaf)) {
        Save-Status 'bad_target' 'Shortcut does not point to an installed Lite release'
        return
    }

    if ($DryRun) {
        Save-Status 'would_launch' (Split-Path -Parent $target | Split-Path -Leaf)
        return
    }
    $started = Start-Process -FilePath $target -ArgumentList '--lite-open-safely' -PassThru
    Save-Status 'launched' ("processId=" + $started.Id)
}
catch {
    Save-Status 'error' $_.Exception.GetType().Name
}
