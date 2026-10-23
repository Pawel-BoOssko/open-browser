# Isolated release-guard regression: no process, profile or live release state changes.
param([string]$Repository=(Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference='Stop'
$manager=Join-Path $Repository 'scripts\lite-release-manager.ps1'
$source=Get-Content $manager -Raw -Encoding UTF8
$match=[regex]::Match($source,
 '(?s)function HasRecentTrialCleanClose\(\$s,\[string\]\$eventsFolder\)\{.*?\n\}\r?\n(?=function SetTarget)')
if(-not $match.Success){throw 'Clean-close guard missing from release manager'}
. ([scriptblock]::Create($match.Value))
$root=Join-Path $env:TEMP ('OBLiteCleanCloseTest_'+[guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($root)|Out-Null
try{
 $now=[datetime]::UtcNow
 $file=Join-Path $root ('lite-events-'+$now.ToString('yyyy-MM-dd')+'.jsonl')
 $sample=[pscustomobject]@{candidatePid=13579;lastHealthyUtc=$now.AddMinutes(-3).ToString('o')}
 function WriteEvent([datetime]$time,[int]$processId,[string]$kind){
  $line=@{utc=$time.ToString('o');pid=$processId;eventName=$kind}|ConvertTo-Json -Compress
  [IO.File]::WriteAllText($file,$line+[Environment]::NewLine,[Text.UTF8Encoding]::new($false))
 }
 WriteEvent ($now.AddMinutes(-2)) 13579 'window_closing'
 if(-not (HasRecentTrialCleanClose $sample $root)){throw 'Valid graceful close rejected'}
 $sample.candidatePid=13
 if(HasRecentTrialCleanClose $sample $root){throw 'Foreign PID accepted'}
 $sample.candidatePid=13579
 $sample.lastHealthyUtc=$now.AddMinutes(-1).ToString('o')
 if(HasRecentTrialCleanClose $sample $root){throw 'Close before last healthy accepted'}
 $sample.lastHealthyUtc=$now.AddMinutes(-30).ToString('o')
 WriteEvent ($now.AddMinutes(-11)) 13579 'window_closing'
 if(HasRecentTrialCleanClose $sample $root){throw 'Stale close accepted'}
 WriteEvent ($now.AddMinutes(-2)) 13579 'worker_exception'
 if(HasRecentTrialCleanClose $sample $root){throw 'Nonclose event accepted'}
 Remove-Item -LiteralPath $file -Force
 if(HasRecentTrialCleanClose $sample $root){throw 'Missing evidence accepted'}
 Write-Output 'RELEASE_CLEAN_CLOSE_6_OF_6_PASS'
}catch{
 Write-Error ('RELEASE_CLEAN_CLOSE_FAILED '+$_.Exception.Message)
 exit 2
}finally{
 Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
}
