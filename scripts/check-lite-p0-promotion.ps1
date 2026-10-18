param(
    [string]$Repository = (Split-Path -Parent $PSScriptRoot),
    [string]$StageDirectory = ''
)
$ErrorActionPreference = 'Stop'
$checks = @(
    'lite-resume-agent.test.cjs',
    'lite-resume-send-race.test.cjs',
    'lite-high-guard.test.cjs',
    'lite-response-signals.test.cjs',
    'lite-dom-default-contract.test.cjs',
    'lite-dom-hard-prune.test.cjs'
)
$failures = [System.Collections.Generic.List[string]]::new()
foreach ($name in $checks) {
    $file = Join-Path (Join-Path $Repository 'tests') $name
    if (-not (Test-Path -LiteralPath $file)) {
        $failures.Add($name + ': missing')
        continue
    }
    & node $file 2>$null | Out-Null
    if ($LASTEXITCODE -ne 0) {
        $failures.Add($name + ': exit=' + $LASTEXITCODE)
        Write-Output ('FAIL ' + $name)
    } else {
        Write-Output ('PASS ' + $name)
    }
}
if ($StageDirectory) {
    $manifest = Join-Path $StageDirectory 'openbrowser-release-manifest.json'
    if (-not (Test-Path -LiteralPath $manifest)) {
        $failures.Add('release manifest missing')
    } else {
        try {
            & (Join-Path $Repository 'scripts\verify-lite-release-manifest.ps1') -ManifestPath $manifest | Out-Null
            Write-Output 'PASS release manifest'
        } catch {
            $failures.Add('release manifest invalid: ' + $_.Exception.GetType().Name)
        }
    }
}
if ($failures.Count -gt 0) {
    Write-Output ('PROMOTION_BLOCKED: ' + ($failures -join '; '))
    exit 2
}
Write-Output 'PROMOTION_GATES_PASS'
exit 0
