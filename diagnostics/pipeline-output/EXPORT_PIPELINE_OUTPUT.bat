@echo off
setlocal
set "OUTDIR=%~dp0"
if not exist "%OUTDIR%" mkdir "%OUTDIR%"
set "ZIP=%OUTDIR%pipeline_output_export.zip"
powershell -NoProfile -Command "Compress-Archive -Path '%OUTDIR%*.txt' -DestinationPath '%ZIP%' -Force"
if %ERRORLEVEL% equ 0 (
    echo Export created: %ZIP%
) else (
    echo Export failed or no .txt files found.
)
pause
endlocal
