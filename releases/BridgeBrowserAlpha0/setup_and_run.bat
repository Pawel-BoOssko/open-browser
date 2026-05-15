@echo off
setlocal
set ROOT=D:\temp\bridge-browser
set RELEASES=%ROOT%\releases
set APP=%RELEASES%\BridgeBrowserAlpha0
set SRC=%~dp0

echo [Bridge Browser Alpha 0] Setup and run

echo.
echo Source folder:
echo %SRC%
dir "%SRC%"

echo.
where dotnet >nul 2>nul
if errorlevel 1 (
  echo ERROR: dotnet SDK not found in PATH. Install .NET 8 SDK and rerun this BAT.
  pause
  exit /b 1
)

echo dotnet version:
dotnet --version

echo.
echo Creating target folders under %ROOT%
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

echo.
echo Copying package to:
echo %APP%
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

if not exist "%ROOT%\config\appsettings.example.json" (
  copy "%APP%\config\appsettings.example.json" "%ROOT%\config\appsettings.example.json"
)

echo.
echo Config folder listing:
dir "%ROOT%\config"

echo.
echo Starting run.bat from installed location...
call "%APP%\run.bat"
exit /b %ERRORLEVEL%
