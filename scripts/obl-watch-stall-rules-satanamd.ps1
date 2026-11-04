# OBL P0-03 deterministic recovery decision, no process or wake sending actions.
function Test-OBLRepeatedDeferral {
 param([object[]]$Events,[DateTimeOffset]$LastAckUtc,[DateTimeOffset]$NowUtc)
 $stallReasons=@('generating','draft_present','assistant_not_complete','no_composer','history_not_stable','answer_not_stable')
 $deferred=@($Events|Where-Object {
   $_.stage -eq 'deferred' -and [DateTimeOffset]::Parse([string]$_.utc) -gt $LastAckUtc
 }|Select-Object -Last 3)
 if($deferred.Count -lt 3){return $false}
 foreach($e in $deferred){if([string]$e.result -notin $stallReasons){return $false}}
 $first=[DateTimeOffset]::Parse([string]$deferred[0].utc)
 $last=[DateTimeOffset]::Parse([string]$deferred[2].utc)
 if(($last-$first).TotalSeconds -lt 120){return $false}
 if(($NowUtc-$last).TotalMinutes -gt 10){return $false}
 $newAttempt=@($Events|Where-Object {
   $_.stage -in @('submission_attempted','receipt_uncertain') -and
   [DateTimeOffset]::Parse([string]$_.utc) -gt $last -and
   ($NowUtc-[DateTimeOffset]::Parse([string]$_.utc)).TotalMinutes -lt 10
 })
 if($newAttempt.Count -gt 0){return $false}
 return $true
}
