@echo off
setlocal
set "OUTDIR=%~dp0"
if exist "%OUTDIR%*.txt" del /q "%OUTDIR%*.txt"
if exist "%OUTDIR%*.zip" del /q "%OUTDIR%*.zip"
echo Pipeline output cleared.
pause
endlocal
