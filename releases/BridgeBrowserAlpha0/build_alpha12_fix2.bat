@echo off
setlocal EnableExtensions EnableDelayedExpansion
chcp 65001 > nul
set DOTNET_CLI_UI_LANGUAGE=en
call "%~dp0patch_install_build_run.bat"
