param(
    [string]$Audit = (Join-Path $PSScriptRoot '..\scripts\audit-lite-release-state.ps1'),
    [string]$Exe = (Join-Path $PSScriptRoot '..\src\BridgeBrowserAlpha0\bin\Debug\net8.0-windows\BridgeBrowserAlpha0.exe')
)
$ErrorActionPreference = 'Stop'
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('ob-audit-test-' + [guid]::NewGuid().ToString('N'))
$checks = 0
function Assert-True([bool]$Value, [string]$Name) {
    if (-not $Value) { throw "FAIL: $Name" }
    $script:checks++
}
try {
    [void](New-Item -ItemType Directory -Path $testRoot)
    $empty = (& $Audit -ProgramsRoot $testRoot -SkipSystemProbes | ConvertFrom-Json)
    Assert-True ($empty.schema -eq 1) 'schema'
    Assert-True ($empty.probeMode -eq 'offline') 'offline mode'
    Assert-True ($empty.installationCount -eq 0) 'empty root'
    Assert-True (@($empty.live).Count -eq 0) 'offline no live'
    Assert-True ($empty.action -eq 'read_only_no_rollout') 'no rollout'

    $broken = Join-Path $testRoot 'OpenBrowserChatGPTLite_broken'
    [void](New-Item -ItemType Directory -Path $broken)
    $missing = (& $Audit -ProgramsRoot $testRoot -SkipSystemProbes | ConvertFrom-Json)
    Assert-True ($missing.installationCount -eq 1) 'missing exe folder discovered'
    Assert-True (-not $missing.installations[0].identity.exists) 'missing exe marked missing'
    Assert-True (-not $missing.installations[0].identity.stamped) 'missing exe no stamp'

    if (-not (Test-Path -LiteralPath $Exe -PathType Leaf)) {
        throw "Test prerequisite missing: $Exe"
    }
    $good = Join-Path $testRoot 'OpenBrowserChatGPTLite_versioned'
    [void](New-Item -ItemType Directory -Path $good)
    Copy-Item -LiteralPath $Exe -Destination (Join-Path $good 'BridgeBrowserAlpha0.exe')
    $versioned = (& $Audit -ProgramsRoot $testRoot -SkipSystemProbes | ConvertFrom-Json)
    $row = @($versioned.installations | Where-Object { $_.folder -eq 'OpenBrowserChatGPTLite_versioned' })[0]
    Assert-True ($versioned.installationCount -eq 2) 'two installations'
    Assert-True ($row.identity.exists) 'exe exists'
    Assert-True ($row.identity.stamped) 'build timestamp embedded'
    Assert-True ($row.identity.builtAtUtc -match '^20\d\d-') 'ISO build time'
    Assert-True ($row.identity.productVersion -match '^1\.60\.0\+') 'expected version'
    Assert-True ($versioned.memoryPressure -eq 'unknown') 'offline no fake RAM'
    "PASS $checks release-audit checks"
}
finally {
    if (Test-Path -LiteralPath $testRoot) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}
