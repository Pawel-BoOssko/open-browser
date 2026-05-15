@echo off
setlocal EnableExtensions
chcp 65001 > nul
set DOTNET_CLI_UI_LANGUAGE=en

set "ROOT=D:\projects\open-browser"
set "EXE=%ROOT%\releases\BridgeBrowserAlpha0\src\BridgeBrowserAlpha0\bin\Release\net8.0-windows\BridgeBrowserAlpha0.exe"

if not exist "%EXE%" (
  echo Browser EXE not found:
  echo %EXE%
  pause
  exit /b 1
)

start "" "%EXE%"
exit /b 0
