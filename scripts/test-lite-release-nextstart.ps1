# Isolation test: no real browser/shortcut/release state is touched.
param([string]$Repository='D:\projects\open-browser')
$ErrorActionPreference='Stop'
$manager=Join-Path $Repository 'scripts\lite-release-manager.ps1'
$candidate=Join-Path $env:TEMP 'OpenBrowserLiteCandidate_v1.72.75\BridgeBrowserAlpha0.exe'
if(-not(Test-Path -LiteralPath $candidate)){throw 'Test candidate missing'}
if(@(Get-CimInstance Win32_Process -Filter "name='BridgeBrowserAlpha0.exe'").Count -ne 0){throw 'Only run on idle SatanAMD'}
$root=Join-Path $env:TEMP ('OBLiteNextStart_'+[guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($root)|Out-Null
try {
 $old=Join-Path $root 'previous.exe'
 Copy-Item -LiteralPath $candidate -Destination $old
 $shortcut=Join-Path $root 'test-link.lnk'
 $shell=New-Object -ComObject WScript.Shell
 $link=$shell.CreateShortcut($shortcut);$link.TargetPath=$old;$link.Save()
 $alive=Join-Path $root 'old-running.flag'
 [IO.File]::WriteAllText($alive,'yes')
 $text=Get-Content -LiteralPath $manager -Raw -Encoding UTF8
 $text=$text.Replace(
  '$root=Join-Path $env:LOCALAPPDATA ''OpenBrowser\ChatGPT-Lite\diagnostics''',
  ('$root='''+$root+''''))
 $text=$text.Replace(
  '$shortcut=Join-Path $env:APPDATA ''Microsoft\Windows\Start Menu\Programs\Open Browser - ChatGPT Lite.lnk''',
  ('$shortcut='''+$shortcut+''''))
 $mock=@'
function Get-CimInstance {
 param([string]$ClassName,[string]$Filter)
 if(Test-Path -LiteralPath '__ALIVE__'){
  return [pscustomobject]@{ProcessId=13579;ExecutablePath='__OLD__'}
 }
 return @()
}
function Get-Process {
 param([int]$Id,[string]$ErrorAction)
 if($Id -eq 13579 -and (Test-Path -LiteralPath '__ALIVE__')){
  return [pscustomobject]@{Id=13579;Responding=$true}
 }
 return $null
}
'@
 $mock=$mock.Replace('__ALIVE__',$alive).Replace('__OLD__',$old)
 $text=$text.Replace('$ErrorActionPreference=''Stop''',
  ('$ErrorActionPreference=''Stop'''+[Environment]::NewLine+$mock))
 if($text -notmatch '\$root=''[A-Z]:\\' -or
    $text -notmatch '\$shortcut=''[A-Z]:\\' -or
    $text -notmatch 'function Get-CimInstance'){throw 'Isolation setup failed'}
 $sandbox=Join-Path $root 'manager-isolated.ps1'
 [IO.File]::WriteAllText($sandbox,$text,[Text.UTF8Encoding]::new($false))
 $sha=[BitConverter]::ToString([Security.Cryptography.SHA256]::Create().ComputeHash(
  [IO.File]::ReadAllBytes($candidate))).Replace('-','').ToLowerInvariant()
 & $sandbox -Mode QueueNextStart -CandidateExe $candidate -ExpectedSha $sha
 if($LASTEXITCODE -ne 0){throw "queue exit $LASTEXITCODE"}
 $s=Get-Content (Join-Path $root 'lite-release-state.json') -Raw|ConvertFrom-Json
 if($s.phase -ne 'queued' -or $s.previousExe -ne $old -or
    $s.previousPid -ne 13579 -or $s.candidateSha -ne $sha -or
    $shell.CreateShortcut($shortcut).TargetPath -ne $candidate){throw 'Queue invariants failed'}
 & $sandbox -Mode Watchdog
 if($LASTEXITCODE -ne 0){throw 'watchdog while old running failed'}
 $s=Get-Content (Join-Path $root 'lite-release-state.json') -Raw|ConvertFrom-Json
 if($s.phase -ne 'queued' -or $s.waitingToStartSinceUtc){throw 'Old process interrupted'}
 # Second scenario: the same running old executable is a healthy active trial.
 # A newer candidate must be queued without interrupting it; prior trial
 # metadata, including its earlier rollback, must be archived.
 $s.phase='trial';$s.candidateExe=$old;$s.candidatePid=13579
 $s.lastHealthyUtc=[datetime]::UtcNow.ToString('o')
 $s |ConvertTo-Json -Depth 8|Set-Content (Join-Path $root 'lite-release-state.json')
 $link=$shell.CreateShortcut($shortcut);$link.TargetPath=$old;$link.Save()
 & $sandbox -Mode QueueNextStart -CandidateExe $candidate -ExpectedSha $sha
 if($LASTEXITCODE -ne 0){throw 'Queue over healthy trial failed'}
 $s=Get-Content (Join-Path $root 'lite-release-state.json') -Raw|ConvertFrom-Json
 if($s.phase -ne 'queued' -or $s.previousExe -ne $old -or
    @((Get-ChildItem $root -Filter 'lite-release-prior-*.json')).Count -ne 1){
   throw 'Trial queue or archive failed'
 }
 Remove-Item -LiteralPath $alive -Force
 $now=[datetime]::UtcNow
 $json=@{utc=$now.ToString('o');pid=13579;eventName='window_closing'}|ConvertTo-Json -Compress
 [IO.File]::WriteAllText(
  (Join-Path $root ('lite-events-'+$now.ToString('yyyy-MM-dd')+'.jsonl')),
  $json+[Environment]::NewLine)
 & $sandbox -Mode Watchdog
 if($LASTEXITCODE -ne 0){throw 'clean exit watchdog failed'}
 $s=Get-Content (Join-Path $root 'lite-release-state.json') -Raw|ConvertFrom-Json
 if($s.phase -ne 'queued' -or -not $s.waitingToStartSinceUtc){throw 'Safe next-start not armed'}
 if(@(Get-CimInstance Win32_Process -Filter "name='BridgeBrowserAlpha0.exe'").Count -ne 0){throw 'Unexpected process started'}
 'QUEUED_NEXT_START_ISOLATED_3_STAGES_PASS'
} finally {try{Remove-Item -LiteralPath $root -Recurse -Force}catch{}}
