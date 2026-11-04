param(
  [int]$Hours = 24,
  [switch]$WriteReport
)
$ErrorActionPreference='Stop'
if($env:COMPUTERNAME -ne 'SATANAMD'){throw ('WRONG_HOST '+$env:COMPUTERNAME)}
if($Hours -lt 1 -or $Hours -gt 168){throw 'INVALID_HOURS'}
$diag=Join-Path $env:LOCALAPPDATA 'OpenBrowser\ChatGPT-Lite\diagnostics'
$path=Join-Path $diag 'wake-lifecycle.jsonl'
if(-not(Test-Path $path)){throw 'WAKE_JOURNAL_MISSING'}
$from=[DateTimeOffset]::UtcNow.AddHours(-$Hours)
$lines=New-Object 'System.Collections.Generic.List[object]'
foreach($line in [IO.File]::ReadLines($path)){
 try{
   $o=ConvertFrom-Json -InputObject $line
   if($null -eq $o.utc){continue}
   $t=[DateTimeOffset]::Parse([string]$o.utc)
   if($t -lt $from){continue}
   if($null -eq $o.receipt -or [string]$o.receipt -notmatch '^[a-f0-9]{32}$'){continue}
   $lines.Add([pscustomobject]@{receipt=[string]$o.receipt;sequence=$o.wakeSequence;actor=[string]$o.actor;stage=[string]$o.stage;utc=$t;result=[string]$o.result})
 }catch{}
}
$groups=@($lines | Group-Object receipt)
$records=foreach($group in $groups){
 $events=@($group.Group|Sort-Object utc)
 $stage=@($events | ForEach-Object {$_.stage})
 $reserved=@($events|Where-Object {$_.actor -eq 'engine' -and $_.stage -eq 'attempt_reserved'})
 $submitted=@($events|Where-Object {$_.actor -eq 'engine' -and $_.stage -eq 'submission_attempted'})
 $notesSubmitted=@($events|Where-Object {$_.actor -eq 'engine' -and $_.stage -eq 'notes_submitted'})
 $confirmed=@($events|Where-Object {$_.actor -eq 'engine' -and $_.stage -eq 'receipt_confirmed'})
 $uncertain=@($events|Where-Object {$_.actor -eq 'engine' -and $_.stage -eq 'receipt_uncertain'})
 $started=@($events|Where-Object {$_.actor -eq 'chat' -and $_.stage -eq 'chat_started'})
 $finished=@($events|Where-Object {$_.actor -eq 'chat' -and $_.stage -eq 'chat_finished'})
 [pscustomobject]@{
   wakeSequence=$events[0].sequence
   receipt=$group.Name
   attempts=$reserved.Count
   submitted=$submitted.Count
   notesSubmitted=$notesSubmitted.Count
   domAck=$confirmed.Count
   domUncertain=$uncertain.Count
   # Independent chat-written T2/T3 confirms receipt even when engine send or DOM logging is incomplete.
   # Preserve the raw submitted/DOM counts; never manufacture a visible ACK.
   finalDelivery=if($confirmed.Count -gt 0) {'confirmed_dom'}
       elseif($started.Count -gt 0) {'confirmed_chat'}
       elseif($finished.Count -gt 0) {'confirmed_chat_finished'}
       elseif($uncertain.Count -gt 0) {'unresolved'}
       elseif($submitted.Count -gt 0) {'unproven'}
       elseif($reserved.Count -gt 0) {'reserved_only'} else {'not_attempted'}
   chatStart=$started.Count
   chatFinish=$finished.Count
   crossSourceReconciled=($uncertain.Count -gt 0 -and ($started.Count -gt 0 -or $finished.Count -gt 0))
   chatEvidenceWithoutDom=($confirmed.Count -eq 0 -and ($started.Count -gt 0 -or $finished.Count -gt 0))
   chatEvidenceWithoutEngineSubmit=($submitted.Count -eq 0 -and ($started.Count -gt 0 -or $finished.Count -gt 0))
   duplicateAttempt=($reserved.Count -gt 1 -or $submitted.Count -gt 1)
 }
}
$records=@($records)
$summary=[pscustomobject]@{
 schema=1
 host=$env:COMPUTERNAME
 generatedUtc=[DateTimeOffset]::UtcNow.ToString('o')
 windowHours=$Hours
 receipts=$records.Count
 attempts=@($records |Where-Object attempts -gt 0).Count
 domConfirmed=@($records|Where-Object domAck -gt 0).Count
 uncertain=@($records|Where-Object domUncertain -gt 0).Count
 uncertainWithChatEvidence=@($records|Where-Object crossSourceReconciled).Count
 notesSubmittedReceipts=@($records|Where-Object notesSubmitted -gt 0).Count
 historicalOrUninstrumentedReceipts=@($records|Where-Object { $_.submitted -gt 0 -and $_.notesSubmitted -eq 0 }).Count
 effectiveConfirmed=@($records|Where-Object {$_.finalDelivery -in @('confirmed_dom','confirmed_chat','confirmed_chat_finished')}).Count
 confirmedByChat=@($records|Where-Object {$_.finalDelivery -in @('confirmed_chat','confirmed_chat_finished')}).Count
 confirmedByChatFinished=@($records|Where-Object finalDelivery -eq 'confirmed_chat_finished').Count
 chatEvidenceWithoutDom=@($records|Where-Object chatEvidenceWithoutDom).Count
 chatEvidenceWithoutEngineSubmit=@($records|Where-Object chatEvidenceWithoutEngineSubmit).Count
 unproven=@($records|Where-Object finalDelivery -eq 'unproven').Count
 reservedOnly=@($records|Where-Object finalDelivery -eq 'reserved_only').Count
 unresolved=@($records|Where-Object finalDelivery -eq 'unresolved').Count
 chatStarted=@($records|Where-Object chatStart -gt 0).Count
 chatFinished=@($records|Where-Object chatFinish -gt 0).Count
 duplicateReceiptAttempts=@($records|Where-Object duplicateAttempt).Count
 target100Complete=(@($records|Where-Object attempts -gt 0).Count -ge 100)
}
Write-Output ($summary|ConvertTo-Json -Compress)
foreach($item in $records|Sort-Object wakeSequence){
 Write-Output ("receipt={0} seq={1} attempts={2} sent={3} DOM={4} historicalDomTimeouts={5} chatT2={6} chatT3={7} finalDelivery={10} reconciled={8} duplicate={9}" -f $item.receipt,$item.wakeSequence,$item.attempts,$item.submitted,$item.domAck,$item.domUncertain,$item.chatStart,$item.chatFinish,$item.crossSourceReconciled,$item.duplicateAttempt,$item.finalDelivery)
}
if($WriteReport){
 $out=Join-Path $diag ('obl-wake-soak-'+(Get-Date -Format 'yyyy-MM-dd')+'.json')
 [IO.File]::WriteAllText($out,($summary|ConvertTo-Json -Depth 4),[Text.UTF8Encoding]::new($false))
 Write-Output ('REPORT_SAVED='+$out)
}
