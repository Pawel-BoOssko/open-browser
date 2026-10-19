param(
    [string]$InputPath = (Join-Path $env:LOCALAPPDATA (
        'OpenBrowser\ChatGPT-Lite\diagnostics\resources-' +
        [DateTime]::UtcNow.ToString('yyyy-MM-dd') + '.jsonl')),
    [string]$OutputPath = (Join-Path $env:LOCALAPPDATA 'OpenBrowser\ChatGPT-Lite\diagnostics\resources-dashboard.html')
)
$ErrorActionPreference = 'Stop'
$analyzer = Join-Path $PSScriptRoot 'analyze-lite-resource-trends.ps1'
$report = (& $analyzer -Path $InputPath -MaxSamples 1000) | ConvertFrom-Json
function Safe($s) { [System.Net.WebUtility]::HtmlEncode([string]$s) }
function NumberOrEmpty($v, $unit) {
    if ($null -eq $v) { return 'brak danych' }
    return (Safe $v) + $unit
}
$rows = foreach ($key in @('5m','1h','24h')) {
    $w = $report.windows.$key
    $status = if ($w.observedMinutes -ge 0.9 * $w.requestedMinutes) {
        'prawie pelne okno'
    } else { 'niepelne okno' }
    '<tr><th>' + (Safe $key) + '</th><td>' +
        (Safe $w.samples) + '</td><td>' +
        (Safe $w.observedMinutes) + ' / ' +
        (Safe $w.requestedMinutes) + ' min</td><td>' +
        (NumberOrEmpty $w.minFreeMemoryMiB ' MiB') + '</td><td>' +
        (NumberOrEmpty $w.maxHostPrivateMiB ' MiB') + '</td><td>' +
        (NumberOrEmpty $w.maxWebviewPrivateMiB ' MiB') + '</td><td>' +
        (NumberOrEmpty $w.maxWebviewCpuCorePercent '% rdzenia') + '</td><td>' +
        (Safe $status) + '</td></tr>'
}
$since = ([DateTimeOffset]::Parse($report.firstUtc)).ToLocalTime().ToString('yyyy-MM-dd HH:mm:ss')
$until = ([DateTimeOffset]::Parse($report.lastUtc)).ToLocalTime().ToString('yyyy-MM-dd HH:mm:ss')
$stable = if ($null -eq $report.stableWebviewPrivateDeltaMiB) {
    'Brak dwoch porownywalnych probek'
} else {
    [string]$report.stableWebviewPrivateDeltaMiB + ' MiB przez ' +
        [string]$report.stableWebviewDurationMinutes + ' min'
}
$html = @'
<!doctype html>
<html lang="pl"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>Open Browser Lite - diagnostyka zasobow</title>
<style>
body{font:15px system-ui,sans-serif;margin:2rem auto;max-width:1200px;padding:0 1rem;color:#17212b;background:#f5f7fb}
h1{font-size:1.65rem}table{border-collapse:collapse;width:100%;background:white}
th,td{border-bottom:1px solid #dce2ec;padding:12px;text-align:left}
thead th{background:#e8eef8}th{font-weight:600}
.panel{background:white;border:1px solid #dce2ec;border-radius:12px;padding:1rem;margin:1rem 0}
small{color:#4e5969}.scroll{overflow:auto}
</style></head><body><h1>Open Browser Lite: diagnostyka zasobow</h1>
'@
$html += '<div class="panel"><p>Przedzial danych: <strong>' + (Safe $since) +
    '</strong> do <strong>' + (Safe $until) + '</strong></p><p>Probek: ' +
    (Safe $report.sampleCount) + ', presja RAM: <strong>' +
    (Safe $report.pressure) + '</strong></p></div>'
$html += '<div class="panel scroll"><h2>Trendy</h2><table><thead><tr>' +
    '<th>Okno</th><th>Probki</th><th>Pokrycie</th><th>Wolny RAM min</th>' +
    '<th>Host RAM max</th><th>WebView2 RAM max</th><th>WebView2 CPU max</th>' +
    '<th>Jakosc</th></tr></thead><tbody>' + ($rows -join [Environment]::NewLine) +
    '</tbody></table></div>'
$html += '<div class="panel"><h2>Stabilna seria WebView2</h2><p>' +
    (Safe $stable) + '</p><p>Ciagly zestaw PID: ' +
    (Safe $report.stableWebviewSampleCount) + ' probek. Zmiana PID/odswiezenie: ' +
    (Safe $report.hasWebviewChurnOrRestart) + '.</p><small>Surowe sumy z okresow po restartach nie sa dowodem wycieku pamieci.</small></div>'
$html += '<small>Raport lokalny, tylko zagregowane liczniki procesow. Nie zawiera czatow, cookies, tokenow ani historii URL.</small></body></html>'
$dir = Split-Path -Parent $OutputPath
if (-not (Test-Path -LiteralPath $dir -PathType Container)) {
    throw 'Output directory does not exist'
}
$tmp = $OutputPath + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
try {
    [IO.File]::WriteAllText($tmp, $html, [Text.UTF8Encoding]::new($false))
    Move-Item -LiteralPath $tmp -Destination $OutputPath -Force -ErrorAction Stop
} finally {
    if (Test-Path -LiteralPath $tmp) { Remove-Item -LiteralPath $tmp -Force }
}
Write-Output ('DASHBOARD_CREATED ' + $OutputPath)
