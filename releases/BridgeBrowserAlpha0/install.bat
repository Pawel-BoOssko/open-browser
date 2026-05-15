@echo off
setlocal
set ROOT=D:\projects\open-browser
set RELEASES=%ROOT%\releases
set APP=%RELEASES%\BridgeBrowserAlpha0
set SRC=%~dp0

echo [Bridge Browser Alpha 0] Install only

echo.
echo Source folder:
dir "%SRC%"

where dotnet >nul 2>nul
if errorlevel 1 (
  echo ERROR: dotnet SDK not found in PATH. Install .NET 8 SDK and rerun this BAT.
  pause
  exit /b 1
)

if not exist "D:\temp" mkdir "D:\temp"
if not exist "%ROOT%" mkdir "%ROOT%"
if not exist "%ROOT%\logs" mkdir "%ROOT%\logs"
if not exist "%ROOT%\extracted" mkdir "%ROOT%\extracted"
if not exist "%ROOT%\profile" mkdir "%ROOT%\profile"
if not exist "%ROOT%\config" mkdir "%ROOT%\config"
if not exist "%RELEASES%" mkdir "%RELEASES%"

echo.
echo Target before copy:
dir "%ROOT%"
if exist "%APP%" dir "%APP%"

if not exist "%APP%" mkdir "%APP%"
xcopy "%SRC%*" "%APP%\" /E /I /Y
if errorlevel 1 (
  echo ERROR: copy failed.
  pause
  exit /b 1
)

echo.
echo Target after copy:
dir "%APP%"
dir "%APP%\src\BridgeBrowserAlpha0"

if not exist "%ROOT%\config\appsettings.example.json" copy "%APP%\config\appsettings.example.json" "%ROOT%\config\appsettings.example.json"

echo.
echo Building installed project...
dotnet build "%APP%\src\BridgeBrowserAlpha0\BridgeBrowserAlpha0.csproj" -c Release
if errorlevel 1 (
  echo ERROR: build failed.
  pause
  exit /b 1
)

echo.
echo Build output listing:
dir "%APP%\src\BridgeBrowserAlpha0\bin\Release\net8.0-windows"

echo Install complete.
pause
exit /b 0
