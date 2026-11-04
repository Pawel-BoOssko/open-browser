$ErrorActionPreference='Stop'
if($env:COMPUTERNAME -ne 'SATANAMD'){throw 'WRONG_HOST'}
. 'D:\projects\open-browser\scripts\obl-watch-stall-rules-satanamd.ps1'
$start=[DateTimeOffset]::Parse('2026-10-28T13:00:00Z')
function E([string]$stage,[string]$reason,[int]$sec) {
 [pscustomobject]@{stage=$stage;result=$reason;utc=$start.AddSeconds($sec).ToString('o')}
}
$rows=@((E 'receipt_confirmed' 'visible_ack' 0),(E 'deferred' 'generating' 10),(E 'deferred' 'no_composer' 90),(E 'deferred' 'draft_present' 170))
if(!(Test-OBLRepeatedDeferral $rows $start $start.AddSeconds(180))){throw 'NOT_DETECTED'}
if(Test-OBLRepeatedDeferral @($rows|Select-Object -First 3) $start $start.AddSeconds(180)){throw 'TWO_FALSE_POSITIVE'}
$recent=@($rows)+(E 'submission_attempted' 'click_invoked' 175)
if(Test-OBLRepeatedDeferral $recent $start $start.AddSeconds(180)){throw 'RECENT_SEND_NOT_FENCED'}
$rate=@((E 'deferred' 'generating' 10),(E 'deferred' 'backend_http_429' 90),(E 'deferred' 'generating' 170))
if(Test-OBLRepeatedDeferral $rate $start $start.AddSeconds(180)){throw 'HTTP429_FALSE_RECOVERY'}
$fast=@((E 'deferred' 'generating' 150),(E 'deferred' 'generating' 160),(E 'deferred' 'generating' 170))
if(Test-OBLRepeatedDeferral $fast $start $start.AddSeconds(180)){throw 'WINDOW_TOO_SHORT'}
'OBL_STALL_5_TESTS_PASS'
