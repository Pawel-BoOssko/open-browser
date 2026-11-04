param(
    [Parameter(Mandatory=$true)][ValidateSet('start','stop','done','scan','ready')][string]$Action,
    [string]$Task,
    [string]$Wake,
    [ValidateRange(15,240)][int]$StaleMinutes=45,
    [string]$Evidence,
    [string]$AsOfUtc
)
$ErrorActionPreference='Stop'
$bd='D:\Apps\Beads\node_modules\.bin\bd.cmd'
$root='D:\projects\open-browser'
if(-not(Test-Path $bd)){throw "Beads executable missing: $bd"}
function Invoke-Bd([string[]]$Arguments){
    $ErrorActionPreference='Continue'
    $result = & $bd -C $root --actor obl-lead @Arguments 2>&1
    $code=$LASTEXITCODE
    $ErrorActionPreference='Stop'
    if($code -ne 0){throw ("Beads command failed ["+$code+"]: "+($Arguments -join ' ')+" OUTPUT="+($result|Out-String))}
    return @($result | Where-Object {$_ -is [string] -and $_ -notmatch '^(Thanks for using bd|Quick heads-up|To opt out)'})
}
function Issue([string]$key){return (Invoke-Bd @('show',$key,'--json') | Out-String | ConvertFrom-Json)}
function LastSession([string]$key){
    $events=Invoke-Bd @('comments',$key,'--json') | Out-String | ConvertFrom-Json
    $last=$null
    foreach($event in @($events)){
        if([string]$event.text -match '^(START|STOP|DONE) (OB-WAKE-\d{6,}|OBL-\d{6,}(?:\.\d+\.\d{6}|(?:\.\d+)?-A\d{2,}-\d{6}))(?: .*)?$'){
            $last=[pscustomobject]@{Verb=$Matches[1];Wake=$Matches[2];Text=[string]$event.text;At=[DateTimeOffset]::Parse([string]$event.created_at)}
        }
    }
    return $last
}
if($Action -eq 'ready'){
    $available=Invoke-Bd @('ready','--json') | Out-String | ConvertFrom-Json
    @($available | Where-Object {$_.issue_type -ne 'epic'}) | ConvertTo-Json -Depth 12
    exit 0
}
if($Action -eq 'scan'){
    $now=if($AsOfUtc){[DateTimeOffset]::Parse($AsOfUtc)}else{[DateTimeOffset]::UtcNow}
    $issues=Invoke-Bd @('list','--status','in_progress','--json') | Out-String | ConvertFrom-Json
    foreach($item in @($issues)){
        if(-not $item.id){continue}
        $last=LastSession $item.id
        if($null -eq $last){
            Write-Output ($item.id+' | NO_SESSION | not an abandoned wake; verify manual work')
            continue
        }
        $age=[Math]::Max(0,[int]($now-$last.At).TotalMinutes)
        $state=if($last.Verb -eq 'STOP'){'NORMAL_STOP'}elseif($last.Verb -eq 'START' -and $age -lt $StaleMinutes){'ACTIVE_OR_RECENT'}elseif($last.Verb -eq 'START'){'REVIEW_STALE'}else{'CHECK_DONE'}
        Write-Output ($item.id+' | '+$state+' | '+$last.Text+' | utc='+$last.At.ToUniversalTime().ToString('o')+' | ageMinutes='+$age)
    }
    exit 0
}
if([string]::IsNullOrWhiteSpace($Task)){throw 'Missing -Task ID'}
if($Wake -notmatch '^(?:OB-WAKE-\d{6,}|OBL-\d{6,}(?:\.\d+\.\d{6}|(?:\.\d+)?-A\d{2,}-\d{6}))$'){throw 'Invalid wake ID: use the actual minimal OB-WAKE-000240, or legacy complete OBL-000050-A01-205947; never invent a timestamp'}
$issue=Issue $Task
if($null -eq $issue){throw "Issue not found: $Task"}
$last=LastSession $Task
switch($Action){
    'start' {
        if($issue.status -eq 'closed'){throw "Cannot START closed issue: $Task"}
        if($null -ne $last -and $last.Verb -eq 'START' -and $last.Wake -eq $Wake){
            Write-Output ("START_ALREADY_RECORDED "+$Task+" "+$Wake)
            break
        }
        if($null -ne $last -and $last.Verb -eq 'START' -and $last.Wake -ne $Wake){
            $age=([DateTimeOffset]::UtcNow-$last.At).TotalMinutes
            if($age -lt $StaleMinutes){throw ("Previous wake "+$last.Wake+" still active/recent "+[int]$age+"m; inspect before taking over: "+$Task)}
            Write-Output ('RECOVERY_CHECK_REQUIRED '+$Task+' previous='+$last.Wake+' ageMinutes='+[int]$age+'; inspect checkpoint and actual artifacts before work')
        }
        Invoke-Bd @('update',$Task,'--status','in_progress','--assignee','obl-lead') | Out-Null
        Invoke-Bd @('comments','add',$Task,("START "+$Wake)) | Out-Null
        Write-Output ("START "+$Task+" "+$Wake)
    }
    'stop' {
        if($null -ne $last -and $last.Verb -eq 'STOP' -and $last.Wake -eq $Wake){
            Write-Output ("STOP_ALREADY_RECORDED "+$Task+" "+$Wake)
            break
        }
        if($null -eq $last -or $last.Verb -ne 'START' -or $last.Wake -ne $Wake){throw ("STOP requires prior START for same wake and task: "+$Task+" "+$Wake)}
        Invoke-Bd @('comments','add',$Task,("STOP "+$Wake)) | Out-Null
        Write-Output ("STOP "+$Task+" "+$Wake)
    }
    'done' {
        if([string]::IsNullOrWhiteSpace($Evidence)){throw 'DONE requires -Evidence with actual verified result'}
        if($issue.status -eq 'closed'){Write-Output ('DONE_ALREADY_CLOSED '+$Task);break}
        Invoke-Bd @('close',$Task,'--reason',("Verified "+$Wake+": "+$Evidence)) | Out-Null
        Invoke-Bd @('comments','add',$Task,("DONE "+$Wake+" verified="+$Evidence)) | Out-Null
        Write-Output ("DONE "+$Task+" "+$Wake)
    }
}
