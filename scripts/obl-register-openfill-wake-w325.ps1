$ErrorActionPreference='Stop'
$path=Join-Path $env:LOCALAPPDATA 'OpenBrowser\ChatGPT-Lite\projects.json'
$url='https://chatgpt.com/c/6ae87f9a-e59c-83eb-914e-2f145de9b27b'
$root=[System.Text.Json.Nodes.JsonNode]::Parse([IO.File]::ReadAllText($path))
$projects=$root['Projects'].AsArray()
$of=@($projects | Where-Object { $_['Key'].ToString() -eq 'of' })
if($of.Count -ne 1){throw ('OPENFILL_PROJECT_COUNT='+$of.Count)}
$matches=@($projects | ForEach-Object { $_['Chats'].AsArray() } | Where-Object { $_['Url'].ToString() -eq $url })
if($matches.Count -gt 1){throw 'DUPLICATE_CHAT_ASSIGNMENT'}
if($matches.Count -eq 1){
  if(@($of[0]['Chats'].AsArray() | Where-Object { $_['Url'].ToString() -eq $url -and $_['Role'].ToString() -eq 'lead' }).Count -ne 1){throw 'CHAT_ASSIGNED_ELSEWHERE_OR_NOT_LEAD'}
  Write-Output 'OPENFILL_LEAD_ALREADY_PRESENT'
  return
}
if(@($of[0]['Chats'].AsArray()).Count -ne 0){throw 'OPENFILL_CHATS_CHANGED'}
$chat=[ordered]@{Id=[guid]::NewGuid().ToString('N');Title='OpenFill: rozwój projektu';Url=$url;Role='lead';AddedUtc=[DateTimeOffset]::UtcNow.ToString('o');NativeArchivedUtc=$null;NativeArchiveVerifyPendingUtc=$null;NativeArchiveVisibilityVerifiedUtc=$null;NativeArchiveOwnerAccepted429Utc=$null}
$of[0]['Chats'].AsArray().Add([System.Text.Json.Nodes.JsonNode]::Parse(($chat | ConvertTo-Json -Compress)))
$backup=$path+'.before-openfill-w325-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'.bak'
Copy-Item -LiteralPath $path -Destination $backup -ErrorAction Stop
$temp=$path+'.openfill-'+[guid]::NewGuid().ToString('N')+'.tmp'
[IO.File]::WriteAllText($temp,$root.ToJsonString(),[Text.UTF8Encoding]::new($false))
Move-Item -LiteralPath $temp -Destination $path -Force
Write-Output ('OPENFILL_LEAD_ADDED backup='+$backup)
