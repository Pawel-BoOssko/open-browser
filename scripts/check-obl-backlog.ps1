$ErrorActionPreference='Stop'
if($env:COMPUTERNAME -ne 'SATANAMD'){throw 'WRONG_HOST'}
$f='D:\projects\open-browser\docs\OBL_BACKLOG.md'
if(!(Test-Path -LiteralPath $f -PathType Leaf)){throw 'BACKLOG_MISSING'}
$rows=@(Get-Content -LiteralPath $f -Encoding UTF8 | Where-Object {$_ -match '^\| OBL-P[012]-\d{2} \|'})
$ids=@($rows|ForEach-Object {if($_ -match '^\| (OBL-P[012]-\d{2}) \|'){$Matches[1]}})
$expected=@()
foreach($i in 1..18){$expected+=('OBL-P0-{0:00}' -f $i)}
foreach($i in 1..13){$expected+=('OBL-P1-{0:00}' -f $i)}
foreach($i in 1..3){$expected+=('OBL-P2-{0:00}' -f $i)}
if($ids.Count -ne 34){throw ('EXPECTED_34_ACTUAL_'+$ids.Count)}
if(@($ids|Select-Object -Unique).Count -ne $ids.Count){throw 'DUPLICATE_IDS'}
foreach($id in $expected){if($ids -notcontains $id){throw ('MISSING_'+$id)}}
foreach($row in $rows){
  $parts=@($row.Trim('|').Split('|'))
  if($parts.Count -ne 4){throw ('BAD_ROW_FIELDS_'+$parts.Count)}
  if($parts[1].Trim().Length -lt 18){throw 'REQUIREMENT_TOO_SHORT'}
  if($parts[2] -notmatch 'VERIFIED|PARTIAL|OPEN|DEFERRED|SUPERSEDED'){throw ('BAD_STATUS_'+$parts[0])}
  if($parts[3].Trim().Length -lt 18){throw ('ACCEPTANCE_MISSING_'+$parts[0])}
}
$all=Get-Content -LiteralPath $f -Raw -Encoding UTF8
foreach($term in @('SATANAMD','AUTOMAT','DPAPI','HANDOFF','RAM','DOM','Git','UNCERTAIN','12','rollback','ChatGPT Tasks','PBOX1','OBL','1.73.01')){
 if($all.IndexOf($term,[StringComparison]::OrdinalIgnoreCase) -lt 0){throw ('COVERAGE_TERM_MISSING_'+$term)}
}
Write-Output ('OBL_BACKLOG_VALIDATION_PASS rows='+$ids.Count+' P0=18 P1=13 P2=3 bytes='+((Get-Item $f).Length))