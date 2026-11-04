param(
 [ValidateSet('Audit','Apply')][string]$Mode='Audit',
 [ValidateRange(4,30)][int]$KeepTurns=12,
 [ValidateRange(30,3600)][int]$GraceSeconds=180,
 [string]$FixtureRoot='',
 [string]$FixtureUrl=''
)
$ErrorActionPreference='Stop'
if($env:COMPUTERNAME -ne 'SATANAMD'){throw 'WRONG_HOST'}
$local=Join-Path $env:LOCALAPPDATA 'OpenBrowser\ChatGPT-Lite'
$diag=Join-Path $local 'diagnostics'
$root=if($FixtureRoot){$FixtureRoot}else{Join-Path $local 'responses'}
$eLog=Join-Path $diag 'obl-archive-retention.jsonl'
$utf8=[Text.UTF8Encoding]::new($false)
function HashUrl([string]$url){
 $h=[Security.Cryptography.SHA256]::Create()
 try{return [BitConverter]::ToString($h.ComputeHash([Text.Encoding]::UTF8.GetBytes($url))).Replace('-','').ToLowerInvariant()}
 finally{$h.Dispose()}
}
function Audit([string]$step,$value){
 if($FixtureRoot){return}
 [IO.File]::AppendAllText($eLog,([ordered]@{utc=[DateTimeOffset]::UtcNow.ToString('o');mode=$Mode;step=$step;details=$value}|ConvertTo-Json -Compress -Depth 5)+[Environment]::NewLine,$utf8)
}
if(-not(Test-Path -LiteralPath $root -PathType Container)){Write-Output 'NO_ARCHIVE_ROOT';return}
if($FixtureRoot){
 $expected=Join-Path 'D:\projects\open-browser\diagnostics' 'archive-retention-fixtures'
 if(-not([IO.Path]::GetFullPath($FixtureRoot).StartsWith($expected,[StringComparison]::OrdinalIgnoreCase))){throw 'FIXTURE_ROOT_OUTSIDE_TESTS'}
 if(-not $FixtureUrl){throw 'FIXTURE_URL_REQUIRED'}
 $url=$FixtureUrl
}else{
 $pre=Join-Path $diag 'live-preflight.json'
 if(-not(Test-Path $pre)){throw 'NO_LIVE_PREFLIGHT_FAIL_CLOSED'}
 $signal=Get-Content $pre -Raw -Encoding UTF8|ConvertFrom-Json
 if(([DateTimeOffset]::UtcNow-[DateTimeOffset]::Parse($signal.utc)).TotalMinutes -gt 5){throw 'PREFLIGHT_STALE_FAIL_CLOSED'}
 $url=[string]$signal.source
 if($url -notmatch '^https://chatgpt\.com/c/[0-9a-f-]{36}$'){throw 'UNVERIFIED_CURRENT_CHAT_URL'}
}
$current=HashUrl $url
if(-not(Test-Path (Join-Path $root $current) -PathType Container)){
 if($FixtureRoot){throw 'CURRENT_CHAT_ARCHIVE_NOT_FOUND'}
 # No locally captured responses for this foreground ChatGPT URL yet.
 # Keep all existing archives untouched, but do not poison Task Scheduler
 # with a false failure on an otherwise healthy application.
 Audit 'no_current_chat_archive' @{activeChatHash=$current;action='no_delete'}
 Write-Output 'ARCHIVE_RETENTION_NO_CURRENT_CHAT_DATA'
 return
}
# Same cross-session kernel mutex as LiteResponseMarkdownArchive.Save,
# RecoverMissingIndex and IsPersisted. No file can be deleted between index
# reconciliation and compaction. A busy archive is deferred, never damaged.
$transactionMutex=[Threading.Mutex]::new($false,'Global\OBL_Response_Archive_Transaction_V1')
$ownsMutex=$false
try {
 try {$ownsMutex=$transactionMutex.WaitOne([TimeSpan]::FromSeconds(8))}
 catch [Threading.AbandonedMutexException] {
  $ownsMutex=$true
  Audit 'abandoned_archive_mutex_recovered' @{action='continue_after_owner_crash'}
 }
 if(!$ownsMutex){
  Audit 'archive_transaction_busy' @{action='deferred_no_mutation'}
  Write-Output 'ARCHIVE_RETENTION_DEFERRED_MUTEX_BUSY'
  return
 }
$cut=[DateTime]::UtcNow.AddSeconds(-$GraceSeconds)
$removed=0;$freed=[long]0;$kept=0;$deletedDirs=0;$touched=0
# A project lead/worker may be a BACKGROUND tab, different from the visible
# preflight chat. Never delete its locally cached response history.
$protected=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$null=$protected.Add($current)
if(-not $FixtureRoot){
 $projectsPath=Join-Path $local 'projects.json'
 if(Test-Path -LiteralPath $projectsPath){
  try{
   $projects=Get-Content -LiteralPath $projectsPath -Raw -Encoding UTF8|ConvertFrom-Json
   if($null -eq $projects.Projects){throw 'no_projects'}
   foreach($project in @($projects.Projects)){
    foreach($chat in @($project.Chats)){
     if($chat.Role -notin @('lead','worker')){continue}
     if($chat.Url -notmatch '^https://chatgpt\.com/c/[0-9a-f-]{36}$'){throw 'bad_lead_url'}
     $null=$protected.Add((HashUrl ([string]$chat.Url)))
    }
   }
  }catch{throw 'PROJECT_REGISTRY_INVALID_FAIL_CLOSED'}
 }
}$dirs=@(Get-ChildItem -LiteralPath $root -Directory | Where-Object {$_.Name -match '^[a-f0-9]{64}$'})
foreach($dir in $dirs){
 if($dir.Name -ne $current){
  if($protected.Contains($dir.Name)){
    $existing=@(Get-ChildItem -LiteralPath $dir.FullName -File -Recurse)
    $kept += $existing.Count
    Audit 'protected_project_chat' @{chatHash=$dir.Name;files=$existing.Count}
    continue
  }
  $existing=@(Get-ChildItem -LiteralPath $dir.FullName -File -Recurse)
  $newest=@($existing|Where-Object {$_.LastWriteTimeUtc -gt $cut})
  if($newest.Count -gt 0){Audit 'skip_other_chat_recent' @{chatHash=$dir.Name;count=$existing.Count};$kept+=$existing.Count;continue}
  if($Mode -eq 'Apply'){
    Remove-Item -LiteralPath $dir.FullName -Recurse -Force -ErrorAction Stop
    if(Test-Path $dir.FullName){throw 'REMOVAL_NOT_COMPLETE'}
    $deletedDirs++
  }
  $removed+=$existing.Count
  $freed += [long](($existing|Measure-Object -Property Length -Sum).Sum)
  Audit 'obsolete_chat' @{chatHash=$dir.Name;files=$existing.Count}
  continue
 }
 $index=Join-Path $dir.FullName 'index.jsonl'
 # File removal and its index compaction form ONE retention transaction.
 # If the index is absent or actively changing, defer BOTH operations:
 # deleting Markdown first would strand durable index rows when index
 # compaction is skipped due to the existing quiet-time guard.
 $indexBefore=Get-Item -LiteralPath $index -ErrorAction SilentlyContinue
 if(!$indexBefore -or $indexBefore.LastWriteTimeUtc -gt $cut){
  $existing=@(Get-ChildItem -LiteralPath $dir.FullName -File)
  $kept+=$existing.Count
  Audit 'defer_active_index' @{chatHash=$dir.Name;files=$existing.Count;reason=$(if($indexBefore){'index_recent'}else{'index_missing'});indexUtc=$(if($indexBefore){$indexBefore.LastWriteTimeUtc.ToString('o')}else{''})}
  continue
 }
 $records=@()
 if(Test-Path $index){
  foreach($line in @(Get-Content -LiteralPath $index -Encoding UTF8)){
   try{$q=$line|ConvertFrom-Json;if($q.fileName -match '^[a-f0-9]{16}_(?:partial|[a-f0-9]{16}(?:_partial)?)\.md$' -and $q.turnKey -match '^[a-f0-9]{16}$'){$records+=,$q}}catch{}
  }
 }
 # Use actual file times instead of trusting historical JSONL timestamps.
 $files=@(Get-ChildItem -LiteralPath $dir.FullName -File -Filter '*.md'|Where-Object {$_.Name -match '^[a-f0-9]{16}_(?:partial|[a-f0-9]{16}(?:_partial)?)\.md$'})
 $grouped=@($files|Group-Object {$_.Name.Substring(0,16)} | Sort-Object @{Expression={($_.Group|Sort-Object LastWriteTimeUtc -Descending|Select-Object -First 1).LastWriteTimeUtc};Descending=$true})
 $turnIds=@($grouped|Select-Object -First $KeepTurns|ForEach-Object {$_.Name})
 $allowed=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
 foreach($g in $grouped){
  if($turnIds -notcontains $g.Name){continue}
  $final=@($g.Group|Where-Object {$_.Name -notlike '*_partial.md'}|Sort-Object LastWriteTimeUtc -Descending|Select-Object -First 1)
  if($final.Count -gt 0){$null=$allowed.Add($final[0].Name)}
  $partial=@($g.Group|Where-Object {$_.Name -like '*_partial.md'}|Sort-Object LastWriteTimeUtc -Descending|Select-Object -First 1)
  if($partial.Count -gt 0 -and ($final.Count -eq 0 -or $partial[0].LastWriteTimeUtc -gt $cut)){$null=$allowed.Add($partial[0].Name)}
 }
 $pendingDeletes=@()
 foreach($file in $files){
  if($allowed.Contains($file.Name) -or $file.LastWriteTimeUtc -gt $cut){$kept++;continue}
  $pendingDeletes+=,$file
 }
 # Compact FIRST and remove Markdown SECOND. A crash or concurrent index
 # update must never leave index rows pointing at deleted response files.
 # An interrupted compact-before-delete creates recoverable Markdown orphans.
 $indexCompacted=$false
 # Compact index only after it has been quiet to avoid conflicting with
 # the active writer's AppendAllText. Old index rows alone are not data files.
 if(Test-Path $index){
  $indexInfo=Get-Item $index
  if($indexInfo.LastWriteTimeUtc -le $cut){
   $existingNames=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
   $deleteNames=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
   foreach($file in $pendingDeletes){$null=$deleteNames.Add($file.Name)}
   foreach($file in @(Get-ChildItem -LiteralPath $dir.FullName -File -Filter '*.md')){
    if(!$deleteNames.Contains($file.Name)){$null=$existingNames.Add($file.Name)}
   }
   $lines=@($records|Where-Object {$existingNames.Contains($_.fileName)}|ForEach-Object {$_|ConvertTo-Json -Compress})
   if($Mode -eq 'Apply'){
    $again=Get-Item $index
    if($again.LastWriteTimeUtc -eq $indexInfo.LastWriteTimeUtc -and $again.Length -eq $indexInfo.Length){
     $temp=$index+'.retention.'+[guid]::NewGuid().ToString('N')+'.tmp'
     [IO.File]::WriteAllText($temp,($lines -join "`n")+$(if($lines.Count){"`n"}else{''}),$utf8)
     try{
      Move-Item -LiteralPath $temp -Destination $index -Force -ErrorAction Stop
      $touched++
      $indexCompacted=$true
     }finally{
      if(Test-Path -LiteralPath $temp){Remove-Item -LiteralPath $temp -Force}
     }
    }
   }else{$indexCompacted=$true}
  }
 }
 if(!$indexCompacted){
  $kept+=$pendingDeletes.Count
  Audit 'defer_compaction_race' @{chatHash=$dir.Name;deferredFiles=$pendingDeletes.Count}
  continue
 }
 $referenced=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
 if($Mode -eq 'Apply'){
  foreach($line in @(Get-Content -LiteralPath $index -Encoding UTF8)){
   try{$record=$line|ConvertFrom-Json;if($record.fileName){$null=$referenced.Add([string]$record.fileName)}}catch{}
  }
 }
 foreach($file in $pendingDeletes){
  if($Mode -eq 'Apply'){
   $fresh=Get-Item -LiteralPath $file.FullName -ErrorAction SilentlyContinue
   if(!$fresh -or $fresh.LastWriteTimeUtc -gt $cut -or $referenced.Contains($file.Name)){
    $kept++
    continue
   }
   Remove-Item -LiteralPath $file.FullName -Force -ErrorAction Stop
  }
  $removed++;$freed+=$file.Length
 }
}
Audit 'finished' @{activeChatHash=$current;keepTurns=$KeepTurns;dryRun=($Mode -eq 'Audit');removed=$removed;bytes=$freed;otherChatDirs=$deletedDirs;kept=$kept;indexCompacted=$touched}
$filesLabel=if($Mode -eq 'Audit'){'WOULD_REMOVE_FILES='}else{'REMOVED_FILES='}
$bytesLabel=if($Mode -eq 'Audit'){'WOULD_FREE_BYTES='}else{'BYTES='}
Write-Output ('ARCHIVE_RETENTION_'+$Mode+' '+$filesLabel+$removed+' '+$bytesLabel+$freed+' OLD_CHAT_DIRS='+$deletedDirs+' KEPT_FILES='+$kept+' INDEX_REWRITTEN='+$touched)
} finally {
 if($ownsMutex){$transactionMutex.ReleaseMutex()}
 $transactionMutex.Dispose()
}