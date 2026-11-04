$ErrorActionPreference='Stop'
if($env:COMPUTERNAME -ne 'SATANAMD'){throw 'WRONG_HOST'}
$root='D:\projects\open-browser'
$watch=[IO.File]::ReadAllText("$root\scripts\obl-watch-recover-satanamd.ps1")
$old=[IO.File]::ReadAllText("$root\scripts\obl-promote-on-idle-17297.ps1")
$rules=[IO.File]::ReadAllText("$root\AGENTS.md")
$matched=[regex]::Match($watch,'(?s)\$conversationalReasons=@\((.*?)\)')
if(-not $matched.Success){throw 'NO_WATCH_REASONS'}
if($matched.Groups[1].Value -match 'generating|draft_present|assistant_not_complete|history_not_stable|answer_not_stable'){throw 'RESTART_BLOCKED_BY_GENERATION'}
if($old -match 'no_confirmed_generation_end|generation_stop_visible|draft_present_or_unknown'){throw 'LEGACY_IDLE_GATE_STILL_ACTIVE'}
if($rules -notmatch 'GENERATING DOES NOT BLOCK RESTART'){throw 'OWNER_RULE_NOT_IN_AGENTS'}
if(-not(Test-Path "$root\scripts\obl-force-deploy-satanamd.ps1")){throw 'FORCE_DEPLOY_MISSING'}
'OWNER_RESTART_POLICY_SELFTEST_PASS'
