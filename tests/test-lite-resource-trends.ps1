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
Check ($normal.windows.'5m'.samples -eq 2) '5m window sample count'
Check ($normal.windows.'1h'.samples -eq 2) '1h window sample count'
Check ($normal.windows.'24h'.samples -eq 2) '24h window sample count'
Check ($normal.windows.'5m'.maxWebviewPrivateMiB -eq 850) '5m peak WebView2 private MiB'

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
$htmlPath = Join-Path $env:TEMP ('openbrowser-dashboard-test-' + [Guid]::NewGuid().ToString('N') + '.html')
try {
    & (Join-Path $root 'scripts\export-lite-resource-dashboard.ps1') -InputPath $fixture -OutputPath $htmlPath | Out-Null
    Check ([IO.File]::Exists($htmlPath)) 'offline dashboard created'
    $html = [IO.File]::ReadAllText($htmlPath)
    Check ($html.Contains('5m') -and $html.Contains('1h') -and $html.Contains('24h')) 'all dashboard time windows'
    Check (-not $html.Contains('<script')) 'dashboard has no active script'
}
finally { if (Test-Path $htmlPath) { Remove-Item $htmlPath -Force } }
$eventFixture = Join-Path $env:TEMP ('openbrowser-events-test-' + [Guid]::NewGuid().ToString('N') + '.jsonl')
try {
    [IO.File]::WriteAllLines($eventFixture, [string[]]@(
        '{"utc":"2026-10-18T12:01:00Z","pid":10,"eventName":"dev_wake_direct_send","details":{"private":"MUST_NOT_APPEAR"}}',
        '{"utc":"2026-10-18T12:01:30Z","pid":10,"eventName":"ui_heartbeat","details":{"private":"MUST_NOT_APPEAR"}}'
    ), [Text.UTF8Encoding]::new($false))
    & (Join-Path $root 'scripts\export-lite-resource-dashboard.ps1') -InputPath $fixture -EventPath $eventFixture -OutputPath $htmlPath | Out-Null
    $reportHtml = [IO.File]::ReadAllText($htmlPath)
    Check ($reportHtml.Contains('dev_wake_direct_send')) 'diagnostic event displayed'
    Check (-not $reportHtml.Contains('MUST_NOT_APPEAR')) 'event details excluded'
}
finally { if (Test-Path $eventFixture) { Remove-Item $eventFixture -Force } }
# Rotation at midnight UTC: a 24h report must merge both source files,
# sort by sample UTC (not argument order), and preserve stable PID detection.
$crossA = Join-Path $env:TEMP ('obl-trend-before-midnight-' + [Guid]::NewGuid().ToString('N') + '.jsonl')
$crossB = Join-Path $env:TEMP ('obl-trend-after-midnight-' + [Guid]::NewGuid().ToString('N') + '.jsonl')
$crossHtml = Join-Path $env:TEMP ('obl-trend-cross-midnight-' + [Guid]::NewGuid().ToString('N') + '.html')
try {
    $fixtureRows = @(Get-Content -LiteralPath $fixture -Encoding UTF8)
    $a = $fixtureRows[0] | ConvertFrom-Json
    $b = $fixtureRows[1] | ConvertFrom-Json
    $a.utc = '2026-10-18T23:59:00Z'
    $b.utc = '2026-10-19T00:00:00Z'
    [IO.File]::WriteAllText($crossA, ($a | ConvertTo-Json -Depth 7 -Compress),
        [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText($crossB, ($b | ConvertTo-Json -Depth 7 -Compress),
        [Text.UTF8Encoding]::new($false))
    # Intentionally provide NEWER file first.
    $cross = (& $analyzer -Path $crossB -AdditionalPaths @($crossA) -MaxSamples 2) | ConvertFrom-Json
    Check ($cross.sampleCount -eq 2) 'two UTC daily files merge into two samples'
    Check ($cross.firstUtc -like '2026-10-18T23:59:*') 'cross-midnight chronological sort'
    Check ($cross.stableWebviewSampleCount -eq 2) 'same renderer pid across midnight'
    Check ($cross.stableWebviewPrivateDeltaMiB -eq 50) 'cross-midnight private delta is 50MiB'
    Check ($cross.windows.'24h'.samples -eq 2) '24h window crosses UTC midnight'
    & (Join-Path $root 'scripts\export-lite-resource-dashboard.ps1') -InputPath $crossB -AdditionalInputPaths @($crossA) -OutputPath $crossHtml | Out-Null
    Check ([IO.File]::Exists($crossHtml) -and ([IO.File]::ReadAllText($crossHtml)).Contains('50 MiB')) 'cross-day dashboard displays stable delta'
}
finally {
    foreach ($f in @($crossA, $crossB, $crossHtml)) {
        if (Test-Path -LiteralPath $f) { Remove-Item -LiteralPath $f -Force }
    }
}
Write-Output 'LITE_RESOURCE_TRENDS_24_PASS'
