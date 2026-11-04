param([ValidateSet('Audit','Apply','Cycle','SelfTest')][string]$Mode='Audit')
$ErrorActionPreference='Stop'
if($env:COMPUTERNAME -ne 'SATANAMD'){throw 'WRONG_HOST'}
$root='D:\projects\open-browser'
$diag=Join-Path $root 'diagnostics'
$releaseRoot='D:\Apps\OpenBrowserLite\releases'
$events=Join-Path $diag 'obl-clean-generated.jsonl'
$manifestPath=Join-Path $diag 'obl-clean-generated-manifest.json'
$utf8=[Text.UTF8Encoding]::new($false)
function Sha([string]$p) {(Get-FileHash -LiteralPath $p -Algorithm SHA256 -ErrorAction Stop).Hash}
function LogEvent([object]$v) {
 [IO.File]::AppendAllText($events,($v|ConvertTo-Json -Compress -Depth 8)+[Environment]::NewLine,$utf8)
}
function ValidDirectory([string]$path) {
 $full=[IO.Path]::GetFullPath($path)
 if([IO.Path]::GetDirectoryName($full) -ne $diag){return $false}
 if([IO.Path]::GetFileName($full) -notmatch '^build-(\d)(\d{2})(\d{2})$'){return $false}
 if(-not(Test-Path -LiteralPath $full -PathType Container)){return $false}
 if((Get-Item -LiteralPath $full -Force).Attributes -band [IO.FileAttributes]::ReparsePoint){return $false}
 return $true
}
function Contents([string]$path) {
 if(-not(ValidDirectory $path)){throw "INVALID_BUILD_DIRECTORY"}
 $items=@(Get-ChildItem -LiteralPath $path -Recurse -Force -ErrorAction Stop)
 if(@($items | Where-Object {$_.Attributes -band [IO.FileAttributes]::ReparsePoint}).Count -gt 0){throw 'REPARSE_POINT_IN_BUILD'}
 $files=@($items|Where-Object {-not $_.PSIsContainer}|Sort-Object FullName)
 $records=@(foreach($x in $files){
  [ordered]@{name=$x.FullName.Substring($path.Length+1);bytes=[long]$x.Length;sha=Sha $x.FullName}
 })
 return [pscustomobject]@{files=$files.Count;bytes=[long](($files|Measure-Object Length -Sum).Sum);entries=$records}
}
function FrozenCandidates {
 $limit=[DateTime]::UtcNow.AddHours(-2)
 $found=@(Get-ChildItem -LiteralPath $diag -Directory -ErrorAction Stop | Where-Object {$_.Name -match '^build-(\d)(\d{2})(\d{2})$' -and $_.LastWriteTimeUtc -le $limit})
 $list=@(foreach($dir in $found){
  if(-not(ValidDirectory $dir.FullName)){continue}
  $m=[regex]::Match($dir.Name,'^build-(\d)(\d{2})(\d{2})$')
  $version='v'+$m.Groups[1].Value+'.'+([int]$m.Groups[2].Value)+'.'+([int]$m.Groups[3].Value)
  $release=@(Get-ChildItem -LiteralPath $releaseRoot -Directory|Where-Object {$_.Name -like ($version+'_*')}|Select-Object -First 1)
  if($release.Count -eq 0){continue}
  $exe=Join-Path $release[0].FullName 'BridgeBrowserAlpha0.exe'
  if(-not(Test-Path -LiteralPath $exe -PathType Leaf)){continue}
  $content=Contents $dir.FullName
  [pscustomobject]@{path=$dir.FullName;version=$version;releaseExe=$exe;releaseSha=Sha $exe;lastWriteUtc=$dir.LastWriteTimeUtc.ToString('o');files=$content.files;bytes=$content.bytes;entries=$content.entries}
 })
 return $list
}function SameEntries([object]$expected,[object]$actual) {
 if(@($expected).Count -ne @($actual).Count){return $false}
 for($i=0;$i -lt @($expected).Count;$i++){
  if($expected[$i].name -cne $actual[$i].name -or
     $expected[$i].bytes -ne $actual[$i].bytes -or
     $expected[$i].sha -cne $actual[$i].sha){return $false}
 }
 return $true
}
if($Mode -eq 'SelfTest'){
 $a=@([pscustomobject]@{name='a';bytes=10;sha='A'},[pscustomobject]@{name='b';bytes=12;sha='B'})
 if(-not(SameEntries $a $a)){throw 'EQUAL_CONTENTS_REJECTED'}
 if(SameEntries $a @([pscustomobject]@{name='a';bytes=10;sha='A'})){throw 'ADDED_REMOVED_FILE_NOT_CAUGHT'}
 if(SameEntries $a @([pscustomobject]@{name='a';bytes=10;sha='X'},$a[1])){throw 'CHANGED_SHA_NOT_CAUGHT'}
 if(SameEntries $a @($a[0],$a[1],$a[0])){throw 'NEW_FILE_NOT_CAUGHT'}
 if(ValidDirectory 'D:\projects\open-browser\src'){throw 'UNSAFE_ROOT_ACCEPTED'}
 'SELFTEST_PASS checks=5 no deletion'
 exit 0
}
if($Mode -eq 'Audit' -or $Mode -eq 'Cycle'){
 $candidates=@(FrozenCandidates)
 $manifest=[ordered]@{schema=2;host='SATANAMD';utc=[DateTimeOffset]::UtcNow.ToString('o');candidates=$candidates}
 $temp=$manifestPath+'.'+[guid]::NewGuid().ToString('N')+'.tmp'
 try {
  [IO.File]::WriteAllText($temp,($manifest|ConvertTo-Json -Depth 12),$utf8)
  Copy-Item -LiteralPath $temp -Destination $manifestPath -Force -ErrorAction Stop
 } finally {if(Test-Path $temp){Remove-Item -LiteralPath $temp -Force}}
 LogEvent ([ordered]@{utc=[DateTimeOffset]::UtcNow.ToString('o');mode='Audit';schema=2;matched=$candidates.Count;manifestSha=Sha $manifestPath;removed=0})
 foreach($c in $candidates){"Audit $([IO.Path]::GetFileName($c.path)) files=$($c.files) bytes=$($c.bytes)"}
 "AUDIT_FROZEN=$($candidates.Count) REMOVED=0 MANIFEST_SHA=$(Sha $manifestPath)"
 if($Mode -eq 'Audit'){exit 0}
}
if(-not(Test-Path -LiteralPath $manifestPath)){
 'NO_VERIFIED_AUDIT REMOVED=0'; exit 0
}
$manifest=Get-Content -LiteralPath $manifestPath -Raw|ConvertFrom-Json
if($manifest.schema -ne 2 -or $manifest.host -ne 'SATANAMD' -or
    ([DateTimeOffset]::UtcNow-[DateTimeOffset]::Parse($manifest.utc)).TotalHours -gt 4){
 throw 'UNSAFE_OR_STALE_AUDIT_MANIFEST'
}
$candidates=@($manifest.candidates)
# VERIFY THE ENTIRE FROZEN SNAPSHOT BEFORE REMOVING ANYTHING.
# Apply must NEVER re-enumerate newly eligible build directories.
foreach($c in $candidates){
 if(-not(ValidDirectory $c.path)){throw 'FROZEN_PATH_CHANGED'}
 if((Get-Item -LiteralPath $c.path).LastWriteTimeUtc.ToString('o') -cne $c.lastWriteUtc){
  throw 'FROZEN_DIRECTORY_TIMESTAMP_CHANGED'
 }
 if(-not(Test-Path -LiteralPath $c.releaseExe -PathType Leaf) -or
    (Sha $c.releaseExe) -cne $c.releaseSha){throw 'FROZEN_RELEASE_CHANGED'}
 $current=Contents $c.path
 if($current.files -ne $c.files -or $current.bytes -ne $c.bytes -or
    -not(SameEntries @($c.entries) @($current.entries))){
  throw 'FROZEN_BUILD_CONTENTS_CHANGED'
 }
}
$removed=0;[long]$freed=0
foreach($c in $candidates){
 Remove-Item -LiteralPath $c.path -Force -Recurse -ErrorAction Stop
 if(Test-Path -LiteralPath $c.path){throw 'DELETE_NOT_COMPLETE'}
 $removed++;$freed+=[long]$c.bytes
 LogEvent ([ordered]@{utc=[DateTimeOffset]::UtcNow.ToString('o');mode='Apply';path=$c.path;files=$c.files;bytes=$c.bytes;removed=$true;schema=2})
 "Apply $([IO.Path]::GetFileName($c.path)) files=$($c.files) bytes=$($c.bytes)"
}
"FROZEN_MATCHED=$($candidates.Count) REMOVED=$removed FREED_BYTES=$freed"
