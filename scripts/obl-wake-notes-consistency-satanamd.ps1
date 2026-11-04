param([int]$Take=6)
$ErrorActionPreference='Stop'
if($env:COMPUTERNAME -ne 'SATANAMD'){throw 'WRONG_HOST'}
$diag=Join-Path $env:LOCALAPPDATA 'OpenBrowser\ChatGPT-Lite\diagnostics'
$journal=Join-Path $diag 'wake-lifecycle.jsonl'
$eventLog=Join-Path $diag ('lite-events-'+(Get-Date -Format 'yyyy-MM-dd')+'.jsonl')
$events=@(Get-Content $eventLog -Tail 6000 -Encoding UTF8 | ForEach-Object {try{$_|ConvertFrom-Json}catch{}} | Where-Object {$_.eventName -in @('multi_wake_notes_snapshot','multi_wake_notes_submitted')})
$rows=@(Get-Content $journal -Tail 2200 -Encoding UTF8 | ForEach-Object {try{$_|ConvertFrom-Json}catch{}} | Where-Object {$_.actor -eq 'engine' -and $_.receipt})
$receipts=@($rows | Where-Object {$_.stage -eq 'attempt_reserved'} | Select-Object -Last $Take)
$results=@(foreach($r in $receipts){
 $key=[string]$r.receipt
 $seq=[long]$r.wakeSequence
 $j=@($rows|Where-Object {$_.receipt -eq $key -and $_.wakeSequence -eq $seq})
 $prepared=@($events|Where-Object {$_.eventName -eq 'multi_wake_notes_snapshot' -and $_.details.receipt -eq $key -and $_.details.wakeSequence -eq $seq})
 $submitted=@($events|Where-Object {$_.eventName -eq 'multi_wake_notes_submitted' -and $_.details.receipt -eq $key -and $_.details.wakeSequence -eq $seq})
 $clicked=@($j|Where-Object stage -eq 'submission_attempted')
 $marker=@($j|Where-Object stage -eq 'notes_submitted')
 $dom=@($j|Where-Object stage -eq 'receipt_confirmed')
 $t2=@($j|Where-Object stage -eq 'receipt_resolved')
 $mismatch=$false
 if($submitted.Count -gt 0){
  if($prepared.Count -ne 1 -or $submitted.Count -ne 1 -or $marker.Count -ne 1 -or $clicked.Count -ne 1){$mismatch=$true}
  elseif([int]$prepared[0].details.includedChars -ne [int]$submitted[0].details.includedChars -or [string]$prepared[0].details.includedSha256 -cne [string]$submitted[0].details.includedSha256){$mismatch=$true}
 }
 [pscustomobject]@{sequence=$seq;receipt=$key;prepared=$prepared.Count;submitted=$submitted.Count;engineClick=$clicked.Count;engineNotesMarker=$marker.Count;includedChars=if($prepared.Count){$prepared[0].details.includedChars}else{$null};confirmed=[bool]($dom.Count -or $t2.Count);mismatch=$mismatch}
})
$summary=[pscustomobject]@{
 host=$env:COMPUTERNAME; checked=$results.Count
 instrumented=@($results|Where-Object {$_.submitted -gt 0}).Count
 mismatches=@($results|Where-Object mismatch).Count
 submittedWithoutClick=@($results|Where-Object {$_.submitted -gt 0 -and $_.engineClick -eq 0}).Count
}
$summary|ConvertTo-Json -Compress
$results|Sort-Object sequence|ForEach-Object{$_|ConvertTo-Json -Compress}
