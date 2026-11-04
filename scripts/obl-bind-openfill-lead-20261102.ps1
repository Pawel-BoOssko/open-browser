$ErrorActionPreference='Stop'
$registry=Join-Path $env:LOCALAPPDATA 'OpenBrowser\ChatGPT-Lite\projects.json'
$target='https://chatgpt.com/c/6ae87f9a-e59c-83eb-914e-2f145de9b27b'
$projectId='06d8677af6b94eeb9bb8e6723eb19424'
$before=[IO.File]::ReadAllText($registry)
$doc=$before | ConvertFrom-Json
$project=@($doc.Projects | Where-Object { $_.Id -eq $projectId -and $_.Key -eq 'of' })
if($project.Count -ne 1){throw 'OPENFILL_PROJECT_NOT_UNIQUE'}
$assigned=@($doc.Projects | ForEach-Object {$_.Chats} | Where-Object Url -eq $target)
if($assigned.Count -gt 0){
 if($assigned.Count -eq 1 -and @($project[0].Chats | Where-Object { $_.Url -eq $target -and $_.Role -eq 'lead' }).Count -eq 1){
  Write-Output 'OPENFILL_LEAD_ALREADY_REGISTERED';return
 }
 throw 'CHAT_ALREADY_ASSIGNED_IN_WRONG_ROLE_OR_PROJECT'
}
if(@($project[0].Chats | Where-Object Role -eq 'lead').Count -gt 0){throw 'OPENFILL_LEAD_ALREADY_EXISTS'}
$chat=[ordered]@{
 Id=[guid]::NewGuid().ToString('N')
 Title='OpenFill: lead rozwoju'
 Url=$target
 Role='lead'
 AddedUtc=[DateTimeOffset]::UtcNow.ToString('o')
 NativeArchivedUtc=$null
 NativeArchiveVerifyPendingUtc=$null
 NativeArchiveVisibilityVerifiedUtc=$null
 NativeArchiveOwnerAccepted429Utc=$null
}
$pattern='("Id"\s*:\s*"' + $projectId + '"[\s\S]*?"Chats"\s*:\s*)\[\s*\]'
$match=[regex]::Match($before,$pattern)
if(-not $match.Success){throw 'PROJECT_EMPTY_CHATS_NOT_FOUND'}
$insert=$match.Groups[1].Value+'['+($chat|ConvertTo-Json -Depth 4)+']'
$after=$before.Substring(0,$match.Index)+$insert+$before.Substring($match.Index+$match.Length)
$check=$after|ConvertFrom-Json
$lead=@($check.Projects | Where-Object Key -eq 'of' | ForEach-Object Chats | Where-Object Role -eq 'lead')
if($lead.Count -ne 1 -or $lead[0].Url -ne $target){throw 'REGISTRY_VERIFY_FAILED'}
if([IO.File]::ReadAllText($registry) -cne $before){throw 'REGISTRY_CONCURRENT_CHANGE_RETRY'}
$tmp=$registry+'.openfill-'+[guid]::NewGuid().ToString('N')+'.tmp'
$backup=$registry+'.before-openfill-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'.bak'
[IO.File]::WriteAllText($tmp,$after,[Text.UTF8Encoding]::new($false))
[IO.File]::Replace($tmp,$registry,$backup)
Write-Output ('OPENFILL_LEAD_SAVED id='+$chat.Id+' url='+$target)
