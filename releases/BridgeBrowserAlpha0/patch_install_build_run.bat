@echo off
setlocal EnableExtensions EnableDelayedExpansion
chcp 65001 > nul
set DOTNET_CLI_UI_LANGUAGE=en

set "ROOT=D:\temp\bridge-browser"
set "TARGET=%ROOT%\releases\BridgeBrowserAlpha0"
set "SRC=%~dp0"
set "LOG_DIR=%ROOT%\logs"
set "LOG=%LOG_DIR%\patch_alpha13_command_bus_install_build.log"

if not exist "%ROOT%" mkdir "%ROOT%"
if not exist "%ROOT%\releases" mkdir "%ROOT%\releases"
if not exist "%LOG_DIR%" mkdir "%LOG_DIR%"

echo [%date% %time%] === alpha13 helper fix1 patch started ===
echo [%date% %time%] === alpha13 helper fix1 patch started === > "%LOG%"
echo [%date% %time%] SRC=%SRC% >> "%LOG%"
echo [%date% %time%] TARGET=%TARGET% >> "%LOG%"

echo.
echo === source listing before copy ===
echo.>>"%LOG%"
echo === source listing before copy ===>>"%LOG%"
dir "%SRC%" >> "%LOG%" 2>&1

echo.
echo === target listing before copy ===
echo.>>"%LOG%"
echo === target listing before copy ===>>"%LOG%"
if exist "%TARGET%" dir "%TARGET%" >> "%LOG%" 2>&1

echo.
echo === kill running BridgeBrowserAlpha0 ===
echo.>>"%LOG%"
echo === kill running BridgeBrowserAlpha0 ===>>"%LOG%"
taskkill /IM BridgeBrowserAlpha0.exe /F >> "%LOG%" 2>&1

echo.
echo === copy patch ===
echo.>>"%LOG%"
echo === copy patch ===>>"%LOG%"
if not exist "%TARGET%" mkdir "%TARGET%"
xcopy "%SRC%*" "%TARGET%\" /E /I /Y /EXCLUDE:%SRC%tools\xcopy_exclude.txt >> "%LOG%" 2>&1
set "COPY_EXIT=!ERRORLEVEL!"
echo COPY_EXIT=!COPY_EXIT!
echo COPY_EXIT=!COPY_EXIT!>>"%LOG%"

echo.
echo === target listing after copy ===
echo.>>"%LOG%"
echo === target listing after copy ===>>"%LOG%"
dir "%TARGET%" >> "%LOG%" 2>&1
if exist "%TARGET%\helper\BridgeBrowserHelper" dir "%TARGET%\helper\BridgeBrowserHelper" >> "%LOG%" 2>&1

echo.
echo === build browser ===
echo.>>"%LOG%"
echo === build browser ===>>"%LOG%"
pushd "%TARGET%\src\BridgeBrowserAlpha0" >> "%LOG%" 2>&1
dotnet restore >> "%LOG%" 2>&1
dotnet build -c Release --no-restore >> "%LOG%" 2>&1
set "BROWSER_BUILD_EXIT=!ERRORLEVEL!"
popd >> "%LOG%" 2>&1
echo BROWSER_BUILD_EXIT=!BROWSER_BUILD_EXIT!
echo BROWSER_BUILD_EXIT=!BROWSER_BUILD_EXIT!>>"%LOG%"

echo.
echo === build helper ===
echo.>>"%LOG%"
echo === build helper ===>>"%LOG%"
pushd "%TARGET%\helper\BridgeBrowserHelper" >> "%LOG%" 2>&1
dotnet restore >> "%LOG%" 2>&1
dotnet build -c Release --no-restore >> "%LOG%" 2>&1
set "HELPER_BUILD_EXIT=!ERRORLEVEL!"
popd >> "%LOG%" 2>&1
echo HELPER_BUILD_EXIT=!HELPER_BUILD_EXIT!
echo HELPER_BUILD_EXIT=!HELPER_BUILD_EXIT!>>"%LOG%"

echo.
echo === helper smoke test LIST_DIR ===
echo.>>"%LOG%"
echo === helper smoke test LIST_DIR ===>>"%LOG%"
if exist "%TARGET%\helper\BridgeBrowserHelper\bin\Release\net8.0\BridgeBrowserHelper.exe" (
  "%TARGET%\helper\BridgeBrowserHelper\bin\Release\net8.0\BridgeBrowserHelper.exe" --request "%TARGET%\helper\requests\list_root.json" >> "%LOG%" 2>&1
) else (
  echo Helper exe missing >> "%LOG%"
)

echo.
echo === helper command envelope log ===
echo.>>"%LOG%"
echo === helper command envelope log ===>>"%LOG%"
if exist "%ROOT%\helper\logs\helper_commands.ndjson" (
  type "%ROOT%\helper\logs\helper_commands.ndjson" >> "%LOG%" 2>&1
) else (
  echo helper_commands.ndjson missing >> "%LOG%"
)

echo.
echo === browser start skipped ===
echo.>>"%LOG%"
echo === browser start skipped ===>>"%LOG%"
echo Build/install finished. Browser was not started automatically in alpha13-fix2.
echo Build/install finished. Browser was not started automatically in alpha13-fix2.>>"%LOG%"
echo To start browser manually run:
echo %TARGET%\run_browser.bat
echo To start browser manually run: %TARGET%\run_browser.bat>>"%LOG%"

type "%LOG%"
echo.
echo Log:
echo %LOG%
pause
exit /b
