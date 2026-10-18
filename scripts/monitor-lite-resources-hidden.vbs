Set sh = CreateObject("WScript.Shell")
path = sh.ExpandEnvironmentStrings("%LOCALAPPDATA%") & "\OpenBrowser\ChatGPT-Lite\diagnostics\monitor-lite-resources.ps1"
command = "powershell.exe -NoLogo -NoProfile -NonInteractive -WindowStyle Hidden -File """ & path & """"
sh.Run command, 0, False
