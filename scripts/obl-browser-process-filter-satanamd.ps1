# Shared classification for force deploy and watchdog.
# CLI selftests use the same executable name as the GUI but must never
# trigger a duplicate-browser restart or be terminated by StopLive.
function GetOblLiveBrowserProcesses([object[]]$processes){
 return @($processes | Where-Object {
  $command=[string]$_.CommandLine
  $isolatedTest=($command -match '(?i)(?:^|\s)--lite-[a-z0-9-]*selftest(?:\s|$)' -or
    $command -match '(?i)(?:^|\s)--lite-markdown-(?:partial-)?crash-child(?:\s|$)')
  -not $isolatedTest
 })
}
