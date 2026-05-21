param(
    [switch]$Strict
)

$ErrorActionPreference = 'Stop'

$root = Resolve-Path (Join-Path $PSScriptRoot '..')
Set-Location $root

$required = @(
    'docs/agent/READ_BEFORE_WORK_new.md',
    'docs/agent/current-state_new.md',
    'docs/agent/development-rules_new.md',
    'docs/agent/coding-invariants_new.md',
    'docs/agent/documentation-update-checklist_new.md',
    'docs/agent/commit-rules_new.md',
    'docs/feedback/field-notes_new.md'
)

Write-Output 'OpenBrowser/OpenBridge documentation gate'
Write-Output '========================================='
Write-Output 'Before recommending or performing COMMIT, developer agent must apply:'
Write-Output 'docs/agent/READ_BEFORE_WORK_new.md'
Write-Output ''
Write-Output 'Required checks:'
Write-Output '- documentation impact checked'
Write-Output '- coding invariants checked'
Write-Output '- field notes checked'
Write-Output '- changed docs updated if behavior changed'
Write-Output ''

$missing = @()
foreach ($file in $required) {
    if (!(Test-Path $file)) {
        $missing += $file
    }
}

if ($missing.Count -gt 0) {
    Write-Output 'Missing required documentation files:'
    $missing | ForEach-Object { Write-Output - $_ }
    exit 1
}

Write-Output 'Required documentation files exist.'
Write-Output ''
Write-Output 'Developer agent final report must include:'
Write-Output '- Documentation impact: yes/no'
Write-Output '- Documentation files updated: list or none'
Write-Output '- Invariants checked: yes'
Write-Output '- Field notes checked: yes'
Write-Output '- Commit allowed by commit-rules: yes/no'
Write-Output ''

if ($Strict) {
    Write-Output 'Strict mode enabled. No additional acknowledgement mechanism implemented yet.'
}

exit 0