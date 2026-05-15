@echo off
setlocal
set ROOT=D:\temp\bridge-browser
set APP=%ROOT%\releases\BridgeBrowserAlpha0
set CSPROJ=%APP%\src\BridgeBrowserAlpha0\BridgeBrowserAlpha0.csproj

echo [Bridge Browser Alpha 0] Run
if not exist "%ROOT%" mkdir "%ROOT%"
if not exist "%ROOT%\logs" mkdir "%ROOT%\logs"
if not exist "%ROOT%\extracted" mkdir "%ROOT%\extracted"
if not exist "%ROOT%\profile" mkdir "%ROOT%\profile"
if not exist "%ROOT%\config" mkdir "%ROOT%\config"
if not exist "%ROOT%\releases" mkdir "%ROOT%\releases"

echo.
echo Target root listing:
dir "%ROOT%"

echo.
where dotnet >nul 2>nul
if errorlevel 1 (
  echo ERROR: dotnet SDK not found in PATH. Install .NET 8 SDK.
  pause
  exit /b 1
)

echo dotnet version:
dotnet --version

echo.
if not exist "%CSPROJ%" (
  echo ERROR: project file not found:
  echo %CSPROJ%
  echo Run setup_and_run.bat from extracted ZIP first.
  pause
  exit /b 1
)

echo Building project:
echo %CSPROJ%
dotnet build "%CSPROJ%" -c Release
if errorlevel 1 (
  echo ERROR: build failed.
  pause
  exit /b 1
)

echo.
echo Build output listing:
dir "%APP%\src\BridgeBrowserAlpha0\bin\Release\net8.0-windows"

echo.
echo Starting Bridge Browser Alpha 0...
dotnet run --project "%CSPROJ%" -c Release
set EXITCODE=%ERRORLEVEL%

echo.
echo Application exited with code %EXITCODE%.
pause
exit /b %EXITCODE%
