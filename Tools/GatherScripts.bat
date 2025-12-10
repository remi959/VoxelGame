:: filepath: Tools/GatherScripts.bat
@echo off
echo Gathering scripts for review...
echo.
powershell -ExecutionPolicy Bypass -File "%~dp0GatherScripts.ps1"
echo.
pause