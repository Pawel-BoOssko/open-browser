@echo off
setlocal EnableExtensions
chcp 65001 >nul

set ROOT=D:\projects\open-browser
set LOGDIR=%ROOT%\logs
if not exist "%LOGDIR%" mkdir "%LOGDIR%" 2>nul
set LOG=%LOGDIR%\list_after_close.log

if /I "%~1"=="__run" goto :main

echo Listing Bridge Browser state to:
echo %LOG%
echo ===== LIST START %DATE% %TIME% ===== > "%LOG%"
cmd /V:ON /C call "%~f0" __run >> "%LOG%" 2>&1
set EXITCODE=%ERRORLEVEL%
type "%LOG%"
echo.
echo Listing finished with code %EXITCODE%.
pause
exit /b %EXITCODE%

:main
@echo on
set ROOT=D:\projects\open-browser
ver
cd /d "%ROOT%"

echo Process list
tasklist /FI "IMAGENAME eq BridgeBrowserAlpha0.exe"

echo Root listing
dir "%ROOT%"

echo Logs listing
forfiles /P "%ROOT%\logs" /M *.* /C "cmd /c echo @fdate @ftime @fsize @path" 2>nul

echo Extracted listing
forfiles /P "%ROOT%\extracted" /M *.* /C "cmd /c echo @fdate @ftime @fsize @path" 2>nul

echo GitHub export listing
dir "%ROOT%\github_export" /s

echo Release folder listing
dir "%ROOT%\releases\BridgeBrowserAlpha0" /s

exit /b 0
