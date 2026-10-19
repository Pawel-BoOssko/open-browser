$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$analyzer = Join-Path $root 'scripts\analyze-lite-resource-trends.ps1'
$fixture = Join-Path $PSScriptRoot 'lite-resource-samples.fixture.jsonl'

function Check([bool]$condition, [string]$label) {
    if (-not $condition) { throw ('FAIL ' + $label) }
    Write-Output ('PASS ' + $label)
}

$normal = (& $analyzer -Path $fixture -MaxSamples 2) | ConvertFrom-Json
Check ($normal.sampleCount -eq 2) 'normal sample count'
Check ($normal.stableHostSampleCount -eq 2) 'same host pid'
Check ($normal.stableWebviewSampleCount -eq 2) 'same webview pid'
Check ($normal.stableWebviewPrivateDeltaMiB -eq 50) 'real 50MiB webview increase'
Check ($normal.hasWebviewChurnOrRestart -eq $false) 'no churn'

$temp = Join-Path $env:TEMP ('openbrowser-resource-test-' + [Guid]::NewGuid().ToString('N') + '.jsonl')
try {
    $lines = @(Get-Content -LiteralPath $fixture -Encoding UTF8)
    $changed = $lines[1] | ConvertFrom-Json
    $changed.webviewProcesses[0].pid = 99
    [IO.File]::WriteAllLines($temp, [string[]]@(
        $lines[0], ($changed | ConvertTo-Json -Depth 7 -Compress)
    ), [Text.UTF8Encoding]::new($false))
    $churn = (& $analyzer -Path $temp -MaxSamples 2) | ConvertFrom-Json
    Check ($churn.stableHostSampleCount -eq 2) 'host survived webview churn'
    Check ($churn.stableWebviewSampleCount -eq 1) 'webview churn resets stable window'
    Check ($null -eq $churn.stableWebviewPrivateDeltaMiB) 'unknown delta is null'
    Check ($churn.hasWebviewChurnOrRestart -eq $true) 'churn is flagged'
}
finally { if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Force } }
Write-Output 'LITE_RESOURCE_TRENDS_9_PASS'
