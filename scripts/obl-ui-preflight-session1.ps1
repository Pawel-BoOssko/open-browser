$ErrorActionPreference='Stop'
$out='D:\projects\open-browser\diagnostics\obl-ui-preflight-session1.json'
try {
 Add-Type -AssemblyName UIAutomationClient
 Add-Type -AssemblyName UIAutomationTypes
 $proc=Get-Process BridgeBrowserAlpha0 | Where-Object {$_.MainWindowHandle -ne 0} | Select-Object -First 1
 if(-not $proc){throw 'No session1 browser window'}
 $window=[System.Windows.Automation.AutomationElement]::FromHandle($proc.MainWindowHandle)
 $all=$window.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.Condition]::TrueCondition)
 $edits=@();$stops=@()
 foreach($element in $all){
  try {
   $type=$element.Current.ControlType.ProgrammaticName
   $name=[string]$element.Current.Name
   if($type -eq 'ControlType.Edit'){
    $valueLength=$null;$pattern=$null;$value=""
    if($element.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern,[ref]$pattern)){$value=[string]([System.Windows.Automation.ValuePattern]$pattern).Current.Value;$valueLength=$value.Length}
    $edits+=@{name=$name;valueLength=$valueLength;enabled=$element.Current.IsEnabled;automationId=$element.Current.AutomationId;className=$element.Current.ClassName;offscreen=$element.Current.IsOffscreen;emptyOrPlaceholder=($value.Trim().Length -eq 0 -or $value.Trim() -in @("Zapytaj ChatGPT","Ask ChatGPT","Ask anything"))}
   }
   if($type -eq 'ControlType.Button' -and $name -match 'stop|zatrzymaj|przerwij|send|wyślij|ChatGPT|Nowy czat|Nowy chat'){$stops+=@{name=$name;enabled=$element.Current.IsEnabled}}
  }catch{}
 }
 $result=@{utc=[DateTimeOffset]::UtcNow.ToString('o');pid=$proc.Id;session=$proc.SessionId;hwnd=[long]$proc.MainWindowHandle;windowTitle=$proc.MainWindowTitle;count=$all.Count;edits=$edits;actionButtons=$stops;error=$null}
}catch{$result=@{utc=[DateTimeOffset]::UtcNow.ToString('o');error=$_.Exception.Message}}
$result|ConvertTo-Json -Depth 6|Set-Content -LiteralPath $out -Encoding UTF8
